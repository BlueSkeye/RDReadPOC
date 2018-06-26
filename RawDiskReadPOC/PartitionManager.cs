using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using RawDiskReadPOC.NTFS;

namespace RawDiskReadPOC
{
    internal class PartitionManager
    {
        internal PartitionManager(IntPtr rawHandle, DiskGeometry geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException();
            _rawHandle = rawHandle;
        }

        internal DiskGeometry Geometry { get { return _geometry; } }

        /// <summary>Discover partitions</summary>
        internal unsafe void Discover()
        {
            byte* masterBootRecord = null;
            try {
                masterBootRecord = (byte*)this.Read(0);
                if (0x55 != masterBootRecord[510]) { throw new ApplicationException(); }
                if (0xAA != masterBootRecord[511]) { throw new ApplicationException(); }
                // From http://thestarman.pcministry.com/asm/mbr/PartTables.htm
                for (uint partitionIndex = 0; partitionIndex < 4; partitionIndex++) {
                    GenericPartition newPartition = GenericPartition.Create(this, masterBootRecord, 446 + (16 * partitionIndex));
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

        internal IEnumerable<GenericPartition> EnumeratePartitions()
        {
            foreach(GenericPartition item in _partitions) { yield return item; }
        }

        internal unsafe void SeekTo(ulong logicalBlockAddress)
        {
            uint bytesPerSector = _geometry.BytesPerSector;
            ulong offset = logicalBlockAddress * bytesPerSector;
            if (!Natives.SetFilePointerEx(_rawHandle, (long)offset, out offset, Natives.FILE_BEGIN)) {
                throw new ApplicationException();
            }
        }

        private DiskGeometry _geometry;
        private List<GenericPartition> _partitions = new List<GenericPartition>();
        private IntPtr _rawHandle;

        internal abstract class GenericPartition
        {
            protected GenericPartition(uint startSector, uint sectorCount)
            {
                StartSector = startSector;
                SectorCount = sectorCount;
            }

            internal bool Active { get; private set; }

            internal PartitionManager Manager { get; private set; }

            internal uint SectorCount { get; private set; }

            internal bool ShouldCapture
            {
                get { return Active; }
            }

            internal uint StartSector { get; private set; }

            internal static unsafe GenericPartition Create(PartitionManager manager, byte* buffer, uint offset)
            {
                if (null == manager) { throw new ArgumentNullException(); }
                byte partitionType = buffer[offset + 4];
                bool hiddenPartition = false;
                bool activePartition = (0x80 == buffer[offset]);
                uint startSector = *((uint*)(buffer + offset + 8));
                uint sectorsCount = *((uint*)(buffer + offset + 12));
                GenericPartition result = null;
                // See : https://en.wikipedia.org/wiki/Partition_type
                switch (partitionType) {
                    case 0x00:
                        // Empty entry.
                        return null;
                    case 0x07:
                        // TODO : Consider using a mapping that restrict viewing to the partition content.
                        IntPtr partitionHandle;
                        if (!Natives.DuplicateHandle(new IntPtr(-1), manager._rawHandle,
                            new IntPtr(-1), out partitionHandle, 0 /* ignored because same access */,
                            false, 2 /*DUPLICATE_SAME_ACCESS*/))
                        {
                            throw new ApplicationException();
                        }
                        result = new NtfsPartition(partitionHandle, hiddenPartition, startSector, sectorsCount);
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
                    result.Manager = manager;
                }
                return result;
            }
        }
    }
}
