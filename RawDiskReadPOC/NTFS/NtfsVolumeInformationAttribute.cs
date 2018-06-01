
namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Windows 2000 formats new volumes as NTFS version 3.0.Windows NT 4.0 formats new volumes
    /// as NTFS version 2.1.</remarks>
    internal class NtfsVolumeInformationAttribute
    {
        internal uint Unknown1;
        internal uint Unknown2;
        /// <summary>The major version number of the NTFS format.</summary>
        internal byte MajorVersion;
        /// <summary>The minor version number of the NTFS format.</summary>
        internal byte MinorVersion;
        /// <summary>A bit array of flags specifying properties of the volume. The values defined
        /// include: VolumeIsDirty 0x0001</summary>
        internal ushort Flags;
    }
}
