using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Windows 2000 formats new volumes as NTFS version 3.0.Windows NT 4.0 formats new volumes
    /// as NTFS version 2.1.
    /// NOTE: Always resident.
    /// NOTE: Present only in FILE_Volume.
    /// NOTE: Windows 2000 uses NTFS 3.0 while Windows NT4 service pack 6a uses
    /// NTFS 1.2. I haven't personally seen other values yet.</remarks>
    internal class NtfsVolumeInformationAttribute
    {
        internal ulong _filler1;
        /// <summary>The major version number of the NTFS format.</summary>
        internal byte MajorVersion;
        /// <summary>The minor version number of the NTFS format.</summary>
        internal byte MinorVersion;
        /// <summary>A bit array of flags specifying properties of the volume. The values defined
        /// include: VolumeIsDirty 0x0001</summary>
        internal VolumeFlags Flags;

        /// <summary>Possible flags for the volume (16-bit). VOLUME_CHKDSK_APPLIED_FIXES -
        /// When this bit is set it means that chkdsk was run and it applied fixes to the
        /// volume and most importantly it means that the chkdsk has completed, thus we
        /// can ignore this bit when mounting.If the NTFS driver is expected to do anything
        /// then the journal is left in a dirty state which we detect when parsing the journal
        /// later on in the mount process.</summary>
        [Flags()]
        internal enum VolumeFlags : ushort
        {
            IsDirty = 0x0001,
            ResizeLogFile = 0x0002,
            UpgradeOnMount = 0x0004,
            MountedOnNT4 = 0x0008,
            DeleteUsnUnderway = 0x0010,
            RepairObjectId = 0x0020,
            ChkdskAppliedFixes = 0x4000,
            ModifiedByChkdsk = 0x8000,

            FlagsMask = 0xC03F,

            /// <summary>To make our life easier when checking if we must mount read-only.</summary>
            MustMountReadOnlyMask = 0x0022,
        }
    }
}
