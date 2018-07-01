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
    internal class NtfsPartition : GenericPartition
    {
        internal NtfsPartition(IntPtr handle, bool hidden, uint startSector, uint sectorCount)
            : base(handle, startSector, sectorCount)
        {
            if (0 >= handle.ToInt64()) { throw new ArgumentException(); }
            _privateHandle = handle;
            Hidden = hidden;
        }

        internal ulong this[string name]
        {
            get { return _metadataFilesLBAByName[name]; }
        }

        internal override uint BytesPerSector
        {
            get { return _bytesPerSector; }
        }

        internal ulong MFTEntrySize { get; private set; }

        internal ulong ClustersPerIndexBuffer { get; private set; }

        internal ulong ClusterSize
        {
            get { return SectorsPerCluster * BytesPerSector; }
        }

        internal static NtfsPartition Current { get; set; }

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
            ulong currentRecordLBA = (MFTClusterNumber * SectorsPerCluster);
            for (int mdfIndex = 0; mdfIndex < 16; mdfIndex++) {
                _metadataFileLBAs[mdfIndex] = currentRecordLBA;
                using (IPartitionClusterData clusterData = ReadSectors(currentRecordLBA, SectorsPerCluster)) {
                    byte* currentRecord = clusterData.Data;
                    NtfsFileRecord* header = (NtfsFileRecord*)currentRecord;
                    header->AssertRecordType();
                    header->ApplyFixups();
                    NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)header + header->AttributesOffset);
                    // Walk attributes. Technically this is useless. However that let us trace metafile names.
                    for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                        if (NtfsAttributeType.AttributeNone == currentAttribute->AttributeType) {
                            break;
                        }
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
                                : currentAttribute->GetTotalSize()));
                    }
                    currentRecordLBA += header->BytesAllocated / BytesPerSector;
                }
            }
        }

        internal unsafe ulong CountFiles()
        {
            NtfsNonResidentAttribute* dataAttribute =
                (NtfsNonResidentAttribute*)_mft.RecordBase->GetAttribute(NtfsAttributeType.AttributeData);
            dataAttribute->AssertNonResident();
            if (null == dataAttribute) {
                throw new ApplicationException();
            }
            NtfsPartition partition = NtfsPartition.Current;
            ulong clusterSize = partition.ClusterSize;
            ulong mftRecordPerCluster = clusterSize / partition.MFTEntrySize;
            ulong sectorsPerMFTRecord = partition.MFTEntrySize / partition.BytesPerSector;
            if (FeaturesContext.InvariantChecksEnabled) {
                if (0 != (clusterSize % partition.MFTEntrySize)) {
                    throw new ApplicationException();
                }
                if (0 != (partition.MFTEntrySize % partition.BytesPerSector)) {
                    throw new ApplicationException();
                }
            }
            ulong recordsPerCluster = clusterSize / NtfsFileRecord.RECORD_SIZE;
            byte[] localBuffer = new byte[clusterSize];
            Stream mftDataStream = dataAttribute->OpenDataStream();
            try {
                NtfsBitmapAttribute* bitmap = (NtfsBitmapAttribute*)_mft.RecordBase->GetAttribute(NtfsAttributeType.AttributeBitmap);
                if (null == bitmap) { throw new AssertionException("Didn't find the $MFT bitmap attribute."); }
                IEnumerator<bool> bitmapEnumerator = bitmap->GetContentEnumerator();
                ulong result = 0;
                while (bitmapEnumerator.MoveNext()) {
                    if (!bitmapEnumerator.Current) {
                        continue;
                    }
                    result++;
                }
                if (null != mftDataStream) { mftDataStream.Close(); }
                return result;
            }
            catch {
                if (null != mftDataStream) { mftDataStream.Close(); }
                throw;
            }
        }

        internal unsafe void DumpFirstFileNames()
        {
            // Start at $MFT LBA.
            ulong currentRecordLBA = (MFTClusterNumber * SectorsPerCluster);
            byte* buffer = null;
            IPartitionClusterData clusterData = null;
            try {
                ulong clusterSize = PartitionManager.Singleton.Geometry.BytesPerSector * SectorsPerCluster;
                uint bufferSize = 0;
                NtfsFileRecord* header = null;
                uint readOpCount = 0;
                for (int fileIndex = 0; fileIndex < 1024; fileIndex++) {
                    if (null == header) {
                        if (null != clusterData) {
                            clusterData.Dispose();
                        }
                        clusterData = ReadSectors(currentRecordLBA, SectorsPerCluster);
                        if (null == clusterData) {
                            throw new ApplicationException();
                        }
                        buffer = clusterData.Data;
                        bufferSize = clusterData.DataSize;
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
            finally {
                if (null != clusterData) {
                    clusterData.Dispose();
                }
            }
        }

        internal unsafe void EnumerateRecordAttributes(ulong recordLBA, ref byte* buffer,
            RecordAttributeEnumeratorCallbackDelegate callback)
        {
            using (IPartitionClusterData clusterData = ReadSectors(recordLBA)) {
                buffer = clusterData.Data;
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
            }
            return;
        }

        protected override IPartitionClusterData GetClusterBufferChain(uint minimumSize = 0)
        {
            bool nonPooled = false;
            uint unitSize = (uint)ClusterSize;
            int unitsCount;
            if (0 == unitSize) {
                unitSize = 16 * 1024;
                unitsCount = 1;
                nonPooled = true;
            }
            else {
                unitsCount = (int)(1 + ((minimumSize - 1) / unitSize));
            }
            // We need to provide an explicit size because the cluster size is not yet known and the buffer
            // allocation would fail.
            return _PartitionClusterData.CreateFromPool(unitSize, unitsCount, nonPooled);
        }

        internal unsafe void InterpretBootSector()
        {
            using(IPartitionClusterData clusterData = ReadSectors(0)) {
                byte* sector = clusterData.Data;
                if (0x55 != sector[510]) { throw new ApplicationException(); }
                if (0xAA != sector[511]) { throw new ApplicationException(); }
                byte* sectorPosition = sector + 3; // Skip jump instruction.
                // Verify OEMID field conformance.
                int OEMIDlength = Constants.OEMID.Length;
                for(int index = 0; index < OEMIDlength; index++) {
                    if (*(sectorPosition++) != Constants.OEMID[index]) {
                        throw new ApplicationException();
                    }
                }
                _bytesPerSector = *(ushort*)(sectorPosition); sectorPosition += sizeof(ushort);
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
                MFTEntrySize = (0 < rawClusteringValue)
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
            IPartitionClusterData clusterData = null;
            uint bufferSize = 0;
            ulong clusterSize = PartitionManager.Singleton.Geometry.BytesPerSector * SectorsPerCluster;
            ulong currentRecordLBA =
                _metadataFileLBAs[(int)NtfsWellKnownMetadataFiles.Bitmap];
            NtfsFileRecord* header = null;
            uint readOpCount = 0;
            try {
                clusterData = ReadSectors(currentRecordLBA, SectorsPerCluster);
                if (null == clusterData) {
                    throw new ApplicationException();
                }
                buffer = clusterData.Data;
                bufferSize = clusterData.DataSize;
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
            finally {
                if (null != clusterData) {
                    clusterData.Dispose();
                }
            }
        }

        internal unsafe void UpdateBadClustersMap()
        {
            byte* buffer = null;
            IPartitionClusterData clusterData = null;
            uint bufferSize = 0;
            ulong clusterSize = PartitionManager.Singleton.Geometry.BytesPerSector * SectorsPerCluster;
            ulong currentRecordLBA =
                _metadataFileLBAs[(int)NtfsWellKnownMetadataFiles.BadClusters];
            NtfsFileRecord* header = null;
            uint readOpCount = 0;
            try {
                clusterData = ReadSectors(currentRecordLBA, SectorsPerCluster);
                buffer = clusterData.Data;
                bufferSize = clusterData.DataSize;
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
                    if ((null != nonResident) && ("$Bad" == nonResident->Header.Name)) {
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
            finally {
                if (null != clusterData) {
                    clusterData.Dispose();
                }
            }
        }

        private uint _bytesPerSector;
        private ulong[] _metadataFileLBAs = new ulong[16];
        private NtfsMFTFileRecord _mft;
        private Dictionary<string, ulong> _metadataFilesLBAByName = new Dictionary<string, ulong>();
        private static List<IntPtr> _partitionClusterDataFreePool = new List<IntPtr>();
        private static Dictionary<_PartitionClusterData, int> _partitionClusterDataUsedPool =
            new Dictionary<_PartitionClusterData, int>();
        private IntPtr _privateHandle;

        private class _PartitionClusterData : IPartitionClusterData
        {
            private unsafe _PartitionClusterData(byte* rawData, ulong size, bool nonPooled,
                _PartitionClusterData chainTo = null)
            {
                if (null == rawData) { throw new ArgumentNullException(); }
                if (uint.MaxValue < size) { throw new ArgumentOutOfRangeException(); }
                _rawData = rawData;
                _dataSize = (uint)size;
                NonPooled = nonPooled;
                if (null != chainTo) {
                    chainTo.NextInChain = this;
                }
            }

            public unsafe byte* Data => _rawData;

            public uint DataSize => _dataSize;

            private bool NonPooled { get; set; }

            public IPartitionClusterData NextInChain { get; private set; }

            public unsafe void BinaryDump()
            {
                Console.WriteLine("-------- CLUSTER DATA -----------");
                Helpers.BinaryDump(_rawData, _dataSize);
                Console.WriteLine();
            }

            public void BinaryDumpChain()
            {
                for(_PartitionClusterData item = this; null != item; item = (_PartitionClusterData)item.NextInChain) {
                    item.BinaryDump();
                }
            }

            internal static unsafe _PartitionClusterData CreateFromPool(ulong unitSize, int unitsCount, bool nonPooled)
            {
                if (0 == unitSize) { throw new ArgumentException(); }
                if (0 >= unitsCount) { throw new ArgumentException(); }
                if (int.MaxValue < unitSize) { throw new ArgumentOutOfRangeException(); }
                if (nonPooled && (1 != unitsCount)) {
                    throw new NotSupportedException();
                }
                List<IntPtr> pool = NtfsPartition._partitionClusterDataFreePool;
                lock (pool) {
                    byte* rawBuffer = null;
                    int poolCount = pool.Count;
                    IntPtr managedBuffer;
                    _PartitionClusterData result = null;
                    _PartitionClusterData resultChainTail = null;
                    for (int index= 0; index < unitsCount; index++) {
                        if (nonPooled || (0 == poolCount)) {
                            managedBuffer = Marshal.AllocCoTaskMem((int)unitSize);
                        }
                        else {
                            int electedIndex = poolCount - 1;
                            managedBuffer = pool[electedIndex];
                            if (FeaturesContext.DataPoolChecksEnabled) {
                                Console.WriteLine("Reusing pointer 0x{0:X8} from pool index {1}",
                                    managedBuffer.ToInt64(), electedIndex);
                            }
                            pool.RemoveAt(electedIndex);
                            poolCount--;
                        }
                        rawBuffer = (byte*)managedBuffer.ToPointer();
                        resultChainTail = new _PartitionClusterData(rawBuffer, unitSize, nonPooled, resultChainTail);
                        if (null == result) {
                            result = resultChainTail;
                        }
                        _partitionClusterDataUsedPool.Add(resultChainTail, 0);
                    }
                    return result;
                }
            }

            public unsafe void Dispose()
            {
                lock (_partitionClusterDataFreePool) {
                    for (_PartitionClusterData disposed = this; null != disposed; disposed = (_PartitionClusterData)disposed.NextInChain) {
                        _partitionClusterDataUsedPool.Remove(disposed);
                        if (!NonPooled) {
                            if (null == disposed._rawData) {
                                throw new ApplicationException();
                            }
                            IntPtr managedBuffer = new IntPtr(disposed._rawData);
                            NtfsPartition._partitionClusterDataFreePool.Add(managedBuffer);
                            if (FeaturesContext.DataPoolChecksEnabled) {
                                Console.WriteLine("Returning pointer 0x{0:X8} to pool",
                                    managedBuffer.ToInt64());
                            }
                        }
                        disposed._rawData = null;
                    }
                }
            }

            internal uint _dataSize;
            private unsafe byte* _rawData;
        }
    }
}
