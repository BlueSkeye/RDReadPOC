using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Can be either resident or non-resident.
    /// Value consists of a sequence of variable length, 8-byte aligned, ATTR_LIST_ENTRY
    /// records.
    /// The list is not terminated by anything at all! The only way to know when the end is
    /// reached is to keep track of the current offset and compare it to the attribute value
    /// size.
    /// The attribute list attribute contains one entry for each attribute of the file in
    /// which the list is located, except for the list attribute itself. The list is sorted:
    /// first by attribute type, second by attribute name (if present), third by instance
    /// number. The extents of one non-resident attribute (if present) immediately follow
    /// after the initial extent. They are ordered by lowest_vcn and have their instace set
    /// to zero. It is not allowed to have two attributes with all sorting keys equal.
    /// Further restrictions:
    /// If not resident, the vcn to lcn mapping array has to fit inside the base mft record.
    /// The attribute list attribute value has a maximum size of 256kb. This is imposed by
    /// the Windows cache manager.
    /// Attribute lists are only used when the attributes of mft record do not fit inside
    /// the mft record despite all attributes (that can be made non-resident) having been
    /// made non-resident. This can happen e.g.when:
    /// - File has a large number of hard links (lots of filename attributes present).
    /// - The mapping pairs array of some non-resident attribute becomes so large due to
    ///   fragmentation that it overflows the mft record.
    /// - The security descriptor is very complex(not applicable to NTFS 3.0 volumes).
    /// - There are many named streams.</remarks>
    internal struct NtfsAttributeListAttribute
    {
        /// <summary>A delegate to be invoked for attribute data retrieval during the attribute
        /// enumeration process.</summary>
        /// <param name="attribute">The attribute (with data) to be handled. The data includes either just
        /// the header or full data depending on the value of the input value of includeData parameter.</param>
        /// <param name="includeData"></param>
        /// <param name="retry">On return, if this value is true, the same delegate will be invoked again
        /// for the same attribute. This is usefull for anattribute that may have a lot of data, such as
        /// the $J attribute of the $UsnJrnl where the method invoking the enumeration wants to quickly
        /// find the relevant attribute just using attribute header data, then once found have a closer
        /// look at the attribute data. This can be easily performed by being called back with just the
        /// attribute header first, then setting the retry parameter and switching the includeData
        /// parameter from false to true.</param>
        /// <returns>True if enumeration should continue, false otherwise.</returns>
        internal unsafe delegate bool EntryDataCallbackDelegate(NtfsAttribute* attribute, ref bool includeData,
            out bool retry);

        /// <summary>The delegate to be invoked by the entry enumerator method.</summary>
        /// <param name="entry">An enumerated entry. Each attribute will be enumerated once only, even
        /// if it span several entries. The enumeration will occur on entry having LowVcn = 0.</param>
        /// <param name="dataRetrievalCallback">On return, if this parameter is not a null reference, it
        /// is a delegate to be invoked eirher with the <see cref="NtfsAttribute"/> header only or with
        /// full attribute data depending on the value of includeFullData</param>
        /// <param name="includeFullData">The value of this parameter on delegate invocation return is
        /// meaningfull iif the dataRetrievalCallback is not a null reference. This boolean value is true
        /// if the dataRetrievalCallback is to be invoked with full attribute data. Otherwise the only
        /// the attribute header part will be provided.</param>
        /// <returns></returns>
        internal unsafe delegate bool EntryEnumeratorCallbackDelegate(ListEntry* entry,
            out EntryDataCallbackDelegate dataRetrievalCallback, out bool includeFullData);

        internal static unsafe void BinaryDump(NtfsAttribute* from)
        {
            _Dump(from, delegate (ListEntry* entry) { entry->BinaryDump(); });
        }

        internal static unsafe void Dump(NtfsAttribute* from)
        {
            _Dump(from, delegate(ListEntry* entry) { entry->Dump(); });
        }

        private unsafe delegate void DumpCallbackDelegate(ListEntry* entry);

        private static unsafe void _Dump(NtfsAttribute* from, DumpCallbackDelegate callback)
        {
            if(null == from) {
                throw new ArgumentNullException();
            }
            if (NtfsAttributeType.AttributeAttributeList != from->AttributeType) {
                throw new ArgumentException();
            }
            IPartitionClusterData disposableData = null;
            ListEntry* listBase = null;
            uint listLength;
            try {
                if (from->IsResident) {
                    NtfsResidentAttribute* listAttribute = (NtfsResidentAttribute*)from;
                    listBase = (ListEntry*)((byte*)from + listAttribute->ValueOffset);
                    listLength = listAttribute->ValueLength;
                }
                else {
                    NtfsNonResidentAttribute* listAttribute = (NtfsNonResidentAttribute*)from;
                    disposableData = listAttribute->GetData();
                    if (null == disposableData) {
                        throw new ApplicationException();
                    }
                    listBase = (ListEntry*)disposableData.Data;
                    ulong candidateLength = listAttribute->DataSize;
                    if (uint.MaxValue < candidateLength) {
                        throw new ApplicationException();
                    }
                    listLength = (uint)candidateLength;
                }
                if (null == listBase) {
                    throw new ApplicationException();
                }
                uint offset = 0;
                while (offset < listLength) {
                    ListEntry* entry = (ListEntry*)((byte*)listBase + offset);
                    entry->Dump();
                    offset += entry->EntryLength;
                }
            }
            finally {
                if (null != disposableData) { disposableData.Dispose(); }
            }
        }

        private static unsafe bool HandleDataRetrieverRequest(ListEntry* entry, ListEntry* listBase,
            NtfsAttributeType currentAttributeType, ushort currentAttributeNumber,
            EntryDataCallbackDelegate dataRetriever, ref uint offset, bool includeData)
        {
            // The last callback invocation decided it needs some more data before deciding
            // what to do.
            NtfsAttribute* retrievedAttribute = null;
            bool dataIncluded = includeData;
            bool retry;
            IPartitionClusterData clusterData = null;
            NtfsPartition currentPartition = NtfsPartition.Current;

            try {
                while(true) {
                    if (null == retrievedAttribute) {
                        uint sectorsCount;
                        List<ulong> entries = new List<ulong>();
                        if (!includeData) {
                            sectorsCount = currentPartition.SectorsPerCluster;
                        }
                        else {
                            // Read each record and prepare for data retrieval
                            uint lastValidOffset;
                            while (true) {
                                entries.Add(entry->FileReferenceNumber);
                                lastValidOffset = offset;
                                offset += entry->EntryLength;
                                entry = (ListEntry*)((byte*)listBase + offset);
                                if ((currentAttributeNumber != entry->AttributeNumber)
                                    || (currentAttributeType != entry->AttributeType))
                                {
                                    break;
                                }
                            }
                            offset = lastValidOffset;
                            entry = (ListEntry*)((byte*)listBase + offset);
                        }
                        // Read each record into the pool allocated zone
                        int entriesCount = entries.Count;
                        int clusterSize = (int)(currentPartition.SectorsPerCluster *
                            currentPartition.BytesPerSector);
                        clusterData = currentPartition.GetBuffer((uint)(clusterSize * entriesCount));
                        if (null == clusterData) {
                            throw new ApplicationException();
                        }
                        long currentOffset = 0;
                        for (int index = 0; index < entriesCount; index++) {
                            currentPartition.GetCluster(clusterData, currentOffset,
                                entry->FileReferenceNumber & 0x0000FFFFFFFFFFFF);
                            currentOffset += clusterSize;
                        }
                        retrievedAttribute = (NtfsAttribute*)clusterData.Data;
                    }
                    if (!dataRetriever(retrievedAttribute, ref includeData, out retry)) {
                        return true;
                    }
                    if (!retry) { break; }
                    if (includeData && !dataIncluded) {
                        // Force another retrieval.
                        if(null != clusterData) {
                            clusterData.Dispose();
                            clusterData = null;
                        }
                        retrievedAttribute = null;
                    }
                }
                return false;
            }
            finally {
                if (null != clusterData) { clusterData.Dispose(); }
            }
        }

        /// <summary></summary>
        /// <param name="from">The NtfsAttributeListAttribute to be used for enumeration.</param>
        /// <remarks>WARNING : This might seems counterintuitive to have this method at a class level instead
        /// of making it an instance one. This is because we absolutely don't want it to be invoked on an
        /// object reference that is subject to being moved in memory by the GC. Forcing the caller to provide
        /// a pointer makes her responsible for enforcing the pinning requirements.</remarks>
        internal static unsafe void EnumerateEntries(NtfsAttribute* from,
            NtfsAttributeType searchedAttributeType, EntryEnumeratorCallbackDelegate callback)
        {
            if (null == from) {
                throw new ArgumentNullException();
            }
            if (NtfsAttributeType.AttributeAttributeList != from->AttributeType) {
                throw new ArgumentException();
            }
            IPartitionClusterData listAttributeData = null;
            ListEntry* listBase = null;
            uint listLength;
            try {
                if (from->IsResident) {
                    NtfsResidentAttribute* listAttribute = (NtfsResidentAttribute*)from;
                    listBase = (ListEntry*)((byte*)from + listAttribute->ValueOffset);
                    listLength = listAttribute->ValueLength;
                }
                else {
                    NtfsNonResidentAttribute* listAttribute = (NtfsNonResidentAttribute*)from;
                    listAttributeData = listAttribute->GetData();
                    if (null == listAttributeData) {
                        throw new ApplicationException();
                    }
                    listBase = (ListEntry*)listAttributeData.Data;
                    ulong candidateLength = listAttribute->DataSize;
                    if (uint.MaxValue < candidateLength) {
                        throw new ApplicationException();
                    }
                    listLength = (uint)candidateLength;
                }
                if (null == listBase) {
                    throw new ApplicationException();
                }
                NtfsAttributeType currentAttributeType = NtfsAttributeType.Unused;
                ushort currentAttributeNumber = ushort.MaxValue;
                ListEntry* entry;
                for (uint offset = 0; offset < listLength; offset += entry->EntryLength) {
                    entry = (ListEntry*)((byte*)listBase + offset);
                    if ((currentAttributeNumber == entry->AttributeNumber)
                        || (currentAttributeType == entry->AttributeType))
                    {
                        if ((NtfsAttributeType.Unused == searchedAttributeType)
                            || (entry->AttributeType == searchedAttributeType))
                        {
                            currentAttributeNumber = entry->AttributeNumber;
                            currentAttributeType = entry->AttributeType;
                            EntryDataCallbackDelegate dataRetriever;
                            bool includeData;
                            if (!callback(entry, out dataRetriever, out includeData)) {
                                return;
                            }
                            if (null != dataRetriever) {
                                // The last callback invocation decided it needs some more data before deciding
                                // what to do.
                                if (HandleDataRetrieverRequest(entry, listBase, currentAttributeType, 
                                    currentAttributeNumber, dataRetriever, ref offset, includeData))
                                {
                                    return;
                                }
                                //NtfsAttribute* retrievedAttribute = null;
                                //bool dataIncluded = includeData;
                                //bool retry;
                                //IPartitionClusterData clusterData = null;
                                //NtfsPartition currentPartition = NtfsPartition.Current;

                                //try {
                                //    while(true) {
                                //        if (null == retrievedAttribute) {
                                //            uint sectorsCount;
                                //            List<ulong> entries = new List<ulong>();
                                //            if (!includeData) {
                                //                sectorsCount = currentPartition.SectorsPerCluster;
                                //            }
                                //            else {
                                //                // Read each record and prepare for data retrieval
                                //                uint lastValidOffset;
                                //                while (true) {
                                //                    entries.Add(entry->FileReferenceNumber);
                                //                    lastValidOffset = offset;
                                //                    offset += entry->EntryLength;
                                //                    entry = (ListEntry*)((byte*)listBase + offset);
                                //                    if ((currentAttributeNumber != entry->AttributeNumber)
                                //                        || (currentAttributeType != entry->AttributeType))
                                //                    {
                                //                        break;
                                //                    }
                                //                }
                                //                offset = lastValidOffset;
                                //                entry = (ListEntry*)((byte*)listBase + offset);
                                //            }
                                //            // Read each record into the pool allocated zone
                                //            int entriesCount = entries.Count;
                                //            int clusterSize = (int)(currentPartition.SectorsPerCluster *
                                //                currentPartition.BytesPerSector);
                                //            clusterData = currentPartition.GetBuffer((uint)(clusterSize * entriesCount));
                                //            if (null == clusterData) {
                                //                throw new ApplicationException();
                                //            }
                                //            long currentOffset = 0;
                                //            for (int index = 0; index < entriesCount; index++) {
                                //                currentPartition.GetCluster(clusterData, currentOffset,
                                //                    entry->FileReferenceNumber & 0x0000FFFFFFFFFFFF);
                                //                currentOffset += clusterSize;
                                //            }
                                //            retrievedAttribute = (NtfsAttribute*)clusterData.Data;
                                //        }
                                //        if (!dataRetriever(retrievedAttribute, ref includeData, out retry)) {
                                //            return;
                                //        }
                                //        if (!retry) { break; }
                                //        if (includeData && !dataIncluded) {
                                //            // Force another retrieval.
                                //            if(null != clusterData) {
                                //                clusterData.Dispose();
                                //                clusterData = null;
                                //            }
                                //            retrievedAttribute = null;
                                //        }
                                //    }
                                //}
                                //finally {
                                //    if (null != clusterData) { clusterData.Dispose(); }
                                //}
                            }
                        }
                    }
                }
            }
            finally {
                if (null != listAttributeData) { listAttributeData.Dispose(); }
            }
        }

        internal void GetEnumeratedEntryData()
        {
            throw new NotImplementedException();
        }

        internal struct ListEntry
        {
            /// <summary>Returns attribute name or a null reference if the name is undefined.</summary>
            internal unsafe string Name
            {
                get
                {
                    if (0 == NameLength) { return null; }
                    fixed (ListEntry* ptr = &this) {
                        return Encoding.Unicode.GetString((byte*)ptr + NameOffset, sizeof(char) * NameLength);
                    }
                }
            }

            internal unsafe void BinaryDump()
            {
                fixed (ListEntry* ptr = &this) {
                    Helpers.BinaryDump((byte*)ptr, this.EntryLength);
                }
            }

            internal void Dump()
            {
                Console.WriteLine("T:{0}, L:{1}, VCN:0x{2:X8}, FRN:0x{3:X8}, #{4} ({5})",
                    AttributeType, EntryLength, LowVcn, FileReferenceNumber,
                    AttributeNumber, Name);
            }

            /// <summary>Type of referenced attribute.</summary>
            internal NtfsAttributeType AttributeType;
            /// <summary>Byte size of this entry (8-byte aligned).</summary>
            internal ushort EntryLength;
            /// <summary>Size in Unicode chars of the name of the attribute or 0 if unnamed.</summary>
            internal byte NameLength;
            /// <summary>Byte offset to beginning of attribute name (always set this to where
            /// the name would start even if unnamed).</summary>
            internal byte NameOffset;
            /// <summary>Lowest virtual cluster number of this portion of the attribute value.
            /// This is usually 0. It is non-zero for the case where one attribute does not fit
            /// into one mft record and thus several mft records are allocated to hold this
            /// attribute. In the latter case, each mft record holds one extent of the attribute
            /// and there is one attribute list entry for each extent.
            /// NOTE: This is DEFINITELY a signed value! The windows driver uses cmp, followed
            /// by jg when comparing this, thus it treats it as signed.</summary>
            internal ulong LowVcn;
            /// <summary>The reference of the mft record holding the <see cref="NtfsAttribute"/>
            /// for this portion of the attribute value.</summary>
            internal ulong FileReferenceNumber;
            /// <summary>If lowest_vcn = 0, the instance of the attribute being referenced;
            /// otherwise 0.</summary>
            internal ushort AttributeNumber;
            // The name if any starts here.
        }
    }
}
