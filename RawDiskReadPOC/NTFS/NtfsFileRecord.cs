using System;
using System.IO;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>The mft record header present at the beginning of every record in the mft.
    /// This is followed by a sequence of variable length attribute records which is
    /// terminated by an attribute of type AT_END which is a truncated attribute in that it
    /// only consists of the attribute type code AT_END and none of the other members of the
    /// attribute structure are present.</summary>
    internal struct NtfsFileRecord
    {
        /// <summary>A name filter delegate.</summary>
        /// <param name="candidate">The delegate is guaranteed the attribute's name is available.
        /// There should be no additional assumption as to what additional data may or may not
        /// be available.</param>
        /// <returns>true if the attribute qualifies and computation should continue with this
        /// attribute, false otherwise.</returns>
        internal unsafe delegate bool AttributeNameFilterDelegate(NtfsAttribute* candidate);

        internal unsafe void ApplyFixups()
        {
            Ntfs.ApplyFixups();
        }

        internal void AssertRecordType()
        {
            if (Constants.FileRecordMarker != Ntfs.Type) {
                throw new AssertionException("Record type mismatch. Expected a FILE record.");
            }
        }

        internal unsafe void BinaryDump()
        {
            fixed (NtfsFileRecord* dumped = &this) {
                Helpers.BinaryDump((byte*)dumped, BytesInUse);
            }
        }

        internal unsafe void BinaryDumpContent()
        {
            NtfsAttribute* dataAttribute = GetAttribute(NtfsAttributeType.AttributeData);
            if (null == dataAttribute) {
                throw new ApplicationException();
            }
            if (dataAttribute->IsResident) {
                NtfsResidentAttribute* realDataAttribute = (NtfsResidentAttribute*)dataAttribute;
                Helpers.BinaryDump((byte*)realDataAttribute + realDataAttribute->ValueOffset,
                    realDataAttribute->ValueLength);
            }
            else {
                NtfsNonResidentAttribute* realDataAttribute = (NtfsNonResidentAttribute*)dataAttribute;
                byte[] localBuffer = new byte[16 * 1024];
                fixed(byte* pBuffer = localBuffer) {
                    using (Stream dataStream = realDataAttribute->OpenDataStream()) {
                        while (true) {
                            int readLength = dataStream.Read(localBuffer, 0, localBuffer.Length);
                            if (-1 == readLength) { break; }
                            Helpers.BinaryDump(pBuffer, (uint)readLength);
                            if (readLength < localBuffer.Length) { break; }
                        }
                    }
                }
            }
        }

        internal static unsafe ulong CountAttributes(NtfsFileRecord* record)
        {
            ulong result = 0;
            NtfsAttribute* scannedAttribute = (NtfsAttribute*)((byte*)record + record->AttributesOffset);
            while (!scannedAttribute->IsLast) {
                result++;
                scannedAttribute = (NtfsAttribute*)((byte*)scannedAttribute + scannedAttribute->Length);
            }
            return result;
        }

        internal void Dump()
        {
            Ntfs.Dump();
            Console.WriteLine(
                "Seq {0}, #lnk {1}, aOff {2}, flg {3}, usd {4}, all {5}, bfr {6}, nxA {7}",
                SequenceNumber, LinkCount, AttributesOffset, Flags, BytesInUse, BytesAllocated,
                BaseFileRecord, NextAttributeNumber);
        }

        internal unsafe void DumpAttributes(bool binaryDump = false)
        {
            EnumerateRecordAttributes(delegate(NtfsAttribute* attribute) {
                if (binaryDump) { attribute->BinaryDump(); }
                else { attribute->Dump(); }
                return true;
            });
        }

        internal unsafe void EnumerateRecordAttributes(NtfsPartition owner, ulong recordLBA,
            ref byte* buffer, RecordAttributeEnumeratorCallbackDelegate callback,
            NtfsAttributeType searchedType = NtfsAttributeType.Any,
            AttributeNameFilterDelegate nameFilter = null)
        {
            using (IPartitionClusterData data = owner.ReadSectors(recordLBA)) {
                buffer = data.Data;
                NtfsFileRecord* header = (NtfsFileRecord*)buffer;
                header->AssertRecordType();
                if (1024 < header->BytesAllocated) {
                    throw new NotImplementedException();
                }
                EnumerateRecordAttributes(header, callback, searchedType, nameFilter);
            }
        }

        internal unsafe void EnumerateRecordAttributes(RecordAttributeEnumeratorCallbackDelegate callback,
            NtfsAttributeType searchedType = NtfsAttributeType.Any, AttributeNameFilterDelegate nameFilter = null)
        {
            fixed (NtfsFileRecord* header = &this) {
                EnumerateRecordAttributes(header, callback, searchedType, nameFilter);
            }
        }

        /// <summary>Enumerate attributes bound to this <see cref="NtfsFileRecord"/>, optionally filtering
        /// on attribute name.</summary>
        /// <param name="header">The file record.</param>
        /// <param name="callback"></param>
        /// <param name="searchedAttributeType"></param>
        /// <param name="nameFilter">An optional name filter that will be provided with the basic attribute
        /// properties (including name) in order to decide if data should be retrieved. This is especially
        /// usefull for <see cref="NtfsAttributeListAttribute"/> attributes that may reference lengthy
        /// attributes data which are expensive to retrieve.</param>
        internal static unsafe void EnumerateRecordAttributes(NtfsFileRecord* header,
            RecordAttributeEnumeratorCallbackDelegate callback, NtfsAttributeType searchedAttributeType,
            AttributeNameFilterDelegate nameFilter)
        {
            // Walk attributes, seeking for the searched one.
            NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)header + header->AttributesOffset);
            NtfsAttributeListAttribute* pendingAttributeList = null;
            NtfsAttribute*[] candidates = new NtfsAttribute*[MaxAttributeCount];
            int candidatesCount = 0;
            for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                if (currentAttribute->IsLast) { break; }
                if (header->BytesInUse < ((byte*)currentAttribute - (byte*)header)) { break; }
                if (NtfsAttributeType.EndOfListMarker == currentAttribute->AttributeType) { break; }
                // If we found an AttributeListAttribute, we must go one level deeper to
                // complete the enumeration.
                if (NtfsAttributeType.AttributeAttributeList == currentAttribute->AttributeType) {
                    if (null != pendingAttributeList) {
                        // No more than one attribute of this kind per file record.
                        throw new ApplicationException();
                    }
                    if (NtfsAttributeType.AttributeAttributeList == searchedAttributeType) {
                        if (!callback(currentAttribute)) { return; }
                    }
                    // Defer handling
                    pendingAttributeList = (NtfsAttributeListAttribute*)currentAttribute;
                    break;
                }
                if (candidatesCount >= MaxAttributeCount) {
                    throw new ApplicationException();
                }
                if ((NtfsAttributeType.Any == searchedAttributeType)
                    || (currentAttribute->AttributeType == searchedAttributeType))
                {
                    candidates[candidatesCount++] = currentAttribute;
                }
                currentAttribute = (NtfsAttribute*)((byte*)currentAttribute + currentAttribute->Length);
            }
            if (NtfsAttributeType.AttributeAttributeList == searchedAttributeType) {
                // Either we already found one such attribute and invoked the callback or found none and
                // we can return immediately. Should we have found several such attributes we would have
                // risen an exception.
                return;
            }
            if (null == pendingAttributeList) {
                // We already walked every attributes and captured those that matched the type filter if
                // any. Invoke callbak on each such attribute.
                for(int candidateIndex = 0; candidateIndex < candidatesCount; candidateIndex++) {
                    if (!callback(candidates[candidateIndex])) { return; }
                }
                // We are done.
                return;
            }
            // HandleAttributeListAttributeEntry
            // We have an attribute list attribute. Delegate him the enumeration.
            NtfsPartition currentPartition = NtfsPartition.Current;
            NtfsAttributeListAttribute.EnumerateEntries((NtfsAttribute*)pendingAttributeList, searchedAttributeType,
                new ListEntryHandler(searchedAttributeType, nameFilter, callback).HandleListEntry);
            return;
        }

        /// <summary>Retrieve the Nth attribute of a given kind from a file record.</summary>
        /// <param name="kind">Searched attribute type.</param>
        /// <param name="order">Attribute rank. Default is first. This is usefull for some kind of
        /// attributes such as Data one that can appear several times in a record.</param>
        /// <returns></returns>
        internal unsafe NtfsAttribute* GetAttribute(NtfsAttributeType kind, uint order = 1,
            AttributeNameFilterDelegate nameFilter = null)
        {
            NtfsAttribute* result = null;
            EnumerateRecordAttributes(delegate (NtfsAttribute* found) {
                if (kind == found->AttributeType) {
                    if (0 == --order) {
                        result = found;
                        return false;
                    }
                }
                return true;
            },
            kind, nameFilter);
            return result;
        }

        internal unsafe void* GetResidentAttributeValue(NtfsAttributeType kind,
            out NtfsResidentAttribute* attributeHeader, uint order = 1)
        {
            attributeHeader = (NtfsResidentAttribute*)GetAttribute(kind, order);
            attributeHeader->AssertResident();
            return (null == attributeHeader) ? null : attributeHeader->GetValue();
        }

        private const int MaxAttributeCount = 1024;
        internal const ulong RECORD_SIZE = 1024;
        /// <summary>An NTFS_RECORD_HEADER structure with a Type of ‘FILE’.</summary>
        internal NtfsRecord Ntfs;
        /// <summary>$LogFile sequence number for this record. Changed every time the record
        /// is modified.</summary>
        internal ulong LogFileSequenceNumber;
        /// <summary>Number of times this mft record has been reused. (See description for
        /// MFT_REF above.) NOTE: The increment(skipping zero) is done when the file is
        /// deleted.NOTE: If this is zero it is left zero.</summary>
        internal ushort SequenceNumber;
        /// <summary>Number of hard links, i.e. the number of directory entries referencing
        /// this record.
        /// NOTE: Only used in mft base records.
        /// NOTE: When deleting a directory entry we check the link_count and if it is 1 we
        /// delete the file. Otherwise we delete the FILENAME_ATTR being referenced by the
        /// directory entry from the mft record and decrement the link_count.
        /// FIXME: Careful with Win32 + DOS names!</summary>
        internal ushort LinkCount;
        /// <summary>Byte offset to the first attribute in this mft record from the start of
        /// the mft record.
        /// NOTE: Must be aligned to 8-byte boundary.</summary>
        internal ushort AttributesOffset;
        /// <summary>Bit array of MFT_RECORD_FLAGS. When a file is deleted, the
        /// <see cref="FileRecordFlags.InUse"/> flag is set to zero.</summary>
        internal FileRecordFlags Flags;
        /// <summary>Number of bytes used in this mft record.
        /// NOTE: Must be aligned to 8-byte boundary.</summary>
        internal uint BytesInUse;
        /// <summary>Number of bytes allocated for this mft record. This should be equal to
        /// the mft record size.</summary>
        internal uint BytesAllocated;
        /// <summary>This is zero for base mft records. When it is not zero it is a mft
        /// reference pointing to the base mft record to which this record belongs (this is
        /// then used to locate the attribute list attribute present in the base record
        /// which describes this extension record and hence might need modification when the
        /// extension record itself is modified, also locating the attribute list also means
        /// finding the other potential extents, belonging to the non-base mft record).</summary>
        internal ulong BaseFileRecord;
        /// <summary>The instance number that will be assigned to the next attribute added
        /// to this mft record.
        /// NOTE: Incremented each time after it is used.
        /// NOTE: Every time the mft record is reused this number is set to zero.
        /// NOTE: The first instance number is always 0.</summary>
        internal ushort NextAttributeNumber;
        internal ushort _unused; // Specific to NTFS 3.1+ (Windows XP and above)
        /// <summary>Number of this mft record. Specific to NTFS 3.1+ (Windows XP and above)</summary>
        internal uint MftRecordNumber;
        // When(re)using the mft record, we place the update sequence array at this offset,
        // i.e.before we start with the attributes. This also makes sense, otherwise we
        // could run into problems with the update sequence array containing in itself the
        // last two bytes of a sector which would mean that multi sector transfer protection
        // wouldn't work.  As you can't protect data by overwriting it since you then can't
        // get it back... When reading we obviously use the data from the ntfs record header.

        private class ListEntryHandler
        {
            internal unsafe ListEntryHandler(NtfsAttributeType searchedAttributeType,
                AttributeNameFilterDelegate nameFilter, RecordAttributeEnumeratorCallbackDelegate callback)
            {
                _callback = callback ?? throw new ArgumentNullException();
                _dataRetrievalCallback = HandleData;
                _searchedAttributeType = searchedAttributeType;
                _nameFilter = nameFilter;
            }

            private unsafe bool HandleData(NtfsAttribute* candidateAttribute, Stream attributeData, out bool retry)
            {
                if (null != attributeData) {
                    retry = false;
                    // Delegate the continuation decision to our caller.
                    return _callback(candidateAttribute);
                }
                if ((null != _nameFilter) && !_nameFilter(candidateAttribute)) {
                    // The name filter failed. Go on with next entry.
                    retry = false;
                    return true;
                }
                // The caller is interested in the attribute, either because there is no
                // name filter. We want the full data.
                retry = true;
                // We will land in the conditional block above.
                return true;
            }

            /// <summary>A callback method that will be invoked on each ListEntry from an
            /// <see cref="NtfsAttributeListAttribute"/> being enumerated.</summary>
            /// <param name="entry">The list entry being scanned.</param>
            /// <param name="attributeHandler">On return, if this parameter value is not a
            /// null reference, the referenced attribute is retrieved including more or less
            /// of its data and this delegate will be invoked.</param>
            /// <param name="includeFullData">On return this parameter is meaningless if the
            /// attributeHandler is a null reference, otherwise if tells whether full attribute
            /// data should be handed to the attributeHandler delegate (true) or if the sole
            /// attribute header will be available to the handler (false).</param>
            /// <returns></returns>
            internal unsafe bool HandleListEntry(NtfsAttributeListAttribute.ListEntry* entry,
                out NtfsAttributeListAttribute.EntryListReferencedAttributeHandlerDelegate attributeHandler,
                out bool includeFullData)
            {
                if (NtfsAttributeType.Any != _searchedAttributeType) {
                    if (entry->AttributeType < _searchedAttributeType) {
                        // Our caller narrowed the search to a special kind of attribute. This one doesn't
                        // match. Bail out and go on with next attribute.
                        attributeHandler = null;
                        includeFullData = false;
                        return true;
                    }
                    if (entry->AttributeType > _searchedAttributeType) {
                        // We can stop here because the list is sorted on attribute type first. Hence, we
                        // can be sure the searched attribute is NOT present.
                        attributeHandler = null;
                        includeFullData = false;
                        return false;
                    }
                }
                // Either the attribute type match our caller requirements or the caller is attribute
                // type agnostic.
                includeFullData = (null == _nameFilter);
                // Prepare to receive attribute data;
                attributeHandler = HandleData;
                // Hands out the continuation to the enumerator that will callback again either with
                // full data or just with the attribute itself.
                return true;
            }

            private RecordAttributeEnumeratorCallbackDelegate _callback;
            private NtfsAttributeListAttribute.EntryListReferencedAttributeHandlerDelegate _dataRetrievalCallback;
            private AttributeNameFilterDelegate _nameFilter;
            private NtfsAttributeType _searchedAttributeType;
        }

        /// <summary>These are the so far known MFT_RECORD_* flags (16-bit) which contain
        /// information about the mft record in which they are present.</summary>
        [Flags()]
        internal enum FileRecordFlags : ushort
        {
            /// <summary>Is set for all in-use mft records.</summary>
            InUse = 0x0001,
            /// <summary>Is set for all directory mft records, i.e. mft records containing
            /// and index with name "$I30" indexing filename attributes.</summary>
            Directory = 0x0002,
            /// <summary>Is set for all system files present in the $Extend system directory.</summary>
            InExtend = 0x0004,
            /// <summary>is set for all system files containing one or more indices with a
            /// name other than "$I30".</summary>
            IsViewIndex = 0x0008,
        }
    }
}
