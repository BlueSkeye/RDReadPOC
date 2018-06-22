using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>The filename attribute is always resident.</remarks>
    internal unsafe struct NtfsFileNameAttribute
    {
        internal NtfsResidentAttribute BaseAttribute;
        /// <summary>The file reference number of the directory in which the filename is entered</summary>
        internal ulong DirectoryFileReferenceNumber;
        /// <summary>The time when the file was created in the standard time format (that is. the number
        /// of 100-nanosecond intervals since January 1, 1601). This member is only updated when the
        /// filename changes and may differ from the field of the same name in the STANDARD_INFORMATION
        /// structure.</summary>
        internal ulong CreationTime;
        /// <summary>The time when the file attributes were last changed in the standard time format (that
        /// is, the number of 100-nanosecond intervals since January 1, 1601). This member is only updated
        /// when the filename changes and may differ from the field of the same name in the
        /// STANDARD_INFORMATION structure.</summary>
        internal ulong ChangeTime;
        /// <summary>The time when the file was last written in the standard time format (that is, the
        /// number of 100-nanosecond intervals since January 1, 1601). This member is only updated when
        /// the filename changes and may differ from the field of the same name in the STANDARD_INFORMATION
        /// structure.</summary>
        internal ulong LastWriteTime;
        /// <summary>The time when the file was last accessed in the standard time format (that is, the
        /// number of 100-nanosecond intervals since January 1, 1601). This member is only updated when
        /// the filename changes and may differ from the field of the same name in the STANDARD_INFORMATION
        /// structure.</summary>
        internal ulong LastAccessTime;
        /// <summary>The size, in bytes, of disk space allocated to hold the attribute value. This member
        /// is only updated when the filename changes.</summary>
        internal ulong AllocatedSize;
        /// <summary>The size, in bytes, of the attribute value. This member is only updated when the
        /// filename changes.</summary>
        internal ulong DataSize;
        /// <summary>The attributes of the file.This member is only updated when the filename changes and
        /// may differ from the field of the same name in the STANDARD_INFORMATION structure.</summary>
        internal uint FileAttributes;
        internal uint Alignment1;
        /// <summary>The size, in characters, of the filename</summary>
        internal byte NameLength;
        /// <summary>The type of the name. A type of zero indicates an ordinary name, a type of one
        /// indicates a long name corresponding to a short name, and a type of two indicates a short name
        /// corresponding to a long name.</summary>
        internal byte NameType;
        /// <summary>The name, in Unicode, of the file</summary>
        internal byte Name;

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
    }
}
