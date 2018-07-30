using System;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>Attribute record header. Always aligned to 8-byte boundary.</summary>
    internal struct NtfsRecord
    {
        /// <summary>The Update Sequence Number of the NTFS record.</summary>
        internal unsafe ushort Usn
        {
            get
            {
                fixed (NtfsRecord* ptr = &this) {
                    return *((ushort*)((byte*)ptr + ptr->UsaOffset));
                }
            }
        }

        /// <summary></summary>
        /// <remarks>The Update Sequence Array (usa) is an array of the short values which
        /// belong to the end of each sector protected by the update sequence record in which
        /// this array is contained. Note that the first entry is the Update Sequence Number
        /// (usn), a cyclic counter of how many times the protected record has been written
        /// to disk. The values 0 and -1 (ie. 0xffff) are not used. All last short's of each
        /// sector have to be equal to the usn (during reading) or are set to it (during
        /// writing). If they are not, an incomplete multi sector transfer has occurred when
        /// the data was written.
        /// The maximum size for the update sequence array is fixed to:
        /// maximum size = usa_ofs + (usa_count * 2) = 510 bytes
        /// The 510 bytes comes from the fact that the last short in the array has to
        /// (obviously) finish before the last short of the first 512 - byte sector.
        /// This formula can be used as a consistency check in that usa_ofs + (usa_count * 2)
        /// has to be less than or equal to 510.</remarks>
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

        private void AssertTag(uint expected)
        {
            if (expected != this.Type) {
                throw new ApplicationException();
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
            ushort usaCount = UsaCount;
            Console.WriteLine("{0}, off {1}, cnt {2}, usn {3}",
                Encoding.ASCII.GetString(nameBytes), UsaOffset, usaCount, Usn);
            if (0 < UsaCount) {
                fixed(NtfsRecord* rawRecord = &this) {
                    ushort* pFixup = (ushort*)((byte*)rawRecord + UsaOffset);
                    Console.Write("Fixup : {0:X4} => ", *(pFixup++));
                    usaCount--; // The first entry is the usn protecting the record.
                    for(int index = 0; index < usaCount; index++) {
                        if (0 != index) { Console.Write(", "); }
                        Console.Write("{0:X4}", *(pFixup++));
                    }
                    Console.WriteLine();
                }
            }
        }

        internal void EnumerateIndex()
        {
            AssertTag(Constants.IndxRecordMarker);
            throw new NotImplementedException();
        }

        /// <summary>The (32-bit) type of the attribute. A four-byte magic identifying the record
        /// type and/or status.</summary>
        internal uint Type;
        /// <summary>Offset to the Update Sequence Array (usa) from the start of the ntfs
        /// record.</summary>
        internal ushort UsaOffset;
        /// <summary>Number of short sized entries in the usa including the Update Sequence
        /// Number(usn), thus the number of fixups is the usa_count minus 1.</summary>
        internal ushort UsaCount;
    }
}