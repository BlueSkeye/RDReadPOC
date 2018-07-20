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
        private delegate bool BitmapWalkerDelegate(ulong recordIndex);

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
        
        private static void AssertFindDirectoryItems(string[] pathItems)
        {
            foreach(string item in pathItems) {
                if (string.IsNullOrEmpty(item)) {
                    throw new ArgumentException("Ill formed path.");
                }
            }
            return;
        }
        
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
                            NtfsFileNameAttribute* nameAttribute = (NtfsFileNameAttribute*)(currentAttribute->GetValue());
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
                                : currentAttribute->GetResidentSize()));
                    }
                    currentRecordLBA += header->BytesAllocated / BytesPerSector;
                }
            }
        }

        /// <summary>Count files in current partition. We walk the bitmap and find which records are used.
        /// Each used record is counted as a file.</summary>
        /// <returns></returns>
        internal unsafe ulong CountFiles()
        {
            ulong result = 0;
            WalkUsedRecord(delegate (ulong recordIndex) {
                result++;
                return true;
            });
            return result;
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
                            NtfsFileNameAttribute* nameAttribute = (NtfsFileNameAttribute*)(currentAttribute->GetValue());
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

        /// <summary> Find the <see cref="NtfsRecord"/> matching the given path items.</summary>
        /// <param name="childName">Searched item name.</param>
        /// <returns></returns>
        private unsafe NtfsRecord* _FindChildItem(NtfsFileRecord* fromRecord, string childName,
            out IPartitionClusterData clusterData)
        {
            fromRecord->Dump();
            fromRecord->EnumerateRecordAttributes(delegate (NtfsAttribute* attribute) {
                attribute->Dump();
                return true;
            });
            ulong currentRecordLBA = _metadataFileLBAs[(int)NtfsWellKnownMetadataFiles.Root];
            clusterData = null;
            NtfsRecord* result = null;
            try {
                NtfsResidentAttribute* rootIndexAttributeHeader =
                    (NtfsResidentAttribute*)(fromRecord->GetAttribute(NtfsAttributeType.AttributeIndexRoot));
                if (null == rootIndexAttributeHeader) {
                    throw new ApplicationException();
                }
                if (FeaturesContext.InvariantChecksEnabled) {
                    rootIndexAttributeHeader->AssertResident();
                }
                NtfsRootIndexAttribute* rootIndexAttribute =
                    (NtfsRootIndexAttribute*)(rootIndexAttributeHeader->GetValue());
                rootIndexAttributeHeader->Dump();
                rootIndexAttribute->Dump();
                ulong indexAllocationVCN = ulong.MaxValue;
                rootIndexAttribute->EnumerateIndexEntries(delegate (NtfsDirectoryIndexEntry* scannedEntry) {
                    string name = scannedEntry->Name;
                    if ((null == name) && !scannedEntry->GenericEntry.LastIndexEntry) {
                        throw new ApplicationException("Unamed index entry found.");
                    }
                    // TODO : Account for sort order. 
                    switch (string.Compare(childName, name)) {
                        case -1:
                            indexAllocationVCN = scannedEntry->ChildNodeVCN;
                            return false;
                        case 0:
                            // Don't know yet how to retrieve an item that is located in teh index_root
                            // attribute.
                            throw new NotImplementedException();
                        case 1:
                            return true;
                        default:
                            throw new ApplicationException();
                    }
                });
                if (ulong.MaxValue == indexAllocationVCN) {
                    return null;
                }
                // Continue with an index allocation entry.
                NtfsIndexAllocationAttribute* indexAllocationAttribute = null;
                NtfsNonResidentAttribute * indexAllocationAttributeHeader =
                    (NtfsNonResidentAttribute*)(fromRecord->GetAttribute(NtfsAttributeType.AttributeIndexAllocation));
                // The Index allocation attribute may be missing.
                if (null != indexAllocationAttributeHeader) {
                    if (FeaturesContext.InvariantChecksEnabled) {
                        indexAllocationAttributeHeader->AssertNonResident();
                    }
                    indexAllocationAttributeHeader->Dump();
                    using (IClusterStream dataStream = indexAllocationAttributeHeader->OpenDataClusterStream()) {
                        while (true) {
                            IPartitionClusterData rawData = dataStream.ReadNextCluster();

                            if (null == rawData) {
                                break;
                            }
                            byte* basePtr = rawData.Data;
                            byte* rawPtr = basePtr;
                            NtfsRecord* record = (NtfsRecord*)rawPtr;
                            record->ApplyFixups();
                            rawPtr += sizeof(NtfsRecord);
                            ulong blockVCN = *((ulong*)rawPtr);
                            rawPtr += sizeof(ulong);
                            NtfsNodeHeader* nodeHeader = (NtfsNodeHeader*)rawPtr;
                            rawPtr += sizeof(NtfsNodeHeader);
                            nodeHeader->Dump();
                            for(uint currentOffset = nodeHeader->OffsetToFirstIndexEntry;
                                currentOffset < nodeHeader->OffsetToEndOfIndexEntries;
                                )
                            {
                                NtfsIndexEntry* currentEntry = (NtfsIndexEntry*)(basePtr + currentOffset);
                                continue;
                            }
                            Helpers.BinaryDump(rawData.Data, 256);
                        }
                        throw new NotImplementedException();
                    }
                }
                throw new NotImplementedException();

                NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)fromRecord + fromRecord->AttributesOffset);
                // Walk attributes.
                for (int attributeIndex = 0; attributeIndex < fromRecord->NextAttributeNumber; attributeIndex++) {
                    if (fromRecord->BytesInUse < ((byte*)currentAttribute - (byte*)fromRecord)) { break; }
                    if (NtfsAttributeType.AttributeNone == currentAttribute->AttributeType) { break; }
                    NtfsNonResidentAttribute* nonResident = (0 == currentAttribute->Nonresident)
                        ? null
                        : (NtfsNonResidentAttribute*)currentAttribute;
                    if (null != nonResident) {
                        nonResident->DecodeRunArray();
                    }
                    currentAttribute = (NtfsAttribute*)((byte*)currentAttribute + currentAttribute->Length);
                }
                //rootRecord = (NtfsFileRecord*)((byte*)rootRecord + rootRecord->BytesAllocated);
                //if (bufferSize <= ((byte*)rootRecord - buffer)) {
                //    rootRecord = null;
                //    currentRecordLBA += SectorsPerCluster;
                //}
            }
            finally {
                if ((null == result) && (null != clusterData)) {
                    clusterData.Dispose();
                }
            }
            throw new NotImplementedException();
        }

        /// <summary>Find a file using it's partition relative path.</summary>
        /// <param name="partitionPath">A path without the drive letter. The leading antislash is optional.
        /// </param>
        /// <returns>An <see cref="NtfsRecord"/> for the file or a null reference if not found.</returns>
        internal unsafe NtfsFileRecord* FindFile(string partitionPath)
        {
            if (string.IsNullOrEmpty(partitionPath)) {
                throw new ArgumentNullException();
            }
            if (PathSeparator == partitionPath[0]) {
                if (1 == partitionPath.Length) {
                    throw new ArgumentException();
                }
                partitionPath = partitionPath.Substring(1);
            }
            // The Path class is relative to the current directory which may or may not be relevant to the
            // currently scanned partition. Hence, we have to implement our own path management code.
            string[] pathItems = partitionPath.Split(PathSeparator);
            int itemsCount = pathItems.Length;
            int directoriesCount = pathItems.Length - 1;
            IPartitionClusterData clusterData = null;
            IPartitionClusterData previousClusterData = null;
            NtfsFileRecord* currentRecord = null;
            ulong currentRecordLBA = _metadataFileLBAs[(int)NtfsWellKnownMetadataFiles.Root];
            // Read the root record.
            clusterData = ReadSectors(currentRecordLBA, SectorsPerCluster);
            if (null == clusterData) {
                throw new ApplicationException();
            }
            byte* buffer = clusterData.Data;
            uint bufferSize = clusterData.DataSize;
            currentRecord = (NtfsFileRecord*)buffer;
            for (int index = 0; index < itemsCount; index++) {
                NtfsFileRecord* previousRecord = currentRecord;
                try {
                    previousClusterData = clusterData;
                    currentRecord = (NtfsFileRecord*)_FindChildItem(previousRecord, pathItems[index],
                        out clusterData);
                    if (null == currentRecord) {
                        throw new ApplicationException("Not found");
                    }
                }
                finally {
                    if (null != previousClusterData) {
                        previousClusterData.Dispose();
                    }
                }
            }
            return currentRecord;
        }

        internal unsafe IPartitionClusterData GetCluster(ulong clusterNumber)
        {
            return ReadSectors(clusterNumber * SectorsPerCluster, SectorsPerCluster);
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

        private unsafe void WalkUsedRecord(BitmapWalkerDelegate callback)
        {
            NtfsNonResidentAttribute* dataAttribute =
                (NtfsNonResidentAttribute*)_mft.RecordBase->GetAttribute(NtfsAttributeType.AttributeData);
            dataAttribute->AssertNonResident();
            if (null == dataAttribute) {
                throw new ApplicationException();
            }
            ulong clusterSize = this.ClusterSize;
            ulong mftRecordPerCluster = clusterSize / this.MFTEntrySize;
            ulong sectorsPerMFTRecord = this.MFTEntrySize / this.BytesPerSector;
            if (FeaturesContext.InvariantChecksEnabled) {
                if (0 != (clusterSize % this.MFTEntrySize)) {
                    throw new ApplicationException();
                }
                if (0 != (this.MFTEntrySize % this.BytesPerSector)) {
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
                ulong recordIndex = 0;
                while (bitmapEnumerator.MoveNext()) {
                    recordIndex++;
                    if (!bitmapEnumerator.Current) {
                        continue;
                    }
                    if (!callback(recordIndex)) {
                        return;
                    }
                }
                return;
            }
            catch {
                throw;
            }
            finally {
                if (null != mftDataStream) { mftDataStream.Close(); }
            }
        }

        /// <summary>This program is for use on Windows only</summary>
        private const char PathSeparator = '\\';
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
