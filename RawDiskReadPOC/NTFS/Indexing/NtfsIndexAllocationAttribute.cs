using System;

namespace RawDiskReadPOC.NTFS.Indexing
{
    /// <summary>The index allocation attribute is an array of index blocks. Each index block starts with
    /// an INDEX_BLOCK_HEADER structure, which is followed by a sequence of DIRECTORY_ENTRY structures.
    /// This is an array of index blocks. Each index block starts with an INDEX_BLOCK structure containing
    /// an index header, followed by a sequence of index entries(INDEX_ENTRY structures), as described by
    /// the INDEX_HEADER.</summary>
    /// <remarks>NOTE: Always non-resident (doesn't make sense to be resident anyway!).</remarks>
    internal struct NtfsIndexAllocationAttribute
    {
        internal void Dump()
        {
            Console.WriteLine(Helpers.Indent(2) + "BVcn 0x{0:X8}",
                IndexBlockVcn);
            IndexHeader.Dump();
        }

        /// <summary>$LogFile sequence number of the last modification of this index block.</summary>
        internal ulong LogFileSequenceNumber;
        /// <summary>The VCN of the index block.
        /// Virtual cluster number of the index block. If the cluster_size on the volume is leq the
        /// index_block_size of the directory, index_block_vcn counts in units of clusters, and in
        /// units of sectors otherwise.</summary>
        internal ulong IndexBlockVcn;
        /// <summary>Describes the following index entries.</summary>
        internal NtfsIndexHeader IndexHeader;

        // When creating the index block, we place the update sequence array at this offset, i.e.
        // before we start with the index entries. This also makes sense, otherwise we could run
        // into problems with the update sequence array containing in itself the last two bytes of
        // a sector which would mean that multi sector transfer protection wouldn't work. As you
        // can't protect data by overwriting it since you then can't get it back... When reading
        // use the data from the ntfs record header.
    }
}
