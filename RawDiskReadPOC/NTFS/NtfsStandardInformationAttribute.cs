using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawDiskReadPOC.NTFS
{
    internal class NtfsStandardInformationAttribute
    {
        /// <summary>The time when the file was created in the standard time format (that is,
        /// the number of 100-nanosecond intervals since January 1, 1601).</summary>
        internal ulong CreationTime;
        /// <summary>The time when the file attributes were last changed in the standard time
        /// format (that is, the number of 100-nanosecond intervals since January 1, 1601).</summary>
        internal ulong ChangeTime;
        /// <summary>LastWriteTime The time when the file was last written in the standard time
        /// format(that is, the number of 100-nanosecond intervals since January 1, 1601).</summary>
        internal ulong LastWriteTime;
        /// <summary>The time when the file was last accessed in the standard time format (that is,
        /// the number of 100-nanosecond intervals since January 1, 1601).</summary>
        internal ulong LastAccessTime;
        /// <summary>The attributes of the file. Defined attributes include: FILE_ATTRIBUTE_READONLY
        /// FILE_ATTRIBUTE_HIDDEN, FILE_ATTRIBUTE_SYSTEM, FILE_ATTRIBUTE_DIRECTORY,
        /// FILE_ATTRIBUTE_ARCHIVE, FILE_ATTRIBUTE_NORMAL, FILE_ATTRIBUTE_TEMPORARY,
        /// FILE_ATTRIBUTE_SPARSE_FILE, FILE_ATTRIBUTE_REPARSE_POINT, FILE_ATTRIBUTE_COMPRESSED,
        /// FILE_ATTRIBUTE_OFFLINE, FILE_ATTRIBUTE_NOT_CONTENT_INDEXED, FILE_ATTRIBUTE_ENCRYPTED</summary>
        internal uint FileAttributes;
        internal uint Alignment1;
        internal uint Alignment2;
        internal uint Alignment3;
        /// <summary>A numeric identifier of the disk quota that has been charged for the file
        /// (probably an index into the file “\$Extend\$Quota”). If quotas are disabled, the value
        /// of QuotaId is zero. This member is only present in NTFS 3.0. If a volume has been upgraded
        /// from an earlier version of NTFS to version 3.0, this member is only present if the file has
        /// been accessed since the upgrade.</summary>
        internal uint QuotaId;
        /// <summary>A numeric identifier of the security descriptor that applies to the file (probably
        /// an index into the file “\$Secure”). This member is only present in NTFS 3.0. If a volume
        /// has been upgraded from an earlier version of NTFS to version 3.0, this member is only
        /// present if the file has been accessed since the upgrade.</summary>
        internal uint SecurityId;
        /// <summary>The size, in bytes, of the charge to the quota for the file. If quotas are disabled,
        /// the value of QuotaCharge is zero.This member is only present in NTFS 3.0. If a volume has
        /// been upgraded from an earlier version of NTFS to version 3.0, this member is only present        /// if the file has been accessed since the upgrade.</summary>
        internal ulong QuotaCharge;
        /// <summary>The Update Sequence Number of the file. If journaling is not enabled, the value of
        /// Usn is zero.This member is only present in NTFS 3.0. If a volume has been upgraded from
        /// an earlier version of NTFS to version 3.0, this member is only present if the file has been        /// accessed since the upgrade.</summary>
        internal ulong Usn;
    }
}
