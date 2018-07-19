using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Until NTFS version 3.0, only filename attributes were indexed. If the HasTrailingVcn
    /// flag of a DIRECTORY_ENTRY structure is set, the last eight bytes of the directory entry
    /// contain the VCN of the index block that holds the entries immediately preceding the current
    /// entry.</remarks>
    internal struct NtfsIndexEntry
    {
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
            Console.WriteLine("\t\t\tLen {0}, AttrL {1}, Flgs 0x{2:X} {3}",
                EntryLength, ContentLength, Flags, LastIndexEntry ? "LAST" : string.Empty);
            if (HasSubNode) {
                fixed(NtfsIndexEntry* pThis = &this) {
                    ulong* pChildVCN = (ulong*)(((byte*)pThis + sizeof(NtfsIndexEntry)) + EntryLength - sizeof(ulong));
                    Console.WriteLine("\t\t\tChildVCN 0x{0:X8}",
                        *pChildVCN);
                }
            }
            else {
                Console.WriteLine("\t\t\tNo child");
            }
        }

        internal ulong _undefined;
        /// <summary>The size, in bytes, of this entry.</summary>
        internal ushort EntryLength;
        /// <summary>The size, in bytes, of the attribute that is indexed</summary>
        internal ushort ContentLength;
        /// <summary>A bit array of flags specifying properties of the entry. The values defined include
        /// HasTrailingVcn 0x0001 A VCN follows the indexed attribute
        /// LastEntry 0x0002 The last entry in an index block</summary>
        internal EntryFlags Flags;

        [Flags()]
        internal enum EntryFlags : uint
        {
            HasTrailingVCN = 1,
            LastIndexInNode = 2
        }
    }
}
