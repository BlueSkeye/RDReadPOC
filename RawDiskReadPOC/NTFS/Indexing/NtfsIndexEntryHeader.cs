using System;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC.NTFS.Indexing
{
    /// <summary>This is an index entry. A sequence of such entries follows each INDEX_HEADER
    /// structure.Together they make up a complete index. The index follows either an index
    /// root attribute or an index allocation attribute.
    /// NOTE: Before NTFS 3.0 only filename attributes were indexed.</summary>
    /// <remarks>Until NTFS version 3.0, only filename attributes were indexed. If the HasTrailingVcn
    /// flag of a DIRECTORY_ENTRY structure is set, the last eight bytes of the directory entry
    /// contain the VCN of the index block that holds the entries immediately preceding the current
    /// entry.</remarks>
    [StructLayout(LayoutKind.Explicit)]
    internal struct NtfsIndexEntryHeader
    {
        /// <summary>If INDEX_ENTRY_NODE bit in flags is set, the last eight bytes of this index entry
        /// contain the virtual cluster number of the index block that holds the entries immediately
        /// preceding the current entry (the vcn references the corresponding cluster in the data of
        /// the non-resident index allocation attribute). If the key_length is zero, then the vcn
        /// immediately follows the INDEX_ENTRY_HEADER. Regardless of key_length, the address of the
        /// 8-byte boundary aligned vcn of INDEX_ENTRY{_HEADER} *ie is given by
        /// (char*)ie + le16_to_cpu(ie*)->length) - sizeof(VCN),
        /// where sizeof(VCN) can be hardcoded as 8 if wanted.</summary>
        internal unsafe ulong ChildNodeVCN
        {
            get
            {
                // The child entry VCN is an ulong at the very end of the entry.
                fixed (NtfsIndexEntryHeader* pEntry = &this) {
                    return *(ulong*)((byte*)pEntry + pEntry->EntryLength - sizeof(ulong));
                }
            }
        }

        internal bool HasSubNode
        {
            get { return 0 != (EntryFlags.HasTrailingVCN & Flags); }
        }

        internal bool LastIndexEntry
        {
            get { return 0 != (EntryFlags.LastIndexInNode & Flags); }
        }

        internal unsafe void Dump()
        {
            Console.WriteLine(Helpers.Indent(3) + "FRef 0x{0:X16}, Len {1}, AttrL {2}, Flgs 0x{3:X} {4}",
                FileReference, EntryLength, KeyLength, Flags, LastIndexEntry ? "LAST" : string.Empty);
            if (HasSubNode) {
                fixed(NtfsIndexEntryHeader* pThis = &this) {
                    ulong* pChildVCN = (ulong*)(((byte*)pThis + sizeof(NtfsIndexEntryHeader)) + EntryLength - sizeof(ulong));
                    Console.WriteLine(Helpers.Indent(3) + "ChildVCN 0x{0:X8}",
                        *pChildVCN);
                }
            }
            else {
                Console.WriteLine(Helpers.Indent(3) + "No child");
            }
        }

        // ----------------- Start of UNION ------------------
        /// <summary>Only valid when INDEX_ENTRY_END is not set. The mft reference of the file
        /// described by this index entry. Used for directory indexes.</summary>
        [FieldOffset(0)]
        internal ulong FileReference;

        /// <summary>Used for views/indexes to find the entry's data. Data byte offset from this
        /// INDEX_ENTRY. Follows theindex key.</summary>
        [FieldOffset(0)]
        internal ushort DataOffset;
        /// <summary>Data length in bytes.</summary>
        [FieldOffset(2)]
        internal ushort DataLength;
        // ----------------- End of UNION ------------------

        /// <summary>Byte size of this index entry, multiple of 8-bytes.</summary>
        [FieldOffset(8)]
        internal ushort EntryLength;
        /// <summary>Byte size of the key value, which is in the index entry. It follows field
        /// reserved. Not multiple of 8-bytes.</summary>
        [FieldOffset(10)]
        internal ushort KeyLength;
        /// <summary>A bit array of flags specifying properties of the entry. The values defined include
        /// HasTrailingVcn 0x0001 A VCN follows the indexed attribute
        /// LastEntry 0x0002 The last entry in an index block</summary>
        [FieldOffset(12)]
        internal EntryFlags Flags;
        [FieldOffset(14)]
        internal ushort _filler1;

        [Flags()]
        internal enum EntryFlags : ushort
        {
            /// <summary>This entry contains a sub-node, i.e. a reference to an index
            /// block in form of a virtual cluster number(see below).</summary>
            HasTrailingVCN = 1,
            /// <summary>This signifies the last entry in an index block. The index entry
            /// does not represent a file but it can point to a sub-node.</summary>
            LastIndexInNode = 2
        }
    }
}
