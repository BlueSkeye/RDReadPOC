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
            using (IPartitionClusterData rawData = fakePartition.ReadSectors(0)) {
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

            internal abstract uint BytesPerSector { get; }

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

            protected abstract IPartitionClusterData GetClusterBufferChain(uint size = 0);

            /// <summary>Read a some number of sectors.</summary>
            /// <param name="logicalSectorId">Logical identifier of the first sector to be read.</param>
            /// <param name="sectorsCount">Number of sectors to read.</param>
            /// <returns>Buffer address.</returns>
            internal unsafe IPartitionClusterData ReadSectors(ulong logicalSectorId, uint sectorsCount = 1)
            {
                IPartitionClusterData result = GetClusterBufferChain(sectorsCount * BytesPerSector);
                try {
                    uint bytesPerSector = Singleton.Geometry.BytesPerSector;
                    uint expectedCount = sectorsCount * bytesPerSector;
                    result = GetClusterBufferChain(expectedCount);
                    ulong offset = (logicalSectorId + StartSector) * bytesPerSector;
                    uint totalBytesRead = 0;
                    // Prevent concurrent reads on this partition.
                    lock (_ioLock) {
                        if (!Natives.SetFilePointerEx(_handle, (long)offset, out offset, Natives.FILE_BEGIN)) {
                            int error = Marshal.GetLastWin32Error();
                            throw new ApplicationException();
                        }
                        if (result.GetChainLength() < expectedCount) {
                            throw new ApplicationException();
                        }
                        uint remainingExpectation = expectedCount;
                        for (IPartitionClusterData currentData = result; null != currentData; currentData = currentData.NextInChain) {
                            uint readSize = remainingExpectation;
                            if (readSize > result.DataSize) {
                                readSize = result.DataSize;
                            }
                            uint effectiveReadSize;
                            if (!Natives.ReadFile(_handle, result.Data, readSize, out effectiveReadSize, IntPtr.Zero)) {
                                int error = Marshal.GetLastWin32Error();
                                throw new ApplicationException();
                            }
                            totalBytesRead += effectiveReadSize;
                            remainingExpectation -= effectiveReadSize;
                        }
                    }
                    if (totalBytesRead != expectedCount) {
                        throw new ApplicationException();
                    }
                    return result;
                }
                catch {
                    if (null != result) {
                        result.Dispose();
                    }
                    throw;
                }
            }

            internal unsafe void SeekTo(ulong logicalBlockAddress)
            {
                PartitionManager.Singleton.SeekTo(logicalBlockAddress + this.StartSector);
            }

            private IntPtr _handle;
            private object _ioLock = new object();
        }

        private class VolumePartition : GenericPartition
        {
            internal VolumePartition(IntPtr handle, uint startSector, uint sectorCount)
                : base(handle, startSector, sectorCount)
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
                public unsafe MinimalPartitionClusterDataImpl(uint count)
                {
                    if (0 == count) {
                        throw new ArgumentOutOfRangeException();
                    }
                    // TODO : Add flexibility. No hardcoded size.
                    DataSize = AllocationChunkSize * (1 + ((count - 1) / AllocationChunkSize));
                    _nativeData = (byte*)Marshal.AllocCoTaskMem((int)DataSize).ToPointer();
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
                    }
                }

                private const int AllocationChunkSize = 1024;
                private unsafe byte* _nativeData;
            }
        }
    }
}
