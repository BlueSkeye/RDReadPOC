using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Until NTFS version 3.0, only filename attributes were indexed. If the HasTrailingVcn flag
    /// of a DIRECTORY_ENTRY structure is set, the last eight bytes of the directory entry contain the VCN
    /// of the index block that holds the entries immediately preceding the current entry</remarks>
    internal struct NtfsDirectoryEntry
    {
        internal bool HasSubNode
        {
            get { return 0 != (EntryFlags.HasSubNode & Flags); }
        }

        internal bool LastIndexEntry
        {
            get { return 0 != (EntryFlags.LastIndexInNode & Flags); }
        }

        internal unsafe void Dump()
        {
            Console.WriteLine("FRN 0x{0:X16}, Len {1}, AttrL {2}, Flgs 0x{3:X9}",
                FileReferenceNumber, DirectoryEntryLength, AttributeLength, Flags);
            if (!LastIndexEntry) {
            }
            if (HasSubNode) {
                fixed(ulong* anchor = &FileReferenceNumber) {
                    ulong* vcnAddress = (ulong*)((byte*)anchor + DirectoryEntryLength - sizeof(ulong));
                    Console.WriteLine("\tSub node VCN : 0x{0:X16}", *vcnAddress);
                }
            }
        }

        /// <summary>The file reference number of the file described by the directory entry</summary>
        internal ulong FileReferenceNumber;
        /// <summary>The size, in bytes, of the directory entry.</summary>
        internal ushort DirectoryEntryLength;
        /// <summary>The size, in bytes, of the attribute that is indexed</summary>
        internal ushort AttributeLength;
        /// <summary>A bit array of flags specifying properties of the entry. The values defined include
        /// HasTrailingVcn 0x0001 A VCN follows the indexed attribute
        /// LastEntry 0x0002 The last entry in an index block</summary>
        internal EntryFlags Flags;
        // FILENAME_ATTRIBUTE Name;
        // ULONGLONG Vcn; // VCN in IndexAllocation of earlier entries

        [Flags()]
        internal enum EntryFlags : uint
        {
            HasSubNode = 1,
            LastIndexInNode = 2
        }
    }
}
