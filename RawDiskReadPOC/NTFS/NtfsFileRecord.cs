
namespace RawDiskReadPOC.NTFS
{
    /// <summary>An entry in the MFT consists of a FILE_RECORD_HEADER followed by a sequence of
    /// attributes.</summary>
    internal struct NtfsFileRecord
    {
        /// <summary>An NTFS_RECORD_HEADER structure with a Type of ‘FILE’.</summary>
        internal NtfsRecord Ntfs;
        /// <summary>The number of times that the MFT entry has been reused.</summary>
        internal ushort SequenceNumber;
        /// <summary>The number of directory links to the MFT entry.</summary>
        internal ushort LinkCount;
        /// <summary>The offset, in bytes, from the start of the structure to the first attribute
        /// of the MFT entry</summary>
        internal ushort AttributesOffset;
        /// <summary>A bit array of flags specifying properties of the MFT entry. The values defined
        /// include:0x0001 = InUse, 0x0002 = Directory</summary>
        internal ushort Flags;
        /// <summary>The number of bytes used by the MFT entry.</summary>
        internal uint BytesInUse;
        /// <summary>The number of bytes allocated for the MFT entry.</summary>
        internal uint BytesAllocated;
        /// <summary>If the MFT entry contains attributes that overflowed a base MFT entry, this
        /// member contains the file reference number of the base entry; otherwise, it contains
        /// zero.</summary>
        internal ulong BaseFileRecord;
        /// <summary>The number that will be assigned to the next attribute added to the MFT entry.
        /// </summary>
        internal ushort NextAttributeNumber;
    }
}
