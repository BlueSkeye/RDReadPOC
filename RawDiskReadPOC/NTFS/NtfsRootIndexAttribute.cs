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
            ntfsResident.AssertResident();
        }

        internal unsafe void Dump()
        {
            ntfsResident.Dump();
            Console.WriteLine("Type {0}, Coll {1}, BPIB {2}, CPIB {3}",
                Helpers.uint32ToUnicodeString(Type), Helpers.uint32ToUnicodeString(CollationRule),
                BytesPerIndexBlock, ClustersPerIndexBlock);
            Console.WriteLine("\t1st off {0}, TotL {1}, Allo {2}, {3}",
                OffsetToFirstIndexEntry, IndexEntriesTotalSize, IndexEntriesAllocatedSize, Flags);
            fixed (NtfsDirectoryEntry* anchor = &DirectoryIndex) {
            }
        }

        internal NtfsResidentAttribute ntfsResident;
        // This part is 0x10/16 bytes
        /// <summary>The type of the attribute that is indexed</summary>
        internal uint Type;
        /// <summary>A numeric identifier of the collation rule used to sort the index entries.</summary>
        internal uint CollationRule;
        /// <summary>The number of bytes per index block.</summary>
        internal uint BytesPerIndexBlock;
        /// <summary>The number of clusters per index block.</summary>
        internal byte ClustersPerIndexBlock;
        internal byte _unused1;
        internal byte _unused2;
        internal byte _unused3;
        // ----------------- DIRECTORY_INDEX part 0x10/16 bytes long -------------------------
        /// <summary>The offset, in bytes, from this field to the first DIRECTORY_ENTRY
        /// structure.</summary>
        internal uint OffsetToFirstIndexEntry;
        /// <summary>The size, in bytes, of the portion of the index block that is in use.</summary>
        internal uint IndexEntriesTotalSize;
        /// <summary>The size, in bytes, of disk space allocated for the index block.</summary>
        internal uint IndexEntriesAllocatedSize;
        /// <summary>A bit array of flags specifying properties of the index. The values defined
        /// include:</summary>
        internal DirectoryFlags Flags;
        internal byte _unused4;
        internal byte _unused5;
        internal byte _unused6;
        /// <summary>A set of DIRECTORY_INDEX structure.</summary>
        internal NtfsDirectoryEntry DirectoryIndex;

        internal enum DirectoryFlags : byte
        {
            SmallDirectory = 0x0000, // Directory fits in index root
            LargeDirectory = 0x0001 // Directory overflows index root
        }
    }
}
