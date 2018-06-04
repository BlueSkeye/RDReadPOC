using System;
using System.Collections.Generic;
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

        internal ulong this[string name]
        {
            get { return _metadataFilesByName[name]; }
        }

        internal uint BytesPerSector { get; private set; }

        internal ulong ClustersPerFileRecordSegment { get; private set; }

        internal ulong ClustersPerIndexBuffer { get; private set; }

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
                    _metadataFilePointers[mdfIndex] = currentRecordLBA;
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
                            _metadataFilesByName.Add(metadataFileName, currentRecordLBA);
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
            ulong currentRecordLBA = StartSector + (MFTClusterNumber * SectorsPerCluster);
            byte* currentRecord = null;
            ulong result = 0;
            try {
                for (int mdfIndex = 0; mdfIndex < 16; mdfIndex++) {
                    _metadataFilePointers[mdfIndex] = currentRecordLBA;
                    currentRecord = Manager.Read(currentRecordLBA, SectorsPerCluster, currentRecord);
                    if (FileRecordMarker != *((uint*)currentRecord)) {
                        // We expect a 'FILE' NTFS record here.
                        throw new NotImplementedException();
                    }
                    NtfsFileRecordHeader* header = (NtfsFileRecordHeader*)currentRecord;
                    if (1024 < header->BytesAllocated) {
                        throw new NotImplementedException();
                    }
                    currentRecordLBA += header->BytesAllocated / BytesPerSector;
                    result++;
                }
                return result;
            }
            finally { if (null != currentRecord) { Marshal.FreeCoTaskMem((IntPtr)currentRecord); } }
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
                        buffer = Manager.Read(currentRecordLBA, out bufferSize, SectorsPerCluster, buffer);
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

        internal unsafe void UpdateBadClustersMap()
        {
            byte* buffer = null;
            uint bufferSize = 0;
            ulong clusterSize = Manager.Geometry.BytesPerSector * SectorsPerCluster;
            ulong currentRecordLBA =
                _metadataFilePointers[(int)NtfsWellKnownMetadataFiles.BadClusters];
            NtfsFileRecordHeader* header = null;
            uint readOpCount = 0;
            try {
                buffer = Manager.Read(currentRecordLBA, out bufferSize, SectorsPerCluster, buffer);
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
        private ulong[] _metadataFilePointers = new ulong[16];
        private Dictionary<string, ulong> _metadataFilesByName = new Dictionary<string, ulong>();
    }
}
