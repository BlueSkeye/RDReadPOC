
namespace RawDiskReadPOC.NTFS
{
    internal class NtfsEAInformation
    {
        /// <summary>The size, in bytes, of the extended attribute information.</summary>
        internal uint EaLength;
        /// <summary>The size, in bytes, of the buffer needed to query the extended attributes when
        /// calling ZwQueryEaFile.</summary>
        internal uint EaQueryLength;
    }
}
