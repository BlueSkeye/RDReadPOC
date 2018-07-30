using System;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>NOTE: Always resident.
    /// NOTE: All fields, except the parent_directory, are only updated when the filename is
    /// changed.Until then, they just become out of sync with reality and the more up to date
    /// values are present in the standard information attribute.
    /// NOTE: There is conflicting information about the meaning of each of the time fields
    /// but the meaning as defined below has been verified to be correct by practical
    /// experimentation on Windows NT4 SP6a and is hence assumed to be the one and only correct
    /// interpretation.</remarks>
    internal unsafe struct NtfsFileNameAttribute
    {
        internal void Dump()
        {
            Console.WriteLine(Helpers.Indent(1) + "RefNum 0x{0:X8}",
                DirectoryFileReferenceNumber);
            Console.WriteLine(Helpers.Indent(1) + "CR {0} ({1})",
                CreationTime, Helpers.DecodeTime(CreationTime));
            Console.WriteLine(Helpers.Indent(1) + "CH {0} ({1})",
                ChangeTime, Helpers.DecodeTime(ChangeTime));
            Console.WriteLine(Helpers.Indent(1) + "LW {0} ({1})",
                LastWriteTime, Helpers.DecodeTime(LastWriteTime));
            Console.WriteLine(Helpers.Indent(1) + "LA {0} ({1})",
                LastAccessTime, Helpers.DecodeTime(LastAccessTime));
            Console.WriteLine(Helpers.Indent(1) + "Alloc {0}, Size {1}",
                AllocatedSize, DataSize);
            Console.WriteLine(Helpers.Indent(1) + "Attr {0} : {1}",
                FileAttributes, NtfsStandardInformationAttribute.DecodeAttributes((uint)FileAttributes));
            Console.WriteLine(Helpers.Indent(1) + "NL {0}, Ty {1} ({2})",
                NameLength, NameType, GetName());
        }

        internal unsafe string GetName()
        {
            if (0 == NameLength) { return string.Empty; }
            fixed (byte* nameBuffer = &this.Name) {
                int nameLength = this.NameLength;
                for (int index = nameLength - 1; 0 <= index; index--) {
                    char scannedCharacter = ((char*)nameBuffer)[index];
                    if (0 == scannedCharacter) { nameLength = index; }
                }
                int bytesCount = sizeof(char) * nameLength;
                return Encoding.Unicode.GetString(nameBuffer, bytesCount);
            }
        }

        // TODO : Provide 2 properties for dir file ref number splitting.
        /// <summary>The file reference number of the directory in which the filename is entered.
        /// This is a composite number. The first 6 bytes are a parent reference number while the 2 lower
        /// bytes are a sequence number within the parent record.
        /// Directory this filename is referenced from.</summary>
        internal ulong DirectoryFileReferenceNumber;
        /// <summary>The time when the file was created in the standard time format (that is. the number
        /// of 100-nanosecond intervals since January 1, 1601). This member is only updated when the
        /// filename changes and may differ from the field of the same name in the STANDARD_INFORMATION
        /// structure.
        /// Time file was created.</summary>
        internal ulong CreationTime;
        /// <summary>The time when the file attributes were last changed in the standard time format (that
        /// is, the number of 100-nanosecond intervals since January 1, 1601). This member is only updated
        /// when the filename changes and may differ from the field of the same name in the
        /// NtfsStandardInformationAttribute structure.
        /// Time the data attribute was last modified.</summary>
        internal ulong ChangeTime;
        /// <summary>The time when the file was last written in the standard time format (that is, the
        /// number of 100-nanosecond intervals since January 1, 1601). This member is only updated when
        /// the filename changes and may differ from the field of the same name in the STANDARD_INFORMATION
        /// structure.
        /// Time this mft record was last modified.</summary>
        internal ulong LastWriteTime;
        /// <summary>The time when the file was last accessed in the standard time format (that is, the
        /// number of 100-nanosecond intervals since January 1, 1601). This member is only updated when
        /// the filename changes and may differ from the field of the same name in the STANDARD_INFORMATION
        /// structure.
        /// Time this mft record was last accessed.</summary>
        internal ulong LastAccessTime;
        /// <summary>The size, in bytes, of disk space allocated to hold the attribute value. This member
        /// is only updated when the filename changes.
        /// Byte size of allocated space for the data attribute.NOTE: Is a multiple of the cluster size.</summary>
        internal ulong AllocatedSize;
        /// <summary>The size, in bytes, of the attribute value. This member is only updated when the
        /// filename changes.
        /// Byte size of actual data in data attribute.</summary>
        internal ulong DataSize;
        /// <summary>The attributes of the file.This member is only updated when the filename changes and
        /// may differ from the field of the same name in the STANDARD_INFORMATION structure.</summary>
        internal NtfsStandardInformationAttribute._FileAttributes FileAttributes;
        /// <summary>Either a ushort size of the buffer needed to pack the extended attributes
        /// (EAs), if such are present
        /// Or a uint type of reparse point, present only in reparse points and only if there are no EAs.</summary>
        internal uint PackedEASizeOrReparsePointTag;
        /// <summary>The size, in characters, of the filename
        /// Length of filename in (Unicode) characters.</summary>
        internal byte NameLength;
        /// <summary>The type of the name. A type of zero indicates an ordinary name, a type of one
        /// indicates a long name corresponding to a short name, and a type of two indicates a short name
        /// corresponding to a long name.
        /// Namespace of the filename.</summary>
        internal Namespacves NameType;
        /// <summary>The name, in Unicode, of the file</summary>
        internal byte Name;

        /// <summary>Possible namespaces for filenames in ntfs (8-bit).</summary>
        internal enum Namespacves : byte
        {
            /// <summary>This is the largest namespace. It is case sensitive and allows all Unicode
            /// characters except for: '\0' and '/'.  Beware that in WinNT/2k/2003 by default files
            /// which eg have the same name except for their case will not be distinguished by the
            /// standard utilities and thus a "del filename" will delete both "filename" and "fileName"
            /// without warning.  However if for example Services For Unix (SFU) are installed and
            /// the case sensitive option was enabled at installation time, then you can
            /// create/access/delete such files. Note that even SFU places restrictions on the filenames
            /// beyond the '\0' and '/' and in particular the following set of characters is not
            /// allowed: '"', '/', '<', '>', '\'.  All other characters, including the ones no allowed
            /// in WIN32 namespace are allowed. Tested with SFU 3.5 (this is now free) running on
            /// Windows XP.</summary>
            Posix = 0x00,
            /// <summary>The standard WinNT/2k NTFS long filenames. Case insensitive. All Unicode
            /// chars except: '\0', '"', '*', '/', ':', '<', '>', '?', '\', and '|'.  Further, names
            /// cannot end with a '.' or a space.</summary>
            Win32 = 0x01,
            /// <summary>The standard DOS filenames (8.3 format). Uppercase only. All 8-bit characters
            /// greater space, except: '"', '*', '+', ',', '/', ':', ';', '<', '=', '>', '?', and '\'.</summary>
            DOS = 0x02,
            /// <summary>Both the Win32 and the DOS filenames are identical and hence have been
            /// saved in this single filename record.</summary>
            Win32AndDOS = 0x03,
        }
    }
}
