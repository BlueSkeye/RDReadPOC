using System;

using RawDiskReadPOC.NTFS.Indexing;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>This is the root node of the B+ tree that implements an index (e.g. a directory).
    /// This file attribute is always resident.
    /// This is followed by a sequence of index entries (INDEX_ENTRY structures) as described
    /// by the index header.
    /// When a directory is small enough to fit inside the index root then this is the only
    /// attribute describing the directory. When the directory is too large to fit in the index
    /// root, on the other hand, two aditional attributes are present: an index allocation
    /// attribute, containing sub-nodes of the B+ directory tree (see below), and a bitmap
    /// attribute, describing which virtual cluster numbers (vcns) in the index allocation
    /// attribute are in use by an index block.</summary>
    /// <remarks>An INDEX_ROOT structure is followed by a sequence of DIRECTORY_ENTRY structures.
    /// Total size before first possible entry is 0x38/56 bytes.
    /// NOTE: Always resident.
    /// NOTE: The root directory (FILE_root) contains an entry for itself. Other dircetories do
    /// not contain entries for themselves, though.</remarks>
    internal struct NtfsRootIndexAttribute
    {
        internal unsafe void Dump()
        {
            Console.WriteLine(Helpers.Indent(1) + "Type {0}, Coll {1}, BPIR {2}, CPIR {3}",
                Helpers.uint32ToUnicodeString(Type),
                // Helpers.uint32ToUnicodeString(CollationRule),
                CollationRule,
                BytesPerIndexRecord, ClustersPerIndexRecord);
            int entryIndex = 0;
            EnumerateIndexEntries(delegate (NtfsIndexEntryHeader* scannedEntry) {
                Console.WriteLine(Helpers.Indent(2) + "entry #{0}", entryIndex++);
                scannedEntry->Dump();
                return true;
            }, true);
        }

        internal unsafe void EnumerateIndexEntries(NtfsIndexEntryHandlerDelegate callback,
            bool traceNodes)
        {
            fixed (NtfsRootIndexAttribute* pAttribute = &this) {
                NtfsNodeHeader* pNodeHeader = (NtfsNodeHeader*)((byte*)pAttribute + sizeof(NtfsRootIndexAttribute));
                if (traceNodes) {
                    pNodeHeader->Dump();
                }
                NtfsIndexEntryHeader* pIndexEntry =
                    (NtfsIndexEntryHeader*)((byte*)pNodeHeader + pNodeHeader->OffsetToFirstIndexEntry);
                while (true) {
                    ulong scannedEntryOffset = (ulong)((byte*)pIndexEntry - (byte*)pNodeHeader);
                    if (pNodeHeader->IndexLength <= scannedEntryOffset) {
                        throw new ApplicationException();
                    }
                    if (!callback(pIndexEntry)) {
                        return;
                    }
                    // TODO : Clarify exit condition. Does the last record holds any valuable data ?
                    if (pIndexEntry->LastIndexEntry) {
                        return;
                    }
                    pIndexEntry = (NtfsIndexEntryHeader*)((byte*)pIndexEntry + pIndexEntry->EntryLength);
                }
                throw new ApplicationException("Last index entry missing.");
            }
        }

        // The following part, until NodeHeader (not included) is 0x10/16 bytes.

        /// <summary>The type of the attribute that is indexed. Is AT_FILENAME for directories,
        /// zero for view indexes. No other values allowed.</summary>
        internal uint Type;
        /// <summary>A numeric identifier of the collation rule used to sort the index entries.
        /// Collation rule used to sort the index entries. If type is AT_FILENAME, this must be
        /// COLLATION_FILENAME.</summary>
        internal uint CollationRule;
        /// <summary>The number of bytes per index record. This is the size of the records in the
        /// <see cref="NtfsIndexAllocationAttribute"/> data part.
        /// Size of each index block in bytes (in the index allocation attribute).</summary>
        internal uint BytesPerIndexRecord;
        /// <summary>The number of clusters per index record. For the possible interpretation of this value,
        /// consider the usual rule that a negative value means the Log2 of the real value.
        /// The value must also be consistent with the one to be found in <see cref="NtfsPartition"/>
        /// Cluster size of each index block (in the index allocation attribute), when an index
        /// block is >= than a cluster, otherwise this will be the log of the size (like how
        /// the encoding of the mft record size and the index record size found in the boot
        /// sector work). Has to be a power of 2.</summary>
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
