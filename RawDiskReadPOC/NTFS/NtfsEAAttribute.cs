using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>NOTE: Can be resident or non-resident.
    /// Like the attribute list and the index buffer list, the EA attribute value is a sequence
    /// of NtfsEaAttribute variable length records.</summary>
    internal class NtfsEAAttribute
    {
        /// <summary>The number of bytes that must be skipped to get to the next entry.</summary>
        internal uint NextEntryOffset;
        /// <summary>A bit array of flags qualifying the extended attribute</summary>
        internal _Flags Flags;
        /// <summary>The size, in bytes, of the extended attribute name.
        /// Length of the name of the EA in bytes excluding the '\0' byte terminator.</summary>
        internal byte EaNameLength;
        /// <summary>The size, in bytes, of the extended attribute value.
        /// Byte size of the EA's value.</summary>
        internal ushort EaValueLength;
        // Name of the EA.Note this is ASCII, not Unicode and it is zero terminated.
        // byte EaName[];
        // The value of the EA.  Immediately follows the name.
        // UCHAR EaData[];

        [Flags()]
        internal enum _Flags : byte
        {
            /* If set the file to which the EA belongs cannot be interpreted 
             * understanding the associates extended attributes. */
            NEED_EA = 0x80,
        }
    }
}
