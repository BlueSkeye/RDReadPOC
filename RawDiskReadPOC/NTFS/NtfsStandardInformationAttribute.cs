using System;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>
    /// NOTE: Always resident.
    /// NOTE: Present in all base file records on a volume.
    /// NOTE on times in NTFS: All times are in MS standard time format, i.e.they are
    /// the number of 100-nanosecond intervals since 1st January 1601, 00:00:00 universal
    /// coordinated time(UTC). (In Linux time starts 1st January 1970, 00:00:00 UTC and
    /// is stored as the number of 1-second intervals since then.)
    /// NOTE: There is conflicting information about the meaning of each of the time
    /// fields but the meaning as defined below has been verified to be correct by
    /// practical experimentation on Windows NT4 SP6a and is hence assumed to be the
    /// one and only correct interpretation.</remarks>
    internal struct NtfsStandardInformationAttribute
    {
        internal void Dump()
        {
            Console.WriteLine(Helpers.Indent(1) + "CR {0} ({1})",
                CreationTime, Helpers.DecodeTime(CreationTime));
            Console.WriteLine(Helpers.Indent(1) + "CH {0} ({1})",
                ChangeTime, Helpers.DecodeTime(ChangeTime));
            Console.WriteLine(Helpers.Indent(1) + "LW {0} ({1})",
                LastWriteTime, Helpers.DecodeTime(LastWriteTime));
            Console.WriteLine(Helpers.Indent(1) + "LA {0} ({1})",
                LastAccessTime, Helpers.DecodeTime(LastAccessTime));
            Console.WriteLine(Helpers.Indent(1) + "Attr {0} : {1}",
                FileAttributes, DecodeAttributes((uint)FileAttributes));
            Console.WriteLine(Helpers.Indent(1) + "Maxv {0}, V# {1}, Clsid {2}",
                    MaxVersionsCount, VersionNumber, ClassId);
            Console.WriteLine(Helpers.Indent(1) + "Qid {0}, Secid {1}, Qch {2}, Usn Ox{3:X8} ",
                    QuotaId, SecurityId, QuotaCharge, Usn);
            return;
        }

        internal static string DecodeAttributes(uint value)
        {
            StringBuilder result = new StringBuilder();
            foreach (uint enumValue in Enum.GetValues(typeof(_FileAttributes))) {
                if (0 != (enumValue & value)) {
                    if (0 != result.Length) { result.Append(", "); }
                    result.Append(Enum.GetName(typeof(_FileAttributes), enumValue));
                }
            }
            return (0 == result.Length) ? "NONE" : result.ToString();
        }

        /// <summary>The time when the file was created in the standard time format (that is,
        /// the number of 100-nanosecond intervals since January 1, 1601). Updated when a
        /// filename is changed(?).</summary>
        internal ulong CreationTime;
        /// <summary>The time when the file attributes were last changed in the standard time
        /// format (that is, the number of 100-nanosecond intervals since January 1, 1601).</summary>
        internal ulong ChangeTime;
        /// <summary>The time when the file was last written in the standard time format(that
        /// is, the number of 100-nanosecond intervals since January 1, 1601). Time this mft
        /// record was last modified.</summary>
        internal ulong LastWriteTime;
        /// <summary>The time when the file was last accessed in the standard time format (that
        /// is, the number of 100-nanosecond intervals since January 1, 1601). Approximate time
        /// when the file was last accessed(obviously this is not updated on read-only volumes).
        /// In indows this is only updated when accessed if some time delta has passed since the
        /// last update.Also, last access times updates can be disabled altogether for speed.</summary>
        internal ulong LastAccessTime;
        /// <summary>The attributes of the file.</summary>
        internal _FileAttributes FileAttributes;
        // ----------------------- Since NTFS 3.x -------------------
        // If a volume has been upgraded from a previous NTFS version, then these fields are
        // present only if the file has been accessed since the upgrade. Recognize the difference
        // by comparing the length of the resident attribute value. If it is 48, then the following
        // fields are missing. If it is 72 then the fields are present.
        // Only problem is that it might be legal to set the length of the value to arbitrarily
        // large values thus spoiling this check. - But chkdsk probably views that as a corruption,
        // assuming that it behaves like this for all attributes.
        /// <summary>Maximum allowed versions for file. Zero if version numbering is disabled.</summary>
        internal uint MaxVersionsCount;
        /// <summary>This file's version (if any). Set to zero if maximum_versions is zero.</summary>
        internal uint VersionNumber;
        /// <summary>Class id from bidirectional class id index(?).</summary>
        internal uint ClassId;
        /// <summary>A numeric identifier of the disk quota that has been charged for the file
        /// (probably an index into the file “\$Extend\$Quota”). If quotas are disabled, the value
        /// of QuotaId is zero. This member is only present in NTFS 3.0. If a volume has been upgraded
        /// from an earlier version of NTFS to version 3.0, this member is only present if the file has
        /// been accessed since the upgrade.
        /// Owner_id of the user owning the file.Translate via $Q index in FILE_Extend/$Quota to
        /// the quota control entry for the user owning the file.Zero if quotas are disabled.</summary>
        internal uint QuotaId; // ~ owner ID
        /// <summary>A numeric identifier of the security descriptor that applies to the file (probably
        /// an index into the file “\$Secure”). This member is only present in NTFS 3.0. If a volume
        /// has been upgraded from an earlier version of NTFS to version 3.0, this member is only
        /// present if the file has been accessed since the upgrade.
        /// Security_id for the file. Translate via $SII index and $SDS data stream in FILE_Secure
        /// to the security descriptor.</summary>
        internal uint SecurityId;
        /// <summary>The size, in bytes, of the charge to the quota for the file. If quotas are disabled,
        /// the value of QuotaCharge is zero.This member is only present in NTFS 3.0. If a volume has
        /// been upgraded from an earlier version of NTFS to version 3.0, this member is only present
        /// if the file has been accessed since the upgrade.
        /// Byte size of the charge to the quota for all streams of the file.Note: Is zero if quotas
        /// are disabled.</summary>
        internal ulong QuotaCharge;
        /// <summary>The Update Sequence Number of the file. If journaling is not enabled, the value of
        /// Usn is zero.This member is only present in NTFS 3.0. If a volume has been upgraded from
        /// an earlier version of NTFS to version 3.0, this member is only present if the file has been
        /// accessed since the upgrade.
        /// Last update sequence number of the file.This is a direct index into the transaction log
        /// file($UsnJrnl). It is zero if the usn journal is disabled or this file has not been subject
        /// to logging yet.See usnjrnl.h for details.</summary>
        internal ulong Usn;

        /// <summary>File attribute flags(32-bit) appearing in the file_attributes fields of
        /// the NtfsStandardInformationAttribute of MFT_RECORDs and the NtfsFilenameAttribute
        /// of MFT_RECORDs and directory index entries.
        /// All of the below flags appear in the directory index entries but only some appear
        /// in the NtfsStandardInformationAttribute .Unless otherwise stated the flags appear
        /// in all of the above.</summary>
        [Flags()]
        internal enum _FileAttributes : uint
        {
            ReadOnly = 0x0001,
            Hidden = 0x0002,
            System = 0x0004,
            DOSVolumeId = 0x0008, // Unused in NT.
            Directory = 0x0010, // Note, not considered valid in NT. It is reserved for the DOS SUBDIRECTORY flag.
            Archive = 0x0020, // Note, only valid/settable on files and not on directories which always have the bit cleared.
            Device = 0x0040,
            Normal = 0x0080,
            Temporary = 0x0100,
            SparseFile = 0x0200,
            ReparsePoint = 0x0400,
            Compressed = 0x0800,
            Offline = 0x1000,
            NotIndexed = 0x2000,
            Encrypted = 0x4000,

            ValidFlags = 0x7FB7, // Note, masks out the old DOSVolumeId and the Device and preserves everything else.
                                 // This mask is used to obtain all flags that are valid for reading.
            ValidSerFlags = 0x31A7, // Note, masks out the old DOSVolumeId, the Device, Directory, SparseFile,
                                    // ReparsePoint, Compressed and Encrypted and preserves the rest. This mask is used
                                    // to obtain all flags that are valid for setting.
            DuplicateFilenamePresent = 0x10000000, // The flag is present in all NtfsFileNameAttribute attributes but
                                                   // not in the NtfsStandardInformationAttribute of an mft record.
                                                   // Note, this is a copy of the corresponding bit from the mft record,
                                                   // telling us whether this is a directory or not, i.e. whether it has
                                                   // an index root attribute or not.
            DuplicateViewIndexPresent = 0x20000000, // Note, this is a copy of the corresponding bit from the mft record,
                                                    // telling us whether this file has a view index present (eg. object
                                                    // id index, quota index, one of the security indexes or the encrypting
                                                    // filesystem related indexes).
        }
    }
}
