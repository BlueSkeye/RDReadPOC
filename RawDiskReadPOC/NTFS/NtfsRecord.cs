using System;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>http://ultradefrag.sourceforge.net/doc/man/ntfs/NTFS_On_Disk_Structure.pdf</summary>
    internal struct NtfsRecord
    {
        internal unsafe void ApplyFixups(uint usedBytesCount)
        {
            ushort fixupCount = UsaCount;
            uint bytesPerSector = NtfsPartition.Current.BytesPerSector;
            fixed(NtfsRecord* nativeRecord = &this) {
                ushort fullSectorCount = (ushort)(usedBytesCount / bytesPerSector);
                if (0 < fixupCount) {
                    if (fullSectorCount > fixupCount) {
                        throw new ApplicationException("Not enough fixups.");
                    }
                    // Adjust fixup count to effective record size.
                    ushort* pFixup = (ushort*)((byte*)nativeRecord + UsaOffset);
                    ushort fixupTag = *(pFixup++);
                    byte* fixLocation = (byte*)nativeRecord + bytesPerSector - sizeof(ushort);
                    for (ushort fixupIndex = 0; fixupIndex < fullSectorCount; fixupIndex++, fixLocation += bytesPerSector) {
                        if (*((ushort*)fixLocation) != fixupTag) {
                            throw new ApplicationException();
                        }
                        *((ushort*)fixLocation) = *(pFixup++);
                    }
                }
                else {
                    if (1 <= fullSectorCount) {
                        throw new ApplicationException("Required fixups are missing.");
                    }
                }
            }
        }

        internal unsafe void Dump()
        {
            // TODO Verify inefficient decoding of the name.
            byte[] nameBytes = new byte[4];
            nameBytes[0] = (byte)(Type % 256);
            nameBytes[1] = (byte)((Type >>8) % 256);
            nameBytes[2] = (byte)((Type >> 16) % 256);
            nameBytes[3] = (byte)((Type >> 24) % 256);
            Console.WriteLine("{0}, off {1}, cnt {2}, usn {3}",
                Encoding.ASCII.GetString(nameBytes), UsaOffset, UsaCount, Usn);
            if (0 < UsaCount) {
                fixed(NtfsRecord* rawRecord = &this) {
                    ushort* pFixup = (ushort*)((byte*)rawRecord + UsaOffset);
                    Console.Write("Fixup : {0:X4} => ", *(pFixup++));
                    for(int index = 0; index < UsaCount; index++) {
                        if (0 != index) { Console.Write(", "); }
                        Console.Write("{0:X4}", *(pFixup++));
                    }
                    Console.WriteLine();
                }
            }
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