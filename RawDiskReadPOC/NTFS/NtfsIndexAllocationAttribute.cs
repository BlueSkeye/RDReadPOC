﻿using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>The index allocation attribute is an array of index blocks. Each index block starts with
    /// an INDEX_BLOCK_HEADER structure, which is followed by a sequence of DIRECTORY_ENTRY structures.
    /// </summary>
    internal struct NtfsIndexAllocationAttribute
    {
        internal void Dump()
        {
            Console.WriteLine(Helpers.Indent(2) + "BVcn 0x{0:X8}",
                IndexBlockVcn);
            DirectoryIndex.Dump();
        }
        
        /// <summary>The VCN of the index block.</summary>
        internal ulong IndexBlockVcn;
        /// <summary>A DIRECTORY_INDEX structure.</summary>
        internal NtfsDirectoryIndex DirectoryIndex;
    }
}
