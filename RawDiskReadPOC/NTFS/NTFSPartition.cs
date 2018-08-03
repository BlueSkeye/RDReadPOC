using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using RawDiskReadPOC.NTFS.Indexing;

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

        /// <summary>Mft record size in clusters.</summary>
        internal ulong MFTEntrySize { get; private set; }

        /// <summary>Index block size in clusters.</summary>
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

        /// <summary>Cluster location of mft data.</summary>
        internal ulong MFTClusterNumber { get; private set; }

        /// <summary>Cluster location of copy of mft.</summary>
        internal ulong MFTMirrorClusterNumber { get; private set; }

        /// <summary>Size of a cluster in sectors.</summary>
        internal uint SectorsPerCluster { get; private set; }

        internal uint SectorsPerTrack { get; private set; }

        /// <summary>Number of sectors in volume. Gives maximum volume size of 2^63 sectors.
        /// Assuming standard sector size of 512 bytes, the maximum byte size is approx.
        /// 4.7x10 ^ 21 bytes.</summary>
        internal ulong TotalSectorsCount { get; private set; }

        /// <summary>Irrelevant (serial number).</summary>
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
            LoadVCNList();
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

        private static unsafe void DebugVCNCluster(IPartitionClusterData candidate)
        {
            byte* rawPtr = candidate.Data;
            NtfsRecord* record = (NtfsRecord*)rawPtr;
            record->ApplyFixups();
            rawPtr += sizeof(NtfsRecord);
            ulong blockVCN = *((ulong*)rawPtr);
            rawPtr += sizeof(ulong);
            NtfsNodeHeader* pNodeHeader = (NtfsNodeHeader*)rawPtr;
            NtfsIndexEntryHeader* currentEntry = null;
            for (uint currentOffset = pNodeHeader->OffsetToFirstIndexEntry;
                currentOffset < pNodeHeader->IndexLength;
                currentOffset += currentEntry->EntryLength)
            {
                currentEntry = (NtfsIndexEntryHeader*)(((byte*)pNodeHeader) + currentOffset);
                NtfsFileNameAttribute* fileName =
                    (NtfsFileNameAttribute*)(((byte*)pNodeHeader) + currentOffset + sizeof(NtfsIndexEntryHeader));
                Console.WriteLine(fileName->GetName());
            }
        }

        /// <summary>Dump the content of the $BadClus file.</summary>
        internal unsafe void DumpBadClusters()
        {
            ulong currentRecordLBA =
                _metadataFileLBAs[(int)NtfsWellKnownMetadataFiles.BadClusters];
            NtfsFileRecord* header = null;
            uint readOpCount = 0;
            IPartitionClusterData clusterData = null;
            try {
                clusterData = ReadSectors(currentRecordLBA, SectorsPerCluster);
                NtfsFileRecord* fileRecord = (NtfsFileRecord*)(clusterData.Data);
                fileRecord->AssertRecordType();
                NtfsFileNameAttribute* fileName =
                    (NtfsFileNameAttribute*)(fileRecord->GetAttribute(NtfsAttributeType.AttributeFileName));
                if (null == fileName) {
                    throw new ApplicationException();
                }
                // First $DATA attribute is expected to be empty.
                NtfsAttribute* dataAttribute = fileRecord->GetAttribute(NtfsAttributeType.AttributeData, 1);
                if (null == dataAttribute) {
                    throw new ApplicationException();
                }
                // TODO : Understand what this data attribute is for.
                // dataAttribute->BinaryDump();

                // Second $DATA attribute is the real data.
                dataAttribute = fileRecord->GetAttribute(NtfsAttributeType.AttributeData, 2);
                if (null == dataAttribute) {
                    throw new ApplicationException();
                }
                // It must be non-resident.
                if (dataAttribute->IsResident) {
                    throw new NotSupportedException();
                }

                // TODO : Are we sure the $Bad data attribute is always the second one ?
                NtfsNonResidentAttribute* nrDataAttribute = (NtfsNonResidentAttribute*)dataAttribute;
                string name = nrDataAttribute->Header.Name;
                if ("$Bad" != name) {
                    throw new ApplicationException();
                }
                bool badClusterFound = false;
                using (IClusterStream stream = (IClusterStream)nrDataAttribute->OpenDataClusterStream()) {
                    while(true) {
                        stream.SeekToNextNonEmptyCluster();
                        long position = ((Stream)stream).Position;
                        using (IPartitionClusterData badClusterData = stream.ReadNextCluster()) {
                            if (null == badClusterData) { break; }
                            badClusterFound = true;
                            Console.WriteLine("Bad cluster #{0}",
                                (position / (BytesPerSector * SectorsPerCluster)));
                        }
                    }
                }
                if (!badClusterFound) {
                    Console.WriteLine("No bad cluster found.");
                }
                return;
            }
            finally {
                if (null != clusterData) {
                    clusterData.Dispose();
                }
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
        /// <param name="searchedName">Searched item name.</param>
        /// <returns></returns>
        private unsafe NtfsIndexEntryHeader* _FindChildItem(NtfsFileRecord* fromRecord, string searchedName,
            out IPartitionClusterData clusterData)
        {
            if (FeaturesContext.FindFileAlgorithmTrace) {
                fromRecord->Dump();
                fromRecord->EnumerateRecordAttributes(delegate (NtfsAttribute* attribute) {
                    attribute->Dump();
                    return true;
                });
            }
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
                if (FeaturesContext.FindFileAlgorithmTrace) {
                    rootIndexAttributeHeader->Dump();
                    rootIndexAttribute->Dump();
                }
                ulong indexAllocationVCN = ulong.MaxValue;
                NtfsIndexEntryHeader* match = null;
                rootIndexAttribute->EnumerateIndexEntries(delegate (NtfsIndexEntryHeader* scannedEntry) {
                    string candidateName = ((NtfsIndexedFileNameAttribute*)scannedEntry)->Name;
                    bool lastEntry = scannedEntry->LastIndexEntry;
                    if (lastEntry) {
                        indexAllocationVCN = scannedEntry->ChildNodeVCN;
                        return false;
                    }
                    if (string.IsNullOrEmpty(candidateName)) {
                        throw new ApplicationException("Unamed index entry found.");
                    }
                    // TODO : Account for sort order. 
                    if (FeaturesContext.FindFileAlgorithmTrace) {
                        Console.WriteLine("RI name '{0}'", candidateName);
                    }
                    switch (string.Compare(searchedName, candidateName)) {
                        case -1:
                            indexAllocationVCN = scannedEntry->ChildNodeVCN;
                            if (FeaturesContext.FindFileAlgorithmTrace) {
                                Console.WriteLine("Found");
                            }
                            return false;
                        case 0:
                            match = (NtfsIndexEntryHeader*)scannedEntry;
                            return false;
                        case 1:
                            if (FeaturesContext.FindFileAlgorithmTrace) {
                                Console.WriteLine("Continue");
                            }
                            return true;
                        default:
                            throw new ApplicationException();
                    }
                }, FeaturesContext.FindFileAlgorithmTrace);
                if (null != match) {
                    // An exact match was found. No need to dive further down the tree.
                    return match;
                }
                if (ulong.MaxValue == indexAllocationVCN) {
                    return null;
                }
                // We found the index allocation entry that might contain the searched entry.
                NtfsIndexAllocationAttribute* indexAllocationAttribute = null;
                // The index allocation attribute holds those files that are "after" the last root
                // index entry according to the sort order.
                NtfsNonResidentAttribute * indexAllocationAttributeHeader =
                    (NtfsNonResidentAttribute*)(fromRecord->GetAttribute(NtfsAttributeType.AttributeIndexAllocation));
                // The Index allocation attribute may be missing. 
                if (null == indexAllocationAttributeHeader) {
                    throw new ApplicationException("TODO : missed something here.");
                }
                if (FeaturesContext.InvariantChecksEnabled) {
                    indexAllocationAttributeHeader->AssertNonResident();
                }
                if (FeaturesContext.FindFileAlgorithmTrace) {
                    indexAllocationAttributeHeader->Dump();
                }
                // Get attribute content and scaan 
                using (IClusterStream dataStream = (IClusterStream)indexAllocationAttributeHeader->OpenDataClusterStream()) {
                    IPartitionClusterData rawData = null;
                    // TODO : Change this. We should be able to seek along the data stream without reading
                    // previous clusters.
                    for (ulong clusterIndex = 0; clusterIndex <= indexAllocationVCN; clusterIndex++) {
                        rawData = dataStream.ReadNextCluster();
                        if (null == rawData) {
                            throw new ApplicationException();
                        }
                        if (FeaturesContext.FindFileAlgorithmTrace) {
                            DebugVCNCluster(rawData);
                        }
                    }
                    byte* rawPtr = rawData.Data;
                    NtfsRecord* record = (NtfsRecord*)rawPtr;
                    record->ApplyFixups();
                    rawPtr += sizeof(NtfsRecord);
                    ulong blockVCN = *((ulong*)rawPtr);
                    rawPtr += sizeof(ulong);
                    NtfsNodeHeader* pNodeHeader = (NtfsNodeHeader*)rawPtr;
                    if (FeaturesContext.FindFileAlgorithmTrace) {
                        pNodeHeader->Dump();
                    }
                    NtfsIndexEntryHeader* currentEntry = null;
                    for (uint currentOffset = pNodeHeader->OffsetToFirstIndexEntry;
                        currentOffset < pNodeHeader->IndexLength;
                        currentOffset += currentEntry->EntryLength)
                    {
                        currentEntry = (NtfsIndexEntryHeader*)(((byte*)pNodeHeader) + currentOffset);
                        NtfsFileNameAttribute* fileName =
                            (NtfsFileNameAttribute*)(((byte*)pNodeHeader) + currentOffset + sizeof(NtfsIndexEntryHeader));
                        string name = fileName->GetName();
                        if (searchedName == name) {
                            return currentEntry;
                        }
                        if (FeaturesContext.FindFileAlgorithmTrace) {
                            Console.WriteLine(name);
                        }
                    }
                    // Helpers.BinaryDump(rawData.Data, 256);
                    // Didn't found the expected record. Return a null reference.
                    return null;
                }
                throw new ApplicationException("UNREACHABLE");
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
        internal unsafe NtfsIndexEntryHeader* FindFile(string partitionPath,
            NtfsWellKnownMetadataFiles fromMetadatFile = NtfsWellKnownMetadataFiles.Root)
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
            ulong currentRecordLBA = _metadataFileLBAs[(int)fromMetadatFile];
            IPartitionClusterData clusterData = null;
            IPartitionClusterData previousClusterData = null;
            NtfsFileRecord* currentRecord = null;
            // Read the root record.
            clusterData = ReadSectors(currentRecordLBA, SectorsPerCluster);
            if (null == clusterData) {
                throw new ApplicationException();
            }
            byte* buffer = clusterData.Data;
            uint bufferSize = clusterData.DataSize;
            currentRecord = (NtfsFileRecord*)buffer;
            int leafItemIndex = itemsCount - 1;
            for (int index = 0; index < itemsCount; index++) {
                NtfsFileRecord* previousRecord = currentRecord;
                try {
                    previousClusterData = clusterData;
                    NtfsIndexEntryHeader* scannedEntry = _FindChildItem(previousRecord, pathItems[index], out clusterData);
                    if (null == scannedEntry) {
                        // Some part of the path is missing.
                        return null;
                    }
                    if (index == leafItemIndex) {
                        return scannedEntry;
                    }
                    currentRecord = GetFileRecord(scannedEntry->FileReference, out clusterData);
                    if (null == currentRecord) {
                        throw new ApplicationException();
                    }
                }
                finally {
                    if (null != previousClusterData) {
                        previousClusterData.Dispose();
                    }
                }
            }
            throw new ApplicationException("UNREACHABLE");
        }

        internal unsafe IPartitionClusterData GetCluster(ulong clusterNumber)
        {
            return ReadSectors(clusterNumber * SectorsPerCluster, SectorsPerCluster);
        }

        internal IPartitionClusterData GetBuffer(uint minimumSize = 0)
        {
            return GetClusterBufferChain(minimumSize);
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

        /// <summary>Returns a file record for one of the well known files or a null reference.
        /// On return fixups are already applied.</summary>
        /// <param name="wellKnownFile"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        internal unsafe NtfsFileRecord* GetFileRecord(NtfsWellKnownMetadataFiles wellKnownFile,
            out IPartitionClusterData data)
        {
            ulong recordIndex = _metadataFileLBAs[(int)wellKnownFile];
            data = GetCluster(recordIndex / SectorsPerCluster);
            if (null == data) {
                return null;
            }
            NtfsFileRecord* result = (NtfsFileRecord*)data.Data;
            result->ApplyFixups();
            return result;
        }

        /// <summary>
        /// On return fixups are already applied.</summary>
        /// <param name="referenceNumber"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        internal unsafe NtfsFileRecord* GetFileRecord(ulong referenceNumber, out IPartitionClusterData data)
        {
            ulong offset = (referenceNumber >> 48);
            ulong recordIndex = referenceNumber & 0xFFFFFFFFFFFF;
            ulong recordsPerCluster = ClusterSize / 1024;
            ulong relativeCluster = recordIndex / recordsPerCluster;
            relativeCluster = VCNtoLCN(relativeCluster);
            ulong clusterOffset = (recordIndex % recordsPerCluster) * 1024;
            data = GetCluster(relativeCluster);
            NtfsFileRecord* result = (NtfsFileRecord*)(data.Data + clusterOffset);
            result->ApplyFixups();
            return result;
        }

        internal unsafe void InterpretBootSector()
        {
            // The standard NTFS_BOOT_SECTOR is on sector 0 of the partition.
            // On NT4 and above there is one backup copy of the boot sector to be found on
            // the last sector of the partition(not normally accessible from within Windows
            // as the bootsector contained number of sectors value is one less than the
            // actual value!).
            // On versions of NT 3.51 and earlier, the backup copy was located at number of
            // sectors / 2(integer divide), i.e. in the middle of the volume.
            using (IPartitionClusterData clusterData = ReadSectors(0)) {
                byte* sector = clusterData.Data;
                if (0x55 != sector[510]) { throw new ApplicationException(); }
                if (0xAA != sector[511]) { throw new ApplicationException(); }
                byte* sectorPosition = sector + 3; // Skip jump instruction.
                // Verify OEMID field conformance.
                int OEMIDlength = Constants.OEMID.Length; // Magic "NTFS    ".
                for (int index = 0; index < OEMIDlength; index++) {
                    if (*(sectorPosition++) != Constants.OEMID[index]) {
                        throw new ApplicationException();
                    }
                }
                _bytesPerSector = *(ushort*)(sectorPosition); sectorPosition += sizeof(ushort);
                SectorsPerCluster = *(byte*)(sectorPosition++);
                sectorPosition += sizeof(ushort); // Unused : reserved sectors
                sectorPosition += 1; // Unused : fats (count?)
                sectorPosition += 2; // Unused: root entries
                sectorPosition += sizeof(ushort); // Unused : sectors
                MediaDescriptor = *(byte*)(sectorPosition++); // 0xF8 = hard disk
                sectorPosition += sizeof(ushort); // Unused :sectors per fat
                SectorsPerTrack = *(ushort*)(sectorPosition); sectorPosition += sizeof(ushort);
                HeadsCount = *(ushort*)(sectorPosition); sectorPosition += sizeof(ushort);
                HiddenSectorsCount = *(uint*)(sectorPosition); sectorPosition += sizeof(uint);
                sectorPosition += sizeof(uint); // Unused : large sectors
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
                sectorPosition += sizeof(uint); // Checksum
                if (0x54 != (sectorPosition - sector)) { throw new ApplicationException(); }
            }
        }

        private unsafe void LoadVCNList()
        {
            bool done = false;
            this._mft.EnumerateRecordAttributes(delegate (NtfsAttribute* attribute) {
                switch (attribute->AttributeType) {
                    case NtfsAttributeType.AttributeData:
                        done = true;
                        NtfsNonResidentAttribute* nonResident = (NtfsNonResidentAttribute*)attribute;
                        _chunks = nonResident->DecodeRunArray();
                        return false;
                    default:
                        return true;
                }
            });
            if (!done) {
                throw new ApplicationException();
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

        internal unsafe void TraceMFT()
        {
            this.MFT.RecordBase->EnumerateRecordAttributes(
                delegate (NtfsAttribute* attribute) {
                    Console.WriteLine("{0} {1}",
                        attribute->AttributeType, (0 == attribute->Nonresident) ? "Re" : "NR");
                    if (0 != attribute->Nonresident)
                    {
                        NtfsNonResidentAttribute* nonResident = (NtfsNonResidentAttribute*)attribute;
                        Console.WriteLine("\tA={0}, D={1}, I={2}", nonResident->AllocatedSize,
                            nonResident->DataSize, nonResident->InitializedSize);
                    }
                    return true;
                });
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

        private ulong VCNtoLCN(ulong virtualClusterNumber)
        {
            ulong cumulatedClustersCount = 0;
            foreach(NtfsNonResidentAttribute.LogicalChunk mapping in _chunks) {
                if ((cumulatedClustersCount + mapping.ClustersCount - 1) < virtualClusterNumber) {
                    cumulatedClustersCount += mapping.ClustersCount;
                    continue;
                }
                return mapping.FirstLogicalClusterNumber + (virtualClusterNumber - cumulatedClustersCount);
            }
            throw new ApplicationException();
        }

        private unsafe void WalkUsedRecord(BitmapWalkerDelegate callback)
        {
            NtfsNonResidentAttribute* dataAttribute =
                (NtfsNonResidentAttribute*)_mft.RecordBase->GetAttribute(NtfsAttributeType.AttributeData);
            if (null == dataAttribute) {
                throw new ApplicationException();
            }
            dataAttribute->AssertNonResident();
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
        /// <summary>Size of a sector in bytes.</summary>
        private uint _bytesPerSector;
        private List<NtfsNonResidentAttribute.LogicalChunk> _chunks;
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

            public unsafe IPartitionClusterData Zeroize()
            {
                Helpers.Zeroize(_rawData, _dataSize);
                return this;
            }

            internal uint _dataSize;
            private unsafe byte* _rawData;
        }
    }
}
