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
            lock (typeof(PartitionManager)) {
                if (null != Singleton) {
                    throw new InvalidOperationException("Singleton rule violation.");
                }
                Singleton = this;
            }
            _geometry = geometry ?? throw new ArgumentNullException();
            _rawHandle = rawHandle;
        }

        internal DiskGeometry Geometry { get { return _geometry; } }

        internal static PartitionManager Singleton { get; private set; }

        /// <summary>Discover partitions</summary>
        internal unsafe void Discover()
        {
            VolumePartition fakePartition = new VolumePartition(_rawHandle, 0, 16);
            using (IPartitionClusterData rawData = fakePartition.Read(0)) {
                uint minSector = uint.MaxValue;
                uint maxSector = 0;
                byte* masterBootRecord = rawData.Data;
                if (0x55 != masterBootRecord[510]) { throw new ApplicationException(); }
                if (0xAA != masterBootRecord[511]) { throw new ApplicationException(); }
                // From http://thestarman.pcministry.com/asm/mbr/PartTables.htm
                for (uint partitionIndex = 0; partitionIndex < 4; partitionIndex++) {
                    GenericPartition newPartition = GenericPartition.Create(this, masterBootRecord, 446 + (16 * partitionIndex));
                    if (null != newPartition) {
                        // TODO : This algorithm doesn't let us witness the extra sectors after the last partition.
                        if (minSector > newPartition.StartSector) {
                            minSector = newPartition.StartSector;
                        }
                        if (maxSector < (newPartition.StartSector + newPartition.SectorCount - 1)) {
                            maxSector = newPartition.StartSector + newPartition.SectorCount - 1;
                        }
                        _partitions.Add(newPartition);
                    }
                }
                Console.WriteLine("Found {0} partitions.", _partitions.Count);
                if (maxSector < minSector) { throw new ApplicationException(); }
                _volumePartition = new VolumePartition(_rawHandle, minSector, maxSector - minSector);
                return;
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
        private VolumePartition _volumePartition;

        internal abstract class GenericPartition
        {
            protected GenericPartition(IntPtr handle, uint startSector, uint sectorCount)
            {
                if (IntPtr.Zero == handle) { throw new ArgumentNullException(); }
                _handle = handle;
                StartSector = startSector;
                SectorCount = sectorCount;
            }

            internal bool Active { get; private set; }

            internal IntPtr Handle { get; private set; }

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
                }
                return result;
            }

            protected abstract IPartitionClusterData GetClusterBuffer();

            internal IPartitionClusterData Read(ulong logicalBlockAddress, uint count = 1)
            {
                return ReadBlocks(logicalBlockAddress, count);
            }

            /// <summary>Read a some number of sectors.</summary>
            /// <param name="logicalBlockAddress">Address of the buffer to read.</param>
            /// <param name="blocksCount">Number of blocks to read.</param>
            /// <returns>Buffer address.</returns>
            internal unsafe IPartitionClusterData ReadBlocks(ulong logicalBlockAddress, uint blocksCount = 1)
            {
                IPartitionClusterData result = GetClusterBuffer();
                try {
                    uint bytesPerSector = Singleton.Geometry.BytesPerSector;
                    uint expectedCount = blocksCount * bytesPerSector;
                    ulong offset = logicalBlockAddress * bytesPerSector;
                    if (!Natives.SetFilePointerEx(_handle, (long)offset, out offset, Natives.FILE_BEGIN)) {
                        throw new ApplicationException();
                    }
                    uint totalBytesRead;
                    if (!Natives.ReadFile(_handle, result.Data, expectedCount, out totalBytesRead, IntPtr.Zero)) {
                        throw new ApplicationException();
                    }
                    if (totalBytesRead != expectedCount) {
                        throw new ApplicationException();
                    }
                    return result;
                }
                catch {
                    result.Dispose();
                    throw;
                }
            }

            private IntPtr _handle;
        }

        private class VolumePartition : GenericPartition
        {
            internal VolumePartition(IntPtr handle, uint startSector, uint sectorCount)
                : base(handle, startSector, sectorCount)
            {
                return;
            }

            protected override IPartitionClusterData GetClusterBuffer()
            {
                return new MinimalPartitionClusterDataImpl();
            }

            private class MinimalPartitionClusterDataImpl : IPartitionClusterData
            {
                public MinimalPartitionClusterDataImpl()
                {
                    // TODO : Add flexibility. No hardcoded size.
                    _nativeData = Marshal.AllocCoTaskMem(16 * 1024);
                }

                public uint DataSize => throw new NotImplementedException();

                public unsafe byte* Data => throw new NotImplementedException();

                public void Dispose()
                {
                    lock (this) {
                        if (IntPtr.Zero != _nativeData) {
                            Marshal.FreeCoTaskMem(_nativeData);
                            _nativeData = IntPtr.Zero;
                        }
                    }
                }

                private IntPtr _nativeData;
            }
        }
    }
}
