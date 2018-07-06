using System;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    internal struct NtfsStandardInformationAttribute
    {
        internal void Dump()
        {
            Console.WriteLine("\tCR {0} ({1})",
                CreationTime, Helpers.DecodeTime(CreationTime));
            Console.WriteLine("\tCH {0} ({1})",
                ChangeTime, Helpers.DecodeTime(ChangeTime));
            Console.WriteLine("\tLW {0} ({1})",
                LastWriteTime, Helpers.DecodeTime(LastWriteTime));
            Console.WriteLine("\tLA {0} ({1})",
                LastAccessTime, Helpers.DecodeTime(LastAccessTime));
            Console.WriteLine("\tAttr {0} : {1}", FileAttributes, DecodeAttributes(FileAttributes));
            Console.WriteLine("\tMaxv {0}, V# {1}, Clsid {2}",
                    MaxVersionsCount, VersionNumber, ClassId);
            Console.WriteLine("\tQid {0}, Secid {1}, Qch {2}, Usn Ox{3:X8} ",
                    QuotaId, SecurityId, QuotaCharge, Usn);
            return;
        }

        internal static string DecodeAttributes(uint value)
        {
            StringBuilder result = new StringBuilder();
            foreach (uint enumValue in Enum.GetValues(typeof(Attributes))) {
                if (0 != (enumValue & value)) {
                    if (0 != result.Length) { result.Append(", "); }
                    result.Append(Enum.GetName(typeof(Attributes), enumValue));
                }
            }
            return (0 == result.Length) ? "NONE" : result.ToString();
        }

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
        internal uint MaxVersionsCount;
        internal uint VersionNumber;
        internal uint ClassId;
        /// <summary>A numeric identifier of the disk quota that has been charged for the file
        /// (probably an index into the file “\$Extend\$Quota”). If quotas are disabled, the value
        /// of QuotaId is zero. This member is only present in NTFS 3.0. If a volume has been upgraded
        /// from an earlier version of NTFS to version 3.0, this member is only present if the file has
        /// been accessed since the upgrade.</summary>
        internal uint QuotaId; // ~ owner ID
        /// <summary>A numeric identifier of the security descriptor that applies to the file (probably
        /// an index into the file “\$Secure”). This member is only present in NTFS 3.0. If a volume
        /// has been upgraded from an earlier version of NTFS to version 3.0, this member is only
        /// present if the file has been accessed since the upgrade.</summary>
        internal uint SecurityId;
        /// <summary>The size, in bytes, of the charge to the quota for the file. If quotas are disabled,
        /// the value of QuotaCharge is zero.This member is only present in NTFS 3.0. If a volume has
        /// been upgraded from an earlier version of NTFS to version 3.0, this member is only present
        /// if the file has been accessed since the upgrade.</summary>
        internal ulong QuotaCharge;
        /// <summary>The Update Sequence Number of the file. If journaling is not enabled, the value of
        /// Usn is zero.This member is only present in NTFS 3.0. If a volume has been upgraded from
        /// an earlier version of NTFS to version 3.0, this member is only present if the file has been
        /// accessed since the upgrade.</summary>
        internal ulong Usn;

        [Flags()]
        private enum Attributes : uint
        {
            ReadOnly = 0x0001,
            Hidden = 0x0002,
            System = 0x0004,
            Archive = 0x0020,
            Device = 0x0040,
            Normal = 0x0080,
            Temporary = 0x0100,
            SParseFile = 0x0200,
            ReparsePoint = 0x0400,
            Compressed = 0x0800,
            Offline = 0x1000,
            NotIndexed = 0x2000,
            Encrypted = 0x4000
        }
    }
}
