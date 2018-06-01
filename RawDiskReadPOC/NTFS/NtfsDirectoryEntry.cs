using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Until NTFS version 3.0, only filename attributes were indexed. If the HasTrailingVcn flag
    /// of a DIRECTORY_ENTRY structure is set, the last eight bytes of the directory entry contain the VCN
    /// of the index block that holds the entries immediately preceding the current entry</remarks>
    internal class NtfsDirectoryEntry
    {
        /// <summary>The file reference number of the file described by the directory entry</summary>
        internal ulong FileReferenceNumber;
        /// <summary>The size, in bytes, of the directory entry.</summary>
        internal ushort Length;
        /// <summary>The size, in bytes, of the attribute that is indexed</summary>
        internal ushort AttributeLength;
        /// <summary>A bit array of flags specifying properties of the entry. The values defined include
        /// HasTrailingVcn 0x0001 A VCN follows the indexed attribute
        /// LastEntry 0x0002 The last entry in an index block</summary>
        internal uint Flags; 
        // FILENAME_ATTRIBUTE Name;
        // ULONGLONG Vcn; // VCN in IndexAllocation of earlier entries
    }
}
