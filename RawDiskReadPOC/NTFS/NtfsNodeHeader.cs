using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>Used in both <see cref="NtfsRootIndexAttribute"/> and
    /// <see cref="NtfsIndexAllocationAttribute"/>
    /// This is the header for indexes, describing the NtfsIndexEntry records, which follow
    /// the NtfsNodeHeader. Together the NtfsNodeHeader and the index entries make up a complete
    /// index.</summary>
    /// <remarks>IMPORTANT NOTE: The offset, length and size structure members are counted
    /// relative to the start of the NtfsNodeHeader structure and not relative to the start of
    /// the index root or index allocation structures themselves.</remarks>
    internal struct NtfsNodeHeader
    {
        internal unsafe void Dump()
        {
            Console.WriteLine(Helpers.Indent(2) + "1st off {0}, last off {1}, All off {2}, {3}",
                OffsetToFirstIndexEntry, IndexLength, AllocationSize, Flags);
        }

        /// <summary>Offset to start of index entry list. In the context of the ROOT index attribute
        /// this is relative to the NtfsNodeHeader itself. In an INDEX_BLOCK this is relative to the
        /// NtfsRecord preceeding the NtfsNodeHeader.</summary>
        internal uint OffsetToFirstIndexEntry;

        // NOTE: For the index root attribute, the below two numbers are always equal, as the
        // attribute is resident and it is resized as needed. In the case of the index allocation
        // attribute the attribute is not resident and hence the allocated_size is a fixed value
        // and must equal the index_block_size specified by the INDEX_ROOT attribute corresponding
        // to the INDEX_ALLOCATION attribute this INDEX_BLOCK belongs to.

        /// <summary>Data size of the index in bytes, i.e.bytes used from allocated size, aligned
        /// to 8-byte boundary.</summary>
        internal uint IndexLength;
        /// <summary>Byte size of this index (block), multiple of 8 bytes.</summary>
        internal uint AllocationSize;
        /// <summary>A bit array of flags specifying properties of the index.</summary>
        internal DirectoryFlags Flags;
        internal byte _unused1;
        internal byte _unused2;
        internal byte _unused3;

        internal enum DirectoryFlags : byte
        {
            // In an index root attribute
            SmallDirectory = 0x0000, // The index is small enough to fit inside the index root
                                     // attribute and there is no index allocation attribute present.
            LargeDirectory = 0x0001, // The index is too large to fit in the index root attribute
                                     // and/or an index allocation attribute is present.

            // an index block
            LeafNode = 0, // This is a leaf node, i.e. there are no more nodes branching off it.
            IndexNode = 1, // This node indexes other nodes, i.e. it is not a leaf node.
        }
    }
}
