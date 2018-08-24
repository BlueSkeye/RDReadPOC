using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>Used whenever all attributes can't fit in a single MFT record. Several records are
    /// required and this attribute is used to list all of these records.</summary>
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
        private unsafe delegate void DumpCallbackDelegate(ListEntry* entry);

        /// <summary>Whenever a process enumerating <see cref="ListEntry"/> from an
        /// <see cref="NtfsAttributeListAttribute"/> wishes to retrieve the underlying
        /// <see cref="NtfsAttribute"/>, it should provide an implementation of this delegate that
        /// will be later invoked with attribute data.</summary>
        /// <param name="attribute">The attribute (with data) to be handled. The data includes either just
        /// the header or full data depending on the value of the includeData parameter.</param>
        /// <param name="data">If this parameter is not null, the invoked delegate can read the stream
        /// to access the attribute data content.</param>
        /// <param name="retry">On return, if this value is true, the same delegate will be invoked again
        /// for the same attribute. This is usefull for an attribute that may have a lot of data, such as
        /// the $J attribute of the $UsnJrnl where the method invoking the enumeration wants to quickly
        /// find the relevant attribute just using attribute header data, then once found have a closer
        /// look at the attribute data. This can be easily performed by being called back with just the
        /// attribute header first, then setting the retry parameter.</param>
        /// <returns>True if enumeration should continue, false otherwise.</returns>
        internal unsafe delegate bool EntryListReferencedAttributeHandlerDelegate(NtfsAttribute* attribute,
            Stream data, out bool retry);

        /// <summary>The delegate to be invoked by the entry enumerator method.</summary>
        /// <param name="entry">An enumerated entry. Each attribute will be enumerated once only, even
        /// if it span several entries. The enumeration will occur on entry having LowVcn = 0.</param>
        /// <param name="attributeDataHandler">On return, if this parameter is not a null reference, it
        /// is a delegate to be invoked either with the <see cref="NtfsAttribute"/> header only or with
        /// full attribute data depending on the value of includeFullData</param>
        /// <param name="includeData">The value of this parameter on delegate invocation return is
        /// meaningfull iif the attributeDataHandler is not a null reference. This boolean value is true
        /// if the attributeDataHandle is to be invoked with full attribute data. Otherwise the sole
        /// the attribute header part will be provided.</param>
        /// <returns></returns>
        internal unsafe delegate bool EntryEnumeratorCallbackDelegate(ListEntry* entry,
            out EntryListReferencedAttributeHandlerDelegate attributeDataHandler, out bool includeData);

        internal static unsafe void BinaryDump(NtfsAttributeListAttribute* from)
        {
            _Dump(from, delegate (ListEntry* entry) { entry->BinaryDump(); });
        }

        internal static unsafe void Dump(NtfsAttributeListAttribute* from)
        {
            _Dump(from, delegate(ListEntry* entry) { entry->Dump(); });
        }

        private static unsafe void _Dump(NtfsAttributeListAttribute* from, DumpCallbackDelegate callback)
        {
            if (null == from) {
                throw new ArgumentNullException();
            }
            if (NtfsAttributeType.AttributeAttributeList != from->Header.AttributeType) {
                throw new ArgumentException();
            }
            IPartitionClusterData disposableData = null;
            ListEntry* listBase = null;
            uint listLength;
            try {
                if (from->Header.IsResident) {
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
                    callback(entry);
                    offset += entry->EntryLength;
                }
            }
            finally {
                if (null != disposableData) { disposableData.Dispose(); }
            }
        }

        /// <summary></summary>
        /// <param name="from">The NtfsAttributeListAttribute to be used for enumeration.</param>
        /// <param name="searchedAttributeType">The type of the searched attribute or
        /// <see cref="NtfsAttributeType.Any"/> if the caller is interested in all kinds of
        /// attributes.</param>
        /// <param name="listEntryHandler">A callback to be invoked on each entry matching
        /// the attribute type selection criteria.</param>
        /// <remarks>WARNING : This might seems counterintuitive to have this method at a class
        /// level instead of making it an instance one. This is because we absolutely don't want
        /// it to be invoked on an object reference that is subject to being moved in memory by
        /// the GC. Forcing the caller to provide a pointer makes her responsible for enforcing
        /// the pinning requirements.</remarks>
        internal static unsafe void EnumerateEntries(NtfsAttribute* from,
            NtfsAttributeType searchedAttributeType, EntryEnumeratorCallbackDelegate listEntryHandler)
        {
            if (null == from) {
                throw new ArgumentNullException();
            }
            if (NtfsAttributeType.AttributeAttributeList != from->AttributeType) {
                throw new ArgumentException();
            }
            IPartitionClusterData listAttributeData = null;
            // Address of first ListeEntry item for this attribute.
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
                NtfsAttributeType currentAttributeType = NtfsAttributeType.Any;
                ushort currentAttributeNumber = ushort.MaxValue;
                ListEntry* scannedEntry;
                for (uint offset = 0; offset < listLength; offset += scannedEntry->EntryLength) {
                    scannedEntry = (ListEntry*)((byte*)listBase + offset);
                    if (   (currentAttributeNumber == scannedEntry->AttributeNumber)
                        && (currentAttributeType == scannedEntry->AttributeType))
                    {
                        // The entry is a continuation of the previous one. Ignore it. It should
                        // have been processed by a previous loop if required.
                        continue;
                    }
                    currentAttributeNumber = scannedEntry->AttributeNumber;
                    currentAttributeType = scannedEntry->AttributeType;
                    if (   (NtfsAttributeType.Any != searchedAttributeType)
                        && (scannedEntry->AttributeType != searchedAttributeType))
                    {
                        // This entry doesn't match the search criteria on attribute type.
                        continue;
                    }
                    EntryListReferencedAttributeHandlerDelegate attributeDataHandler;
                    bool includeData;
                    if (!listEntryHandler(scannedEntry, out attributeDataHandler, out includeData)) {
                        // The callback doesn't wish to continue with other list entries.
                        return;
                    }
                    if (null == attributeDataHandler) {
                        // The callback doesn't wish to retrieve the attribute itself for the
                        // currently scanned entry.
                        continue;
                    }
                    // The last callback invocation decided it needs some data from the attribute
                    // itself before deciding what to do.
                    if (!HandleEntryReferencedAttribute(scannedEntry, listLength - offset,
                        attributeDataHandler, includeData))
                    {
                        return;
                    }
                }
            }
            finally {
                if (null != listAttributeData) { listAttributeData.Dispose(); }
            }
        }

        /// <summary>The caller enumerating entries might be interested in the content of
        /// the currently scanned entry. This might be either for additional filtering at
        /// attribute level (including attribute name) or for full attribute data processing.
        /// </summary>
        /// <param name="entry">The scanned list entry of interest. Should a single
        /// attribute span several entries, this one is guaranteed to be the first one for the
        /// attribute.</param>
        /// <param name="remainingBytesInList">Number of bytes remaining in list, relatively to the
        /// entry address.</param>
        /// <param name="entryReferencedAttributeHandler">The callback that will be invoked
        /// with the referenced attribute with or without full attribute data depending on
        /// the value of <paramref name="dataIncluded"/></param>
        /// <param name="dataIncluded"></param>
        /// <returns>true if caller should continue process data, false if it should stop.</returns>
        private static unsafe bool HandleEntryReferencedAttribute(ListEntry* entry, uint remainingBytesInList,
            EntryListReferencedAttributeHandlerDelegate entryReferencedAttributeHandler,
            bool dataIncluded)
        {
            ushort currentAttributeNumber = entry->AttributeNumber;
            NtfsAttributeType currentAttributeType = entry->AttributeType;
            byte* baseAddress = (byte*)entry;
            uint relativeOffset = 0;

            // The last callback invocation decided it needs some more data before deciding
            // what to do.
            IPartitionClusterData clusterData = null;
            NtfsPartition currentPartition = NtfsPartition.Current;

            using (PartitionDataDisposableBatch batch = PartitionDataDisposableBatch.CreateNew()) {
                while (true) {
                    ListEntry* scannedEntry = (ListEntry*)baseAddress;
                    ulong mainFileReferenceNumber = entry->FileReferenceNumber;
                    List<ulong> entries = new List<ulong>();
                    Stream dataStream = null;
                    if (dataIncluded) {
                        // Read each record and prepare for data retrieval. 
                        while (true) {
                            entries.Add(scannedEntry->FileReferenceNumber);
                            relativeOffset += scannedEntry->EntryLength;
                            if (relativeOffset >= remainingBytesInList) {
                                // Take care not to go further than the end of the list.
                                break;
                            }
                            scannedEntry = (ListEntry*)(baseAddress + relativeOffset);
                            if (   (currentAttributeNumber != scannedEntry->AttributeNumber)
                                || (currentAttributeType != scannedEntry->AttributeType))
                            {
                                break;
                            }
                        }
                        dataStream = new MultiRecordAttributeDataStream(entries);
                    }
                    // Retrieve attribute itself.
                    NtfsFileRecord* mainFileRecord =
                        currentPartition.GetFileRecord(mainFileReferenceNumber, ref clusterData);
                    if (null == mainFileRecord) {
                        throw new ApplicationException();
                    }
                    NtfsAttribute* retrievedAttribute = 
                        (NtfsAttribute*)((byte*)mainFileRecord + mainFileRecord->AttributesOffset);

                    // Invoke callback.
                    bool retry;
                    if (!entryReferencedAttributeHandler(retrievedAttribute, dataStream, out retry)) {
                        // After attribute has been processed, it has been decided no other list
                        // entry should be performed.
                        return false;
                    }
                    if (!retry) {
                        // After attribute has been processed, it has been decided that no
                        // additional data from this attribute is required. However the enumeration
                        // of other list entries should continue.
                        return true;
                    }
                    if (dataIncluded) {
                        throw new InvalidOperationException();
                    }
                    // Attribute has been processed, however not enough data was available for a
                    // final decision. We loop and include all data now.
                    dataIncluded = true;
                }
            }
        }

        /// <summary>The attribute header. From there we can cast to a resident or non resident
        /// attribute.</summary>
        private NtfsAttribute Header;

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

        /// <summary>Implement a datastream that span over several file records. This is quite specific
        /// and can only be found with attributes defined through an attributeListAttribute such as
        /// for the $J attribute of the Usn journal</summary>
        internal class MultiRecordAttributeDataStream : Stream
        {
            internal MultiRecordAttributeDataStream(List<ulong> fileReferenceNumbers)
            {
                if (null == fileReferenceNumbers) {
                    throw new ArgumentNullException();
                }
                if (1 > fileReferenceNumbers.Count) {
                    throw new ArgumentException();
                }
                _fileReferenceNumbers = new List<ulong>(fileReferenceNumbers);
                _currentEntryIndex = 0;
                _currentEntry = _fileReferenceNumbers[_currentEntryIndex];
                _thisBatch = PartitionDataDisposableBatch.CreateNew(true);
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanTimeout => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                _thisBatch.Detach();
                _thisBatch.Dispose();
            }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                _thisBatch.Attach();
                try {
                    throw new NotImplementedException();
                }
                finally {
                    _thisBatch.Detach();
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            private PartitionDataDisposableBatch _thisBatch;
            private ulong _currentEntry;
            private int _currentEntryIndex;
            private List<ulong> _fileReferenceNumbers;
        }
    }
}
