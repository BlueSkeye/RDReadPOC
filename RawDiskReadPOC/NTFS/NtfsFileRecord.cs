using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>An entry in the MFT consists of a FILE_RECORD_HEADER followed by a sequence of
    /// attributes.</summary>
    internal struct NtfsFileRecord
    {
        internal unsafe void ApplyFixups()
        {
            Ntfs.ApplyFixups(BytesInUse);
        }

        internal void AssertRecordType()
        {
            if (Constants.FileRecordMarker != Ntfs.Type) {
                throw new AssertionException("Reacord type mismatch. Expected a FILE record.");
            }
        }

        internal unsafe void BinaryDump()
        {
            fixed (NtfsFileRecord* dumped = &this) {
                Helpers.BinaryDump((byte*)dumped, BytesInUse);
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
            for (int attributeIndex = 0; attributeIndex < header->NextAttributeNumber; attributeIndex++) {
                if (ushort.MaxValue == currentAttribute->AttributeNumber) { break; }
                if (header->BytesInUse < ((byte*)currentAttribute - (byte*)header)) { break; }
                if (NtfsAttributeType.AttributeNone == currentAttribute->AttributeType) { break; }
                if (!callback(currentAttribute)) { return; }
                currentAttribute = (NtfsAttribute*)((byte*)currentAttribute + currentAttribute->Length);
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
        /// <summary>The number of times that the MFT entry has been reused.</summary>
        internal ushort SequenceNumber;
        /// <summary>The number of directory links to the MFT entry.</summary>
        internal ushort LinkCount;
        /// <summary>The offset, in bytes, from the start of the structure to the first attribute
        /// of the MFT entry</summary>
        internal ushort AttributesOffset;
        /// <summary>A bit array of flags specifying properties of the MFT entry. The values defined
        /// include:0x0001 = InUse, 0x0002 = Directory</summary>
        internal ushort Flags;
        /// <summary>The number of bytes used by the MFT entry.</summary>
        internal uint BytesInUse;
        /// <summary>The number of bytes allocated for the MFT entry.</summary>
        internal uint BytesAllocated;
        /// <summary>If the MFT entry contains attributes that overflowed a base MFT entry, this
        /// member contains the file reference number of the base entry; otherwise, it contains
        /// zero.</summary>
        internal ulong BaseFileRecord;
        /// <summary>The number that will be assigned to the next attribute added to the MFT entry.
        /// </summary>
        internal ushort NextAttributeNumber;
    }
}
