using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>See
    /// http://www.ntfs.com/ntfs-mft.htm
    /// http://dubeyko.com/development/FileSystems/NTFS/ntfsdoc.pdf
    /// https://en.wikipedia.org/wiki/NTFS
    /// </remarks>
    internal class NtfsPartition : PartitionManager.PartitionBase
    {
        internal NtfsPartition(bool hidden, uint startSector, uint sectorCount)
            : base(startSector, sectorCount)
        {
            Hidden = hidden;
        }

        internal ulong this[string name]
        {
            get { return _metadataFilesLBAByName[name]; }
        }

        internal uint BytesPerSector { get; private set; }

        internal ulong ClustersPerFileRecordSegment { get; private set; }

        internal ulong ClustersPerIndexBuffer { get; private set; }

        internal ulong ClusterSize
        {
            get { return SectorsPerCluster * BytesPerSector; }
        }

        internal uint HeadsCount { get; private set; }

        internal bool Hidden { get; private set; }

        internal uint HiddenSectorsCount { get; private set; }

        internal byte MediaDescriptor { get; private set; }

        internal unsafe NtfsMFTFileRecord MFT
        {
            get
            {
                if (null == _mft) { throw new InvalidOperationException(); }
                return _mft;
            }
        }

        internal ulong MFTClusterNumber { get; private set; }

        internal ulong MFTMirrorClusterNumber { get; private set; }

        internal uint SectorsPerCluster { get; private set; }

        internal uint SectorsPerTrack { get; private set; }

        internal ulong TotalSectorsCount { get; private set; }

        internal ulong VolumeSerialNumber { get; private set; }

        /// <summary>Retrieve and store pointers at system metadata files.</summary>
        internal unsafe void CaptureMetadataFilePointers()
        {
            // Start at $MFT LBA.
            ulong currentRecordLBA = StartSector + (MFTClusterNumber * SectorsPerCluster);
            byte* currentRecord = null;
            try {
                for (int mdfIndex = 0; mdfIndex < 16; mdfIndex++) {
                    _metadataFileLBAs[mdfIndex] = currentRecordLBA;
                    currentRecord = Manager.Read(currentRecordLBA, SectorsPerCluster, currentRecord);
                    NtfsFileRecord* header = (NtfsFileRecord*)currentRecord;
                    header->AssertRecordType();
                    NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)header + header->AttributesOffset);
                    // Walk attributes. Technically this is useless. However that let us trace metafile names.
                    for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                        if (NtfsAttributeType.AttributeFileName == currentAttribute->AttributeType) {
                            NtfsFileNameAttribute* nameAttribute = (NtfsFileNameAttribute*)currentAttribute;
                            string metadataFileName = Encoding.Unicode.GetString((byte*)&nameAttribute->Name, nameAttribute->NameLength * sizeof(char));
                            if ("$MFT" == metadataFileName) {
                                // It is not expected for a partition to have more than one $Mft record.
                                if (null != _mft) { throw new AssertionException("Several $Mft record were found."); }
                                _mft = NtfsMFTFileRecord.Create(this, currentRecord);
                            }
                            _metadataFilesLBAByName.Add(metadataFileName, currentRecordLBA);
                        }
                        currentAttribute = (NtfsAttribute*)((byte*)currentAttribute +
                            ((NtfsAttributeType.AttributeNone == currentAttribute->AttributeType)
                                ? sizeof(ulong)
                                : currentAttribute->Length));
                    }
                    currentRecordLBA += header->BytesAllocated / BytesPerSector;
                }
            }
            finally { if (null != currentRecord) { Marshal.FreeCoTaskMem((IntPtr)currentRecord); } }
        }

        internal unsafe ulong CountFiles()
        {
            // Start at $MFT LBA.
            // ulong mftLBA = _metadataFileLBAs[(int)NtfsWellKnownMetadataFiles.MFT];
            //byte* buffer = null;
            //try {
            //    EnumerateRecordAttributes(mftLBA, ref buffer, delegate (NtfsAttribute* found) {
            //        Console.WriteLine(found->AttributeType.ToString());
            //        return true;
            //    });
            //}
            //finally { if (null != buffer) { Marshal.FreeCoTaskMem((IntPtr)buffer); } }

            // TODO :
            // - Retrieve partition MFT record
            // - Trigger Bitmap attribute capture
            // - Read records.
            // - Count them
            ulong fileCount = 0;
            NtfsFileRecord* rootRecord = null;
            _mft.EnumerateRecords(this,
                delegate (NtfsFileRecord* record) {
                    NtfsFileNameAttribute* nameAttribute =
                        (NtfsFileNameAttribute*)record->GetAttribute(NtfsAttributeType.AttributeFileName);
                    if (null != nameAttribute) {
                        string name = nameAttribute->GetName();
                        if ("." == name) {
                            Console.WriteLine("Root directory found");
                            rootRecord = record;
                            return false;
                        }
                    }
                    return true;
                });
            if (null == rootRecord) {
                throw new ApplicationException();
            }
            NtfsRootIndexAttribute* rootIndexAttribute =
                (NtfsRootIndexAttribute*)rootRecord->GetAttribute(NtfsAttributeType.AttributeIndexRoot);
            if (null == rootIndexAttribute) {
                throw new ApplicationException("Root index attribute not found.");
            }
            Helpers.BinaryDump((byte*)rootIndexAttribute, 128);
            rootIndexAttribute->Dump();
            throw new NotImplementedException();
            //byte* mftRecord = null;
            //ulong result = 0;
            //try {
            //    // Find Bitmap attribute. TODO Handle case where this require an attribute list.
            //    NtfsAttribute* bitmapAttribute = GetFileRecordAttribute(mftLBA, NtfsAttributeType.AttributeBitmap, ref mftRecord);
            //    if (null == bitmapAttribute) { throw new ApplicationException(); }
            //    if (0 == bitmapAttribute->Nonresident) {
            //        // Extremely unlikely.
            //        throw new NotImplementedException();
            //    }
            //    NtfsBitmapAttribute* nrBitmapAttribute = (NtfsBitmapAttribute*)bitmapAttribute;
            //    int bitmapBufferLength = 8192;
            //    byte[] bitmapBuffer = new byte[bitmapBufferLength];
            //    using (Stream dataStream = nrBitmapAttribute->nonResidentHeader.OpenDataStream(this)) {
            //        int readCount;
            //        while (0 != (readCount = dataStream.Read(bitmapBuffer, 0, bitmapBufferLength))) {
            //        }
            //    }
            //    return result;
            //}
            //finally { if (null != mftRecord) { Marshal.FreeCoTaskMem((IntPtr)mftRecord); } }
        }

        internal unsafe void DumpFirstFileNames()
        {
            // Start at $MFT LBA.
            ulong currentRecordLBA = StartSector + (MFTClusterNumber * SectorsPerCluster);
            byte* buffer = null;
            try {
                ulong clusterSize = Manager.Geometry.BytesPerSector * SectorsPerCluster;
                uint bufferSize = 0;
                NtfsFileRecord* header = null;
                uint readOpCount = 0;
                for (int fileIndex = 0; fileIndex < 1024; fileIndex++) {
                    if (null == header) {
                        buffer = Manager.ReadBlocks(currentRecordLBA, out bufferSize, SectorsPerCluster, buffer);
                        readOpCount++;
                        header = (NtfsFileRecord*)buffer;
                    }
                    if (0xC6 == fileIndex) {
                        uint bufferOffset = (uint)((byte*)header - buffer);
                        Helpers.BinaryDump((byte*)header, bufferSize - bufferOffset);
                    }
                    if (0 == header->Ntfs.Type) {
                        // Trigger data read on next LBA
                        header = null;
                        currentRecordLBA += SectorsPerCluster;
                        continue;
                    }
                    header->AssertRecordType();
                    NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)header + header->AttributesOffset);
                    // Walk attributes. Technically this is useless. However that let us trace metafile names.
                    for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                        if (ushort.MaxValue == currentAttribute->AttributeNumber) { break; }
                        if (header->BytesInUse < ((byte*)currentAttribute - (byte*)header)) { break; }
                        if (NtfsAttributeType.AttributeFileName == currentAttribute->AttributeType) {
                            NtfsFileNameAttribute* nameAttribute = (NtfsFileNameAttribute*)currentAttribute;
                            string metadataFileName = Encoding.Unicode.GetString((byte*)&nameAttribute->Name, nameAttribute->NameLength * sizeof(char));
                            Console.WriteLine(metadataFileName);
                        }
                        if (NtfsAttributeType.AttributeNone == currentAttribute->AttributeType) { break; }
                        currentAttribute = (NtfsAttribute*)((byte*)currentAttribute + currentAttribute->Length);
                    }
                    header = (NtfsFileRecord*)((byte*)header + header->BytesAllocated);
                    if (bufferSize <= ((byte*)header - buffer)) {
                        header = null;
                        currentRecordLBA += SectorsPerCluster;
                    }
                }
            }
            finally { if (null != buffer) { Marshal.FreeCoTaskMem((IntPtr)buffer); } }
        }

        internal unsafe void EnumerateRecordAttributes(ulong recordLBA, ref byte* buffer,
            RecordAttributeEnumeratorCallbackDelegate callback)
        {
            buffer = Manager.Read(recordLBA, SectorsPerCluster, buffer);
            NtfsFileRecord* header = (NtfsFileRecord*)buffer;
            header->AssertRecordType();
            if (1024 < header->BytesAllocated) {
                throw new NotImplementedException();
            }
            // Walk attributes, seeking for the searched one.
            NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)header + header->AttributesOffset);
            // Walk attributes. Technically this is useless. However that let us trace metafile names.
            for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                if (ushort.MaxValue == currentAttribute->AttributeNumber) { break; }
                if (header->BytesInUse < ((byte*)currentAttribute - (byte*)header)) { break; }
                if (!callback(currentAttribute)) { return; }
                if (NtfsAttributeType.AttributeNone == currentAttribute->AttributeType) { break; }
                currentAttribute = (NtfsAttribute*)((byte*)currentAttribute + currentAttribute->Length);
            }
            return;
        }

        /// <summary>This is an optimization. The $Bitmap metadata file is heavily used. We don't want
        /// to read the header again and again.</summary>
        /// <remarks>Not currently used in code.</remarks>
        internal unsafe void GetBitmapProxy()
        {
            ulong bitmapLBA = _metadataFileLBAs[(int)NtfsWellKnownMetadataFiles.Bitmap];
            byte* buffer = null;
            throw new NotImplementedException();
            //NtfsNonResidentAttribute* bitmapDataAttribute = (NtfsNonResidentAttribute*)
            //    GetFileRecordAttribute(bitmapLBA, NtfsAttributeType.AttributeData, ref buffer);
            //Stream bitmapStream = bitmapDataAttribute->OpenDataStream(this);
            //ulong initializedSize = bitmapDataAttribute->InitializedSize;
            //int bitmapBufferLength = 1;
            //byte[] bitmapBuffer = new byte[bitmapBufferLength];
            //int lastReadCount = 0;
            //int totalUsedClusters = 0;
            //int totalIndexedClusters = 0;
            //for (ulong offset = 0; offset < initializedSize; offset += (uint)lastReadCount) {
            //    lastReadCount = bitmapStream.Read(bitmapBuffer, 0, bitmapBufferLength);
            //    // Invariant check
            //    if (bitmapBufferLength < lastReadCount) { throw new ApplicationException(); }
            //    // Invariant check
            //    if (0 == lastReadCount) { throw new ApplicationException(); }
            //    // For debugging purpose
            //    for (int index = 0; index < lastReadCount; index++) {
            //        byte item = bitmapBuffer[index];
            //        for(int bitIndex = 0; bitIndex < 8; bitIndex++) {
            //            totalIndexedClusters++;
            //            if (0 != (item & (byte)(1 << bitIndex))) { totalUsedClusters++; }
            //        }
            //    }
            //}
            //// For debuging purpose.
            //int unusedBytes = 0;
            //while (true) {
            //    int trashBytes = bitmapStream.Read(bitmapBuffer, 0, bitmapBufferLength);
            //    unusedBytes += trashBytes;
            //    if (0 == trashBytes) { break; }
            //}
            //totalIndexedClusters -= (unusedBytes * 8);
            //Console.WriteLine("{0} indexed clusters, {1} in use, {2} free, {3} extra bits.",
            //    totalIndexedClusters, totalUsedClusters, totalIndexedClusters - totalUsedClusters,
            //    unusedBytes * 8);
        }

        internal unsafe void InterpretBootSector()
        {
            byte* sector = null;
            try {
                sector = Manager.Read(StartSector, 1, sector);
                if (0x55 != sector[510]) { throw new ApplicationException(); }
                if (0xAA != sector[511]) { throw new ApplicationException(); }
                byte* sectorPosition = sector + 3;
                // Verify OEMID field conformance.
                int OEMIDlength = Constants.OEMID.Length;
                for(int index = 0; index < OEMIDlength; index++) {
                    if (*(sectorPosition++) != Constants.OEMID[index]) {
                        throw new ApplicationException();
                    }
                }
                BytesPerSector = *(ushort*)(sectorPosition); sectorPosition += sizeof(ushort);
                SectorsPerCluster = *(byte*)(sectorPosition++);
                sectorPosition += sizeof(ushort); // Unused
                sectorPosition += 3; // Unused
                sectorPosition += sizeof(ushort); // Unused
                MediaDescriptor = *(byte*)(sectorPosition++);
                sectorPosition += sizeof(ushort); // Unused
                SectorsPerTrack = *(ushort*)(sectorPosition); sectorPosition += sizeof(ushort);
                HeadsCount = *(ushort*)(sectorPosition); sectorPosition += sizeof(ushort);
                HiddenSectorsCount = *(uint*)(sectorPosition); sectorPosition += sizeof(uint);
                sectorPosition += sizeof(uint); // Unused
                sectorPosition += sizeof(uint); // Unused
                TotalSectorsCount = *(ulong*)(sectorPosition); sectorPosition += sizeof(ulong);
                MFTClusterNumber = *(ulong*)(sectorPosition); sectorPosition += sizeof(ulong);
                MFTMirrorClusterNumber = *(ulong*)(sectorPosition); sectorPosition += sizeof(ulong);
                sbyte rawClusteringValue = *(sbyte*)(sectorPosition++);
                ClustersPerFileRecordSegment = (0 < rawClusteringValue)
                    ? (byte)rawClusteringValue
                    : (1UL << (-rawClusteringValue));
                sectorPosition += 3; // Unused
                rawClusteringValue = *(sbyte*)(sectorPosition++);
                ClustersPerIndexBuffer = (0 < rawClusteringValue)
                    ? (byte)rawClusteringValue
                    : (1UL << (-rawClusteringValue));
                sectorPosition += 3; // Unused
                VolumeSerialNumber = *(ulong*)(sectorPosition); sectorPosition += sizeof(ulong);
                sectorPosition += sizeof(uint); // Unused
                if (0x54 != (sectorPosition - sector)) { throw new ApplicationException(); }
            }
            finally { if(null != sector) { Marshal.FreeCoTaskMem((IntPtr)sector); } }
        }

        /// <summary>Monitor bad clusters. Maintains an internal list of bad clusters and
        /// periodically update the list.</summary>
        internal unsafe void MonitorBadClusters()
        {
            UpdateBadClustersMap();
            // TODO : Implement periodic polling.
        }

        /// <summary>For testing purpose only. No real use until now.</summary>
        internal unsafe void ReadBitmap()
        {
            byte* buffer = null;
            uint bufferSize = 0;
            ulong clusterSize = Manager.Geometry.BytesPerSector * SectorsPerCluster;
            ulong currentRecordLBA =
                _metadataFileLBAs[(int)NtfsWellKnownMetadataFiles.Bitmap];
            NtfsFileRecord* header = null;
            uint readOpCount = 0;
            try {
                buffer = Manager.ReadBlocks(currentRecordLBA, out bufferSize, SectorsPerCluster, buffer);
                readOpCount++;
                header = (NtfsFileRecord*)buffer;
                header->AssertRecordType();
                NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)header + header->AttributesOffset);
                // Walk attributes.
                for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                    if (ushort.MaxValue == currentAttribute->AttributeNumber) { break; }
                    if (header->BytesInUse < ((byte*)currentAttribute - (byte*)header)) { break; }
                    if (NtfsAttributeType.AttributeNone == currentAttribute->AttributeType) { break; }
                    NtfsNonResidentAttribute* nonResident = (0 == currentAttribute->Nonresident)
                        ? null
                        : (NtfsNonResidentAttribute*)currentAttribute;
                    if (null != nonResident) {
                        nonResident->DecodeRunArray();
                    }
                    currentAttribute = (NtfsAttribute*)((byte*)currentAttribute + currentAttribute->Length);
                }
                header = (NtfsFileRecord*)((byte*)header + header->BytesAllocated);
                if (bufferSize <= ((byte*)header - buffer)) {
                    header = null;
                    currentRecordLBA += SectorsPerCluster;
                }
            }
            finally { if (null != buffer) { Marshal.FreeCoTaskMem((IntPtr)buffer); } }
        }

        internal unsafe byte* Read(ulong logicalBlockAddress, uint count = 1, byte* into = null)
        {
            return Manager.Read(logicalBlockAddress + this.StartSector, count, into);
        }

        internal unsafe byte* ReadBlocks(ulong logicalBlockAddress, out uint totalBytesRead,
            uint blocksCount = 1, byte* into = null)
        {
            return Manager.ReadBlocks(logicalBlockAddress + this.StartSector, out totalBytesRead,
                blocksCount, into);
        }

        internal unsafe void SeekTo(ulong logicalBlockAddress)
        {
            Manager.SeekTo(logicalBlockAddress + this.StartSector);
        }

        internal unsafe void UpdateBadClustersMap()
        {
            byte* buffer = null;
            uint bufferSize = 0;
            ulong clusterSize = Manager.Geometry.BytesPerSector * SectorsPerCluster;
            ulong currentRecordLBA =
                _metadataFileLBAs[(int)NtfsWellKnownMetadataFiles.BadClusters];
            NtfsFileRecord* header = null;
            uint readOpCount = 0;
            try {
                buffer = Manager.ReadBlocks(currentRecordLBA, out bufferSize, SectorsPerCluster, buffer);
                readOpCount++;
                header = (NtfsFileRecord*)buffer;
                header->AssertRecordType();
                NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)header + header->AttributesOffset);
                // Walk attributes.
                for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                    if (ushort.MaxValue == currentAttribute->AttributeNumber) { break; }
                    if (header->BytesInUse < ((byte*)currentAttribute - (byte*)header)) { break; }
                    if (NtfsAttributeType.AttributeNone == currentAttribute->AttributeType) { break; }
                    NtfsNonResidentAttribute* nonResident = (0 == currentAttribute->Nonresident)
                        ? null
                        : (NtfsNonResidentAttribute*)currentAttribute;
                    if ((null != nonResident) && ("$Bad" == nonResident->Attribute.Name)) {
                        if (0 != nonResident->InitializedSize) { throw new NotImplementedException(); }
                    }
                    currentAttribute = (NtfsAttribute*)((byte*)currentAttribute + currentAttribute->Length);
                }
                header = (NtfsFileRecord*)((byte*)header + header->BytesAllocated);
                if (bufferSize <= ((byte*)header - buffer)) {
                    header = null;
                    currentRecordLBA += SectorsPerCluster;
                }
            }
            finally { if (null != buffer) { Marshal.FreeCoTaskMem((IntPtr)buffer); } }
        }

        private ulong[] _metadataFileLBAs = new ulong[16];
        private NtfsMFTFileRecord _mft;
        private Dictionary<string, ulong> _metadataFilesLBAByName = new Dictionary<string, ulong>();
    }
}
