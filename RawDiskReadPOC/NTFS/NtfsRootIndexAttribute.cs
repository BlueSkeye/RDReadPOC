using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>This is the root node of the B+ tree that implements an index (e.g. a directory).
    /// This file attribute is always resident.</summary>
    /// <remarks>An INDEX_ROOT structure is followed by a sequence of DIRECTORY_ENTRY structures.
    /// Total size before first possible entry is 0x38/56 bytes.</remarks>
    internal struct NtfsRootIndexAttribute
    {
        internal unsafe void Dump()
        {
            Console.WriteLine("\tType {0}, Coll {1}, BPIR {2}, CPIR {3}",
                Helpers.uint32ToUnicodeString(Type),
                // Helpers.uint32ToUnicodeString(CollationRule),
                CollationRule,
                BytesPerIndexRecord, ClustersPerIndexRecord);
            int entryIndex = 0;
            EnumerateIndexEntries(delegate (NtfsDirectoryIndexEntry* scannedEntry) {
                Console.WriteLine("\t\tentry #{0}", entryIndex++);
                scannedEntry->BinaryDump();
                scannedEntry->Dump();
                return true;
            });
        }

        internal unsafe void EnumerateIndexEntries(NtfsIndexEntryHandlerDelegate callback)
        {
            fixed (NtfsRootIndexAttribute* pAttribute = &this) {
                NtfsNodeHeader* pNodeHeader = (NtfsNodeHeader*)((byte*)pAttribute + sizeof(NtfsRootIndexAttribute));
                pNodeHeader->Dump();
                NtfsDirectoryIndexEntry* scannedEntry =
                    (NtfsDirectoryIndexEntry*)((byte*)pNodeHeader + pNodeHeader->OffsetToFirstIndexEntry);
                while (true) {
                    ulong scannedEntryOffset = (ulong)((byte*)scannedEntry - (byte*)pNodeHeader);
                    if (pNodeHeader->OffsetToEndOfIndexEntries <= scannedEntryOffset) {
                        throw new ApplicationException();
                    }
                    if (!callback(scannedEntry)) {
                        return;
                    }
                    // TODO : Clarify exit condition. Does the last record holds any valuable data ?
                    if (scannedEntry->GenericEntry.LastIndexEntry) {
                        return;
                    }
                    scannedEntry = (NtfsDirectoryIndexEntry*)((byte*)scannedEntry + scannedEntry->GenericEntry.EntryLength);
                }
                throw new ApplicationException("Last index entry missing.");
            }
        }

        //internal uint GetTotalSize()
        //{
        //    uint result = Header.Header.Length;
        //    return result;
        //}

        //// This part is 0x18/24 bytes
        //internal NtfsResidentAttribute Header;
        //// TODO : Clarify why this padding is required. EITHER there is an alignment constraint that we
        //// missed in our readings OR there are additional fields that are new in our testing environment.
        //// The later is less likely due to FS version.
        //internal ulong _padding;

        // The following part, until NodeHeader (not included) is 0x10/16 bytes.
        /// <summary>The type of the attribute that is indexed</summary>
        internal uint Type;
        /// <summary>A numeric identifier of the collation rule used to sort the index entries.</summary>
        internal uint CollationRule;
        /// <summary>The number of bytes per index record. This is the size of the records in the
        /// <see cref="NtfsIndexAllocationAttribute"/> data part.</summary>
        internal uint BytesPerIndexRecord;
        /// <summary>The number of clusters per index record. For the possible interpretation of this value,
        /// consider the usual rule that a negative value means the Log2 of the real value.
        /// The value must also be consistent with the one to be found in <see cref="NtfsPartition"/>
        /// </summary>
        internal byte ClustersPerIndexRecord;
        internal byte _unused1;
        internal byte _unused2;
        internal byte _unused3;
        // This attribute stores a single node grouping one or more index entries. Entries in a node
        // are organized as a list. The last entry in a list is a special empty one.
        // ----------------- DIRECTORY_INDEX part 0x10/16 bytes long -------------------------
        //internal NtfsNodeHeader NodeHeader;
        //// Directory index entries just follows the node header which has a variable size.
        //internal NtfsDirectoryIndexEntry IndexEntry;
    }
}
