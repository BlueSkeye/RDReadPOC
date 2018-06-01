
namespace RawDiskReadPOC.NTFS
{
    internal class NtfsEAAttribute
    {
        /// <summary>The number of bytes that must be skipped to get to the next entry.</summary>
        internal uint NextEntryOffset;
        /// <summary>A bit array of flags qualifying the extended attribute</summary>
        internal byte Flags;
        /// <summary>The size, in bytes, of the extended attribute name.</summary>
        internal byte EaNameLength;
        /// <summary>The size, in bytes, of the extended attribute value.</summary>
        internal ushort EaValueLength;
        // byte EaName[];
        // UCHAR EaData[];
    }
}
