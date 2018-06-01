﻿
namespace RawDiskReadPOC.NTFS
{
    /// <summary>The index allocation attribute is an array of index blocks. Each index block starts with
    /// an INDEX_BLOCK_HEADER structure, which is followed by a sequence of DIRECTORY_ENTRY structures.
    /// </summary>
    internal class NtfsIndexAllocationAttribute
    {
        /// <summary>An NTFS_RECORD_HEADER structure with a Type of ‘INDX’.</summary>
        internal NtfsRecordHeader Ntfs;
        /// <summary>The VCN of the index block.</summary>
        internal ulong IndexBlockVcn;
        /// <summary>A DIRECTORY_INDEX structure.</summary>
        internal NtfsDirectoryIndex DirectoryIndex;
    }
}