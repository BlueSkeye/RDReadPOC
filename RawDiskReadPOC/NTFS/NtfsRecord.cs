using System;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>http://ultradefrag.sourceforge.net/doc/man/ntfs/NTFS_On_Disk_Structure.pdf</summary>
    internal struct NtfsRecord
    {
        internal unsafe void ApplyFixups()
        {
            // TODO : Should track already fixed records otherwise we encounter spurious fixup
            // mismatches. Tracked record should be reset on Disposal.
            NtfsPartition partition = NtfsPartition.Current;
            uint bytesPerSector = partition.BytesPerSector;
            uint sectorsPerCluster = partition.SectorsPerCluster;
            ushort fixupCount = UsaCount;
            fixed(NtfsRecord* nativeRecord = &this) {
                // Check magic number on every sector.
                byte* fixLocation = (byte*)nativeRecord + bytesPerSector - sizeof(ushort);
                ushort* pFixup = (ushort*)((byte*)nativeRecord + UsaOffset);
                ushort fixupTag = *(pFixup++);
                for (int sectorIndex = 0; sectorIndex < fixupCount; sectorIndex++, fixLocation += bytesPerSector) {
                    ushort fixedValue = *((ushort*)fixLocation);
                    if (fixedValue != fixupTag) {
                        Console.WriteLine("WARNING : fixup tag mismatch.");
                    }
                }
                // Apply those fixups that are defined.
                fixLocation = (byte*)nativeRecord + bytesPerSector - sizeof(ushort);
                pFixup = (ushort*)((byte*)nativeRecord + UsaOffset);
                pFixup++; // Skip fixup tag.
                for (int sectorIndex = 0; sectorIndex < fixupCount; sectorIndex++, fixLocation += bytesPerSector) {
                    *((ushort*)fixLocation) = *(pFixup++);
                }
            }
        }

        private void AssertTag(byte[] expected)
        {
            if ((null == expected) || (4 != expected.Length)) {
                throw new ArgumentException();
            }
            for(int index = 0; index < expected.Length; index++) {
                // TODO : Should perform an uint value comparison.
                if (expected[index] != (byte)(this.Type & ((uint)0xFF << index))) {
                    throw new ApplicationException();
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

        internal void EnumerateIndex()
        {
            AssertTag(INDEX_TAG_LE);
            throw new NotImplementedException();
        }

        private const string IndexTag = "INDX";
        private static byte[] INDEX_TAG_LE = Encoding.ASCII.GetBytes(IndexTag);
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