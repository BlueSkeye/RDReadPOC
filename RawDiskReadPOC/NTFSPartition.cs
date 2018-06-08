﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using RawDiskReadPOC.NTFS;

namespace RawDiskReadPOC
{
    /// <summary></summary>
    /// <remarks>See
    /// http://www.ntfs.com/ntfs-mft.htm
    /// http://dubeyko.com/development/FileSystems/NTFS/ntfsdoc.pdf
    /// https://en.wikipedia.org/wiki/NTFS
    /// </remarks>
    internal class NTFSPartition : PartitionManager.PartitionBase
    {
        internal NTFSPartition(bool hidden, uint startSector, uint sectorCount)
            : base(startSector, sectorCount)
        {
            Hidden = hidden;
        }

        internal unsafe delegate bool RecordAttributeEnumeratorCallbackDelegate(NtfsAttribute* value);

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
                    NtfsFileRecordHeader* header = (NtfsFileRecordHeader*)currentRecord;
                    if (FileRecordMarker != header->Ntfs.Type) {
                        // We expect a 'FILE' NTFS record here.
                        throw new NotImplementedException();
                    }
                    NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)header + header->AttributesOffset);
                    // Walk attributes. Technically this is useless. However that let us trace metafile names.
                    for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                        if (NtfsAttributeType.AttributeFileName == currentAttribute->AttributeType) {
                            NtfsFileNameAttribute* nameAttribute = (NtfsFileNameAttribute*)
                                ((byte*)currentAttribute + sizeof(NtfsResidentAttribute));
                            string metadataFileName = Encoding.Unicode.GetString((byte*)&nameAttribute->Name, nameAttribute->NameLength * sizeof(char));
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
            ulong mftLBA = _metadataFileLBAs[(int)NtfsWellKnownMetadataFiles.MFT];
            byte* buffer = null;
            try {
                EnumerateRecordAttributes(mftLBA, ref buffer, delegate (NtfsAttribute* found) {
                    Console.WriteLine(found->AttributeType.ToString());
                    return true;
                });
            }
            finally { if (null != buffer) { Marshal.FreeCoTaskMem((IntPtr)buffer); } }
            byte* mftRecord = null;
            ulong result = 0;
            try {
                // Find Bitmap attribute. TODO Handle case where this require an attribute list.
                NtfsAttribute* bitmapAttribute = GetFileRecordAttribute(mftLBA, NtfsAttributeType.AttributeBitmap, ref mftRecord);
                if (null == bitmapAttribute) { throw new ApplicationException(); }
                if (0 == bitmapAttribute->Nonresident) {
                    // Extremely unlikely.
                    throw new NotImplementedException();
                }
                NtfsNonResidentAttribute* nrBitmapAttribute = (NtfsNonResidentAttribute*)bitmapAttribute;
                // Useless due to default parameter value in OpenDataStream
                // List<NtfsNonResidentAttribute.LogicalChunk> chunks = nrBitmapAttribute->DecodeRunArray();
                int bitmapBufferLength = 8192;
                byte[] bitmapBuffer = new byte[bitmapBufferLength];
                using (Stream dataStream = nrBitmapAttribute->OpenDataStream(this)) {
                    int readCount;
                    while (0 != (readCount = dataStream.Read(bitmapBuffer, 0, bitmapBufferLength))) {
                    }
                }
                return result;
            }
            finally { if (null != mftRecord) { Marshal.FreeCoTaskMem((IntPtr)mftRecord); } }
        }

        internal unsafe void DumpFirstFileNames()
        {
            // Start at $MFT LBA.
            ulong currentRecordLBA = StartSector + (MFTClusterNumber * SectorsPerCluster);
            byte* buffer = null;
            try {
                ulong clusterSize = Manager.Geometry.BytesPerSector * SectorsPerCluster;
                uint bufferSize = 0;
                NtfsFileRecordHeader* header = null;
                uint readOpCount = 0;
                for (int fileIndex = 0; fileIndex < 1024; fileIndex++) {
                    if (null == header) {
                        buffer = Manager.ReadBlocks(currentRecordLBA, out bufferSize, SectorsPerCluster, buffer);
                        readOpCount++;
                        header = (NtfsFileRecordHeader*)buffer;
                    }
                    if (0xC6 == fileIndex) {
                        uint bufferOffset = (uint)((byte*)header - buffer);
                        Helpers.Dump((byte*)header, bufferSize - bufferOffset);
                    }
                    if (0 == header->Ntfs.Type) {
                        // Trigger data read on next LBA
                        header = null;
                        currentRecordLBA += SectorsPerCluster;
                        continue;
                    }
                    if (FileRecordMarker != header->Ntfs.Type) {
                        // We expect a 'FILE' NTFS record here.
                        throw new NotImplementedException();
                    }
                    NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)header + header->AttributesOffset);
                    // Walk attributes. Technically this is useless. However that let us trace metafile names.
                    for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                        if (ushort.MaxValue == currentAttribute->AttributeNumber) { break; }
                        if (header->BytesInUse < ((byte*)currentAttribute - (byte*)header)) { break; }
                        if (NtfsAttributeType.AttributeFileName == currentAttribute->AttributeType) {
                            NtfsFileNameAttribute* nameAttribute = (NtfsFileNameAttribute*)
                                ((byte*)currentAttribute + sizeof(NtfsResidentAttribute));
                            string metadataFileName = Encoding.Unicode.GetString((byte*)&nameAttribute->Name, nameAttribute->NameLength * sizeof(char));
                            Console.WriteLine(metadataFileName);
                        }
                        if (NtfsAttributeType.AttributeNone == currentAttribute->AttributeType) { break; }
                        currentAttribute = (NtfsAttribute*)((byte*)currentAttribute + currentAttribute->Length);
                    }
                    header = (NtfsFileRecordHeader*)((byte*)header + header->BytesAllocated);
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
            if (FileRecordMarker != *((uint*)buffer)) {
                // We expect a 'FILE' NTFS record here.
                throw new NotImplementedException();
            }
            NtfsFileRecordHeader* header = (NtfsFileRecordHeader*)buffer;
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
            NtfsNonResidentAttribute* bitmapDataAttribute = (NtfsNonResidentAttribute*)
                GetFileRecordAttribute(bitmapLBA, NtfsAttributeType.AttributeData, ref buffer);
            Stream bitmapStream = bitmapDataAttribute->OpenDataStream(this);
            ulong initializedSize = bitmapDataAttribute->InitializedSize;
            int bitmapBufferLength = 1;
            byte[] bitmapBuffer = new byte[bitmapBufferLength];
            int lastReadCount = 0;
            int totalUsedClusters = 0;
            int totalIndexedClusters = 0;
            for (ulong offset = 0; offset < initializedSize; offset += (uint)lastReadCount) {
                lastReadCount = bitmapStream.Read(bitmapBuffer, 0, bitmapBufferLength);
                // Invariant check
                if (bitmapBufferLength < lastReadCount) { throw new ApplicationException(); }
                // Invariant check
                if (0 == lastReadCount) { throw new ApplicationException(); }
                // For debugging purpose
                for (int index = 0; index < lastReadCount; index++) {
                    byte item = bitmapBuffer[index];
                    for(int bitIndex = 0; bitIndex < 8; bitIndex++) {
                        totalIndexedClusters++;
                        if (0 != (item & (byte)(1 << bitIndex))) { totalUsedClusters++; }
                    }
                }
            }
            // For debuging purpose.
            int unusedBytes = 0;
            while (true) {
                int trashBytes = bitmapStream.Read(bitmapBuffer, 0, bitmapBufferLength);
                unusedBytes += trashBytes;
                if (0 == trashBytes) { break; }
            }
            totalIndexedClusters -= (unusedBytes * 8);
            Console.WriteLine("{0} indexed clusters, {1} in use, {2} free, {3} extra bits.",
                totalIndexedClusters, totalUsedClusters, totalIndexedClusters - totalUsedClusters,
                unusedBytes * 8);
        }

        /// <summary>Retrieve the Nth attribute of a given kind from a file record.</summary>
        /// <param name="recordLBA"></param>
        /// <param name="kind"></param>
        /// <param name="buffer"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        internal unsafe NtfsAttribute* GetFileRecordAttribute(ulong recordLBA, NtfsAttributeType kind,
            ref byte* buffer, uint order = 1)
        {
            NtfsAttribute* result = null;
            EnumerateRecordAttributes(recordLBA, ref buffer, delegate (NtfsAttribute* found) {
                if (kind == found->AttributeType) {
                    if (0 == --order) {
                        result = found;
                        return false;
                    }
                }
                return true;
            });
            return result;
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
                int OEMIDlength = OEMID.Length;
                for(int index = 0; index < OEMIDlength; index++) {
                    if (*(sectorPosition++) != OEMID[index]) {
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
            NtfsFileRecordHeader* header = null;
            uint readOpCount = 0;
            try {
                buffer = Manager.ReadBlocks(currentRecordLBA, out bufferSize, SectorsPerCluster, buffer);
                readOpCount++;
                header = (NtfsFileRecordHeader*)buffer;
                if (FileRecordMarker != header->Ntfs.Type) {
                    // We expect a 'FILE' NTFS record here.
                    throw new NotImplementedException();
                }
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
                header = (NtfsFileRecordHeader*)((byte*)header + header->BytesAllocated);
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
            NtfsFileRecordHeader* header = null;
            uint readOpCount = 0;
            try {
                buffer = Manager.ReadBlocks(currentRecordLBA, out bufferSize, SectorsPerCluster, buffer);
                readOpCount++;
                header = (NtfsFileRecordHeader*)buffer;
                if (FileRecordMarker != header->Ntfs.Type) {
                    // We expect a 'FILE' NTFS record here.
                    throw new NotImplementedException();
                }
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
                header = (NtfsFileRecordHeader*)((byte*)header + header->BytesAllocated);
                if (bufferSize <= ((byte*)header - buffer)) {
                    header = null;
                    currentRecordLBA += SectorsPerCluster;
                }
            }
            finally { if (null != buffer) { Marshal.FreeCoTaskMem((IntPtr)buffer); } }
        }

        private static readonly uint FileRecordMarker = 0x454C4946; // FILE
        private static readonly byte[] OEMID = Encoding.ASCII.GetBytes("NTFS    ");
        private ulong[] _metadataFileLBAs = new ulong[16];
        private Dictionary<string, ulong> _metadataFilesLBAByName = new Dictionary<string, ulong>();
    }
}
