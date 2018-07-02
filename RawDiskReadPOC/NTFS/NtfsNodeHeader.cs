using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>Used in both <see cref="NtfsRootIndexAttribute"/> and
    /// <see cref="NtfsIndexAllocationAttribute"/></summary>
    internal struct NtfsNodeHeader
    {
        internal unsafe void Dump()
        {
            Console.WriteLine("\t\t1st off {0}, last off {1}, All off {2}, {3}",
                OffsetToFirstIndexEntry, OffsetToEndOfIndexEntries, OffsetToEndOfAllocation, Flags);
        }

        /// <summary>Offset to start of index entry list, relatively to the node header itself.</summary>
        internal uint OffsetToFirstIndexEntry;
        /// <summary>Offset to end of used portion of index entry list relatively to the node
        /// header itself.</summary>
        internal uint OffsetToEndOfIndexEntries;
        /// <summary>Offset to end of allocated index entry list buffer, relatively to the node header
        /// itself.</summary>
        internal uint OffsetToEndOfAllocation;
        /// <summary>A bit array of flags specifying properties of the index.</summary>
        internal DirectoryFlags Flags;
        internal byte _unused1;
        internal byte _unused2;
        internal byte _unused3;

        internal enum DirectoryFlags : byte
        {
            SmallDirectory = 0x0000, // Directory fits in index root
            LargeDirectory = 0x0001 // Directory overflows index root
        }
    }
}
