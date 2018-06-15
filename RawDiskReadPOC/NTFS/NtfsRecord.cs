using System;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>http://ultradefrag.sourceforge.net/doc/man/ntfs/NTFS_On_Disk_Structure.pdf</summary>
    internal struct NtfsRecord
    {
        internal void Dump()
        {
            // TODO Verify inefficient decoding of the name.
            byte[] nameBytes = new byte[4];
            nameBytes[0] = (byte)(Type % 256);
            nameBytes[1] = (byte)((Type >>8) % 256);
            nameBytes[2] = (byte)((Type >> 16) % 256);
            nameBytes[3] = (byte)((Type >> 24) % 256);
            Console.WriteLine("{0}, off {1}, cnt {2}, usn {3}",
                Encoding.ASCII.GetString(nameBytes), UsaOffset, UsaCount, Usn);
        }
        
        /// <summary>The type of NTFS record.When the value of Type is considered as a sequence of
        /// four one-byte characters, it normally spells an acronym for the type. Defined values
        /// include: ‘FILE’, ‘INDX’, ‘BAAD’, ‘HOLE’, ‘CHKD’</summary>
        internal uint Type;
        /// <summary>The offset, in bytes, from the start of the structure to the Update Sequence
        /// Array</summary>
        internal ushort UsaOffset;
        /// <summary>The number of values in the Update Sequence Array</summary>
        internal ushort UsaCount;
        /// <summary>The Update Sequence Number of the NTFS record.</summary>
        internal ulong Usn;
    }
}