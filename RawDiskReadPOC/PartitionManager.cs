using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC
{
    internal class PartitionManager
    {
        internal PartitionManager(IntPtr rawHandle, DiskGeometry geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException();
            _rawHandle = rawHandle;
        }

        /// <summary>Discover partitions</summary>
        internal unsafe void Discover()
        {
            byte* masterBootRecord = null;
            try {
                masterBootRecord = (byte*)_geometry.Read(0);
                if (0x55 != masterBootRecord[510]) { throw new ApplicationException(); }
                if (0xAA != masterBootRecord[511]) { throw new ApplicationException(); }
                // From http://thestarman.pcministry.com/asm/mbr/PartTables.htm
                for (uint partitionIndex = 0; partitionIndex < 4; partitionIndex++) {
                    PartitionBase newPartition = PartitionBase.Create(masterBootRecord, 446 + (16 * partitionIndex));
                    if (null != newPartition) {
                        _partitions.Add(newPartition);
                    }
                }
                Console.WriteLine("Found {0} partitions.", _partitions.Count);
                return;
            }
            finally {
                if (null != masterBootRecord) { Marshal.FreeCoTaskMem((IntPtr)masterBootRecord); }
            }
        }

        internal IEnumerable<PartitionBase> EnumeratePartitions()
        {
            foreach(PartitionBase item in _partitions) { yield return item; }
        }

        private DiskGeometry _geometry;
        private List<PartitionBase> _partitions = new List<PartitionBase>();
        private IntPtr _rawHandle;

        internal abstract class PartitionBase
        {
            protected PartitionBase(uint startSector, uint sectorCount)
            {
                StartSector = startSector;
                SectorCount = sectorCount;
            }

            internal bool Active { get; private set; }

            internal uint SectorCount { get; private set; }

            internal uint StartSector { get; private set; }

            internal static unsafe PartitionBase Create(byte* buffer, uint offset)
            {
                byte partitionType = buffer[offset + 4];
                bool hiddenPartition = false;
                bool activePartition = (0x80 == buffer[offset]);
                uint startSector = *((uint*)(buffer + offset + 8));
                uint sectorsCount = *((uint*)(buffer + offset + 12));
                PartitionBase result = null;
                // See : https://en.wikipedia.org/wiki/Partition_type
                switch (partitionType) {
                    case 0x00:
                        // Empty entry.
                        return null;
                    case 0x07:
                        result = new NTFSPartition(hiddenPartition, startSector, sectorsCount);
                        break;
                    case 0x17:
                        // TODO : Should differentiate 0x17 & 0x27
                        hiddenPartition = true;
                        goto case 0x07;
                    case 0x27:
                        // See https://docs.microsoft.com/en-us/windows/deployment/mbr-to-gpt
                        // See https://docs.microsoft.com/en-us/windows-hardware/manufacture/desktop/configure-biosmbr-based-hard-drive-partitions
                        // See https://docs.microsoft.com/en-us/windows-hardware/manufacture/desktop/windows-and-gpt-faq
                        hiddenPartition = true;
                        goto case 0x07;
                    default:
                        Console.WriteLine("unsupported partition type 0x{0:X2}.", partitionType);
                        return null;
                }
                if (null != result) {
                    result.Active = activePartition;
                }
                return result;
            }
        }
    }
}
