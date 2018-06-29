using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>This is the root node of the B+ tree that implements an index (e.g. a directory).
    /// This file attribute is always resident.</summary>
    /// <remarks>An INDEX_ROOT structure is followed by a sequence of DIRECTORY_ENTRY structures.
    /// Total size before first possible entry is 0x38/56 bytes.</remarks>
    internal struct NtfsRootIndexAttribute
    {
        internal void AssertResident()
        {
            Header.AssertResident();
        }

        internal unsafe void Dump()
        {
            Header.AssertResident();
            Header.Dump();
            Console.WriteLine("\tType {0}, Coll {1}, BPIR {2}, CPIR {3}",
                Helpers.uint32ToUnicodeString(Type), Helpers.uint32ToUnicodeString(CollationRule),
                BytesPerIndexRecord, ClustersPerIndexRecord);
            NodeHeader.Dump();
            int entryIndex = 0;
            fixed (NtfsNodeHeader* pNodeHeader = &NodeHeader) {
                NtfsDirectoryIndexEntry* scannedEntry =
                    (NtfsDirectoryIndexEntry*)((byte*)pNodeHeader + pNodeHeader->OffsetToFirstIndexEntry);
                do {
                    Console.WriteLine("\t\tentry #{0}", entryIndex++);
                    scannedEntry->Dump();
                    if (scannedEntry->GenericEntry.LastIndexEntry) { break; }
                    scannedEntry = (NtfsDirectoryIndexEntry*)((byte*)scannedEntry + scannedEntry->GenericEntry.EntryLength);
                } while (true);
            }
        }

        internal uint GetTotalSize()
        {
            uint result = Header.Header.Length;
            return result;
        }

        internal NtfsResidentAttribute Header;
        // This part is 0x10/16 bytes
        /// <summary>The type of the attribute that is indexed</summary>
        internal uint Type;
        /// <summary>A numeric identifier of the collation rule used to sort the index entries.</summary>
        internal uint CollationRule;
        /// <summary>The number of bytes per index block.</summary>
        internal uint BytesPerIndexRecord;
        /// <summary>The number of clusters per index block.</summary>
        internal byte ClustersPerIndexRecord;
        internal byte _unused1;
        internal byte _unused2;
        internal byte _unused3;
        // ----------------- DIRECTORY_INDEX part 0x10/16 bytes long -------------------------
        internal NtfsNodeHeader NodeHeader;
        // Directory index entries just follows the node header which has a variable size.
    }
}
