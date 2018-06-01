using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RawDiskReadPOC
{
    /// <summary></summary>
    /// <remarks>See
    /// http://www.ntfs.com/ntfs-mft.htm
    /// http://dubeyko.com/development/FileSystems/NTFS/ntfsdoc.pdf
    /// https://en.wikipedia.org/wiki/NTFS
    /// </remarks>
    internal class NTFSPartition : PartitionManager.PartitionBase
    {
        internal NTFSPartition(bool hidden, uint startSector, uint sectorCount)
            : base(startSector, sectorCount)
        {
            Hidden = hidden;
        }

        internal uint BytesPerSector { get; private set; }

        internal ulong ClustersPerFileRecordSegment { get; private set; }

        internal ulong ClustersPerIndexBuffer { get; private set; }

        internal uint HeadsCount { get; private set; }

        internal bool Hidden { get; private set; }

        internal uint HiddenSectorsCount { get; private set; }

        internal byte MediaDescriptor { get; private set; }

        internal ulong MFTClusterNumber { get; private set; }

        internal ulong MFTMirrorClusterNumber { get; private set; }

        internal uint SectorsPerCluster { get; private set; }

        internal uint SectorsPerTrack { get; private set; }

        internal ulong TotalSectorsCount { get; private set; }

        internal ulong VolumeSerialNumber { get; private set; }

        internal unsafe void InterpretBootSector()
        {
            byte* sector = null;
            try {
                sector = Manager.Read(StartSector, 1, sector);
                if (0x55 != sector[510]) { throw new ApplicationException(); }
                if (0xAA != sector[511]) { throw new ApplicationException(); }
                byte* sectorPosition = sector + 3;
                // Verify OEMID field conformance.
                int OEMIDlength = OEMID.Length;
                for(int index = 0; index < OEMIDlength; index++) {
                    if (*(sectorPosition++) != OEMID[index]) {
                        throw new ApplicationException();
                    }
                }
                BytesPerSector = *(ushort*)(sectorPosition); sectorPosition += sizeof(ushort);
                SectorsPerCluster = *(byte*)(sectorPosition++);
                sectorPosition += sizeof(ushort); // Unused
                sectorPosition += 3; // Unused
                sectorPosition += sizeof(ushort); // Unused
                MediaDescriptor = *(byte*)(sectorPosition++);
                sectorPosition += sizeof(ushort); // Unused
                SectorsPerTrack = *(ushort*)(sectorPosition); sectorPosition += sizeof(ushort);
                HeadsCount = *(ushort*)(sectorPosition); sectorPosition += sizeof(ushort);
                HiddenSectorsCount = *(uint*)(sectorPosition); sectorPosition += sizeof(uint);
                sectorPosition += sizeof(uint); // Unused
                sectorPosition += sizeof(uint); // Unused
                TotalSectorsCount = *(ulong*)(sectorPosition); sectorPosition += sizeof(ulong);
                MFTClusterNumber = *(ulong*)(sectorPosition); sectorPosition += sizeof(ulong);
                MFTMirrorClusterNumber = *(ulong*)(sectorPosition); sectorPosition += sizeof(ulong);
                sbyte rawClusteringValue = *(sbyte*)(sectorPosition++);
                ClustersPerFileRecordSegment = (0 < rawClusteringValue)
                    ? (byte)rawClusteringValue
                    : (1UL << (-rawClusteringValue));
                sectorPosition += 3; // Unused
                rawClusteringValue = *(sbyte*)(sectorPosition++);
                ClustersPerIndexBuffer = (0 < rawClusteringValue)
                    ? (byte)rawClusteringValue
                    : (1UL << (-rawClusteringValue));
                sectorPosition += 3; // Unused
                VolumeSerialNumber = *(ulong*)(sectorPosition); sectorPosition += sizeof(ulong);
                sectorPosition += sizeof(uint); // Unused
                if (0x54 != (sectorPosition - sector)) { throw new ApplicationException(); }
            }
            finally { if(null != sector) { Marshal.FreeCoTaskMem((IntPtr)sector); } }
        }

        private static readonly byte[] OEMID = Encoding.ASCII.GetBytes("NTFS    ");
    }
}
