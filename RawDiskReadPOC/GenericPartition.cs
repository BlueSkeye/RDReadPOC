using System;
using System.IO;
using System.Runtime.InteropServices;

using RawDiskReadPOC.NTFS;

namespace RawDiskReadPOC
{
    internal abstract class GenericPartition
    {
        protected GenericPartition(IntPtr handle, uint startSector, uint sectorCount, bool allocateId = true)
        {
            if (IntPtr.Zero == handle) { throw new ArgumentNullException(); }
            _handle = handle;
            StartSector = startSector;
            SectorCount = sectorCount;
            if (allocateId) {
                Id = NextPartitionId++;
            }
        }

        internal bool Active { get; private set; }

        internal abstract uint BytesPerSector { get; }

        internal IntPtr Handle { get; private set; }

        /// <summary>This identifier is NOT from the underlying disk data. It is an index that is incremented
        /// each time a new partition is created.</summary>
        internal uint Id { get; private set; }

        internal uint SectorCount { get; private set; }

        // TODO : Make this more configurable and not relying on the entry program.
        internal bool ShouldCapture
        {
            get { return Id == Program.TrackedPartitionIndex; }
        }

        internal uint StartSector { get; private set; }

        internal static unsafe GenericPartition Create(IntPtr partitionHandle, byte* buffer, uint offset)
        {
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
        
        /// <summary>Read some number of sectors.</summary>
        /// <param name="into">Pre-allocated buffer.</param>
        /// <param name="logicalSectorId">Logical identifier of the first sector to be read.</param>
        /// <param name="sectorsCount">Number of sectors to read.</param>
        /// <returns>Buffer address.</returns>
        internal unsafe IPartitionClusterData ReadSectors(IPartitionClusterData into, long atOffset,
            ulong logicalSectorId, uint sectorsCount = 1)
        {
            uint bytesPerSector = PartitionManager.Singleton.Geometry.BytesPerSector;
            uint expectedCount = sectorsCount * bytesPerSector;
            if (null == into) {
                throw new ArgumentNullException();
            }
            if (0 > atOffset) {
                throw new ArgumentException();
            }
            if ((into.DataSize - atOffset) < expectedCount) {
                throw new ArgumentException();
            }
            if (FeaturesContext.DataPoolChecksEnabled) {
                if (expectedCount > into.DataSize) {
                    throw new ApplicationException();
                }
            }
            ulong offset = (logicalSectorId + StartSector) * bytesPerSector;
            uint totalBytesRead = 0;
            // Prevent concurrent reads on this partition.
            lock (_ioLock) {
                if (!Natives.SetFilePointerEx(_handle, (long)offset, out offset, Natives.FILE_BEGIN)) {
                    int error = Marshal.GetLastWin32Error();
                    throw new ApplicationException();
                }
                uint chainItemsCount = into.GetChainItemsCount();
                uint chainLength = into.GetChainLength();
                if (chainLength < expectedCount) {
                    throw new ApplicationException();
                }
                uint remainingExpectation = expectedCount;
                for (IPartitionClusterData currentData = into; null != currentData; currentData = currentData.NextInChain) {
                    uint readSize = remainingExpectation;
                    if (readSize > into.DataSize) {
                        readSize = into.DataSize;
                    }
                    uint effectiveReadSize;
                    if (!Natives.ReadFile(_handle, into.Data + atOffset, readSize, out effectiveReadSize, IntPtr.Zero)) {
                        int error = Marshal.GetLastWin32Error();
                        throw new ApplicationException();
                    }
                    atOffset += effectiveReadSize;
                    totalBytesRead += effectiveReadSize;
                    remainingExpectation -= effectiveReadSize;
                }
            }
            if (totalBytesRead != expectedCount) {
                throw new ApplicationException();
            }
            return into;
        }
        
        /// <summary>Read some number of sectors.</summary>
        /// <param name="logicalSectorId">Logical identifier of the first sector to be read.</param>
        /// <param name="sectorsCount">Number of sectors to read.</param>
        /// <returns>Buffer address.</returns>
        internal unsafe IPartitionClusterData ReadSectors(ulong logicalSectorId, uint sectorsCount = 1)
        {
            IPartitionClusterData result = GetClusterBufferChain(sectorsCount * BytesPerSector);
            try {
                return ReadSectors(result, 0, logicalSectorId, sectorsCount);
            }
            catch {
                if (null != result) {
                    result.Dispose();
                }
                throw;
            }
        }

        internal unsafe IPartitionClusterData ReadSparseSectors(uint sectorsCount)
        {
            return GetClusterBufferChain(sectorsCount * BytesPerSector).Zeroize();
        }

        internal unsafe void SeekToSector(ulong logicalSectorNumber)
        {
            PartitionManager.Singleton.SeekTo(logicalSectorNumber + this.StartSector);
        }

        private IntPtr _handle;
        private object _ioLock = new object();
        private static uint NextPartitionId = 1;
    }
}
