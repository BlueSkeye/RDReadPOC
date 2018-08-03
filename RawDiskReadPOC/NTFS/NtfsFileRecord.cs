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
            ref byte* buffer, RecordAttributeEnumeratorCallbackDelegate callback)
        {
            using (IPartitionClusterData data = owner.ReadSectors(recordLBA)) {
                buffer = data.Data;
                NtfsFileRecord* header = (NtfsFileRecord*)buffer;
                header->AssertRecordType();
                if (1024 < header->BytesAllocated) {
                    throw new NotImplementedException();
                }
                EnumerateRecordAttributes(header, callback);
            }
        }

        internal unsafe void EnumerateRecordAttributes(RecordAttributeEnumeratorCallbackDelegate callback)
        {
            fixed (NtfsFileRecord* header = &this) {
                EnumerateRecordAttributes(header, callback);
            }
        }

        internal static unsafe void EnumerateRecordAttributes(NtfsFileRecord* header,
            RecordAttributeEnumeratorCallbackDelegate callback)
        {
            // Walk attributes, seeking for the searched one.
            NtfsAttribute* currentAttribute = (NtfsAttribute*)((byte*)header + header->AttributesOffset);
            NtfsAttributeListAttribute* pendingAttributeList = null;
            for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                if (ushort.MaxValue == currentAttribute->AttributeNumber) { break; }
                if (header->BytesInUse < ((byte*)currentAttribute - (byte*)header)) { break; }
                if (NtfsAttributeType.AttributeNone == currentAttribute->AttributeType) { break; }
                // If we found an AttributeListAttribute, we must go one level deeper to
                // complete the enumeration.
                if (NtfsAttributeType.AttributeAttributeList == currentAttribute->AttributeType) {
                    if (null != pendingAttributeList) {
                        throw new ApplicationException();
                    }
                    // Defer handling
                    pendingAttributeList = (NtfsAttributeListAttribute*)currentAttribute;
                }
                else {
                    if (!callback(currentAttribute)) { return; }
                }
                currentAttribute = (NtfsAttribute*)((byte*)currentAttribute + currentAttribute->Length);
            }
            if (null != pendingAttributeList) {
                uint entriesCount = 0;
                NtfsAttributeListAttribute.EnumerateEntries((NtfsAttribute*)pendingAttributeList,
                    delegate (NtfsAttributeListAttribute.ListEntry* entry) {
                        entry->Dump();
                        entriesCount++;
                        return true;
                    });
                Console.WriteLine("{0} attributes in list.", entriesCount);
                throw new NotImplementedException();
            }
            return;
        }

        /// <summary>Retrieve the Nth attribute of a given kind from a file record.</summary>
        /// <param name="kind">Searched attribute type.</param>
        /// <param name="order">Attribute rank. Default is first. This is usefull for some kind of
        /// attributes such as Data one that can appear several times in a record.</param>
        /// <returns></returns>
        internal unsafe NtfsAttribute* GetAttribute(NtfsAttributeType kind, uint order = 1)
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
            });
            return result;
        }

        internal unsafe void* GetResidentAttributeValue(NtfsAttributeType kind,
            out NtfsResidentAttribute* attributeHeader, uint order = 1)
        {
            attributeHeader = (NtfsResidentAttribute*)GetAttribute(kind, order);
            attributeHeader->AssertResident();
            return (null == attributeHeader) ? null : attributeHeader->GetValue();
        }

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
