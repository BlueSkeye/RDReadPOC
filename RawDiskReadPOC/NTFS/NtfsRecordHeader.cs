
namespace RawDiskReadPOC.NTFS
{
    /// <summary>http://ultradefrag.sourceforge.net/doc/man/ntfs/NTFS_On_Disk_Structure.pdf</summary>
    internal struct NtfsRecordHeader
    {
        /// <summary>The type of NTFS record.When the value of Type is considered as a sequence of
        /// four one-byte characters, it normally spells an acronym for the type. Defined values
        /// include: ‘FILE’, ‘INDX’, ‘BAAD’, ‘HOLE’, ‘CHKD’</summary>
        internal uint Type;
        /// <summary>The offset, in bytes, from the start of the structure to the Update Sequence
        /// Array</summary>
        internal ushort UsaOffset;
        /// <summary>The number of values in the Update Sequence Array</summary>
        internal ushort UsaCount;
        /// <summary>The Update Sequence Number of the NTFS record.</summary>
        internal ulong Usn;
    }
}