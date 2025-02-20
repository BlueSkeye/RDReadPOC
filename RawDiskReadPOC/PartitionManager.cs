using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC
{
    internal class PartitionManager
    {
        private static readonly IntPtr ThisProcessHandle = new IntPtr(-1);

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

        internal DiskGeometry Geometry => _geometry;

        internal static PartitionManager Singleton { get; private set; }

        /// <summary>Discover partitions</summary>
        internal unsafe void Discover()
        {
            VolumePartition fakePartition = new VolumePartition(_rawHandle, 0, 16);
            using (IPartitionClusterData rawData = fakePartition.ReadSectors(0)) {
                uint minSector = uint.MaxValue;
                uint maxSector = 0;
                byte* masterBootRecord = rawData.Data;
                if (0x55 != masterBootRecord[510]) { throw new ApplicationException(); }
                if (0xAA != masterBootRecord[511]) { throw new ApplicationException(); }
                // From http://thestarman.pcministry.com/asm/mbr/PartTables.htm
                // TODO : This is suitable for a Master Boot Record. Other types of boot records exist such
                // as the ExtendedBoot Record format.
                for (uint partitionIndex = 0; partitionIndex < 4; partitionIndex++) {
                    IntPtr partitionHandle;
                    if (!Natives.DuplicateHandle(ThisProcessHandle, _rawHandle, ThisProcessHandle,
                        out partitionHandle, 0 /* ignored because same access */,
                        false, 2 /*DUPLICATE_SAME_ACCESS*/))
                    {
                        throw new ApplicationException();
                    }
                    GenericPartition newPartition = GenericPartition.Create(partitionHandle,
                        masterBootRecord, 446 + (16 * partitionIndex));
                    if (null == newPartition) {
                        Natives.CloseHandle(partitionHandle);
                        continue;
                    }
                    // TODO : This algorithm doesn't let us witness the extra sectors after the last partition.
                    if (minSector > newPartition.StartSector) {
                        minSector = newPartition.StartSector;
                    }
                    if (maxSector < (newPartition.StartSector + newPartition.SectorCount - 1)) {
                        maxSector = newPartition.StartSector + newPartition.SectorCount - 1;
                    }
                    _partitions.Add(newPartition);
                }
                Console.WriteLine("[+] Found {0} partitions.", _partitions.Count);
                if (maxSector < minSector) { throw new ApplicationException(); }
                _volumePartition = new VolumePartition(_rawHandle, minSector, maxSector - minSector);
                return;
            }
        }

        internal IEnumerable<GenericPartition> EnumeratePartitions()
        {
            foreach(GenericPartition item in _partitions) { yield return item; }
        }

        internal unsafe void SeekTo(ulong physicalSectorNumber)
        {
            uint bytesPerSector = _geometry.BytesPerSector;
            ulong offset = physicalSectorNumber * bytesPerSector;
            if (!Natives.SetFilePointerEx(_rawHandle, (long)offset, out offset, Natives.FILE_BEGIN)) {
                throw new ApplicationException();
            }
        }

        private DiskGeometry _geometry;
        private List<GenericPartition> _partitions = new List<GenericPartition>();
        private IntPtr _rawHandle;
        private VolumePartition _volumePartition;

        private class VolumePartition : GenericPartition
        {
            internal VolumePartition(IntPtr handle, uint startSector, uint sectorCount)
                : base(handle, startSector, sectorCount, false)
            {
                return;
            }

            internal override uint BytesPerSector => DefaultSectorSize;

            protected override IPartitionClusterData GetClusterBufferChain(uint count)
            {
                return new MinimalPartitionClusterDataImpl(count);
            }

            private uint DefaultSectorSize = 512;

            private class MinimalPartitionClusterDataImpl : IPartitionClusterData
            {
                private const int AllocationChunkSize = 1024;
                private unsafe byte* _nativeData;

                public unsafe MinimalPartitionClusterDataImpl(uint count)
                {
                    if (0 == count) {
                        throw new ArgumentOutOfRangeException();
                    }
                    // TODO : Add flexibility. No hardcoded size.
                    DataSize = AllocationChunkSize * (1 + ((count - 1) / AllocationChunkSize));
                    _nativeData = (byte*)Marshal.AllocCoTaskMem((int)DataSize).ToPointer();
                }

                public event IPartitionClusterDataDisposedDelegate Disposed;

                public void BinaryDump()
                {
                    throw new NotImplementedException();
                }

                public void BinaryDumpChain()
                {
                    throw new NotImplementedException();
                }

                public uint DataSize { get; private set; }

                public unsafe byte* Data => _nativeData;

                public IPartitionClusterData NextInChain => null;

                public unsafe void Dispose()
                {
                    lock (this) {
                        if (null != _nativeData) {
                            Marshal.FreeCoTaskMem(new IntPtr(_nativeData));
                            _nativeData = null;
                        }
                        Disposed?.Invoke(this);
                    }
                }

                public unsafe IPartitionClusterData Zeroize()
                {
                    Helpers.Zeroize(_nativeData, AllocationChunkSize);
                    return this;
                }
            }
        }
    }
}
