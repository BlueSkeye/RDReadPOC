
namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>NOTE: Always resident. (Is this true???)</remarks>
    internal class NtfsEAInformation
    {
        /// <summary>The size, in bytes, of the extended attribute information.
        /// Byte size of the packed extended attributes.</summary>
        internal ushort EaLength;
        /// <summary>The number of extended attributes which have the NEED_EA bit set.</summary>
        internal ushort NeedEaCount;
        /// <summary>The size, in bytes, of the buffer needed to query the extended attributes when
        /// calling ZwQueryEaFile.
        /// Byte size of the buffer required to query the extended attributes when calling
        /// ZwQueryEaFile() in Windows NT/2k. I.e. the byte size of the unpacked extended
        /// attributes.</summary>
        internal uint EaQueryLength;
    }
}
