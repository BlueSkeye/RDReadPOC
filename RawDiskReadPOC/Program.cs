using System;
using System.Reflection;
using System.Runtime.InteropServices;

using RawDiskReadPOC.NTFS;

namespace RawDiskReadPOC
{
    public static class Program
    {
        public static unsafe int Main(string[] args)
        {
            Assembly entryAssembly = Assembly.GetEntryAssembly();
            AssemblyName entryAssemblyName = entryAssembly.GetName();
            Console.WriteLine("{0} v{1}", entryAssemblyName.Name, entryAssemblyName.Version.ToString());

            IntPtr handle = IntPtr.Zero;
            int nativeError;
            DiskGeometry geometry = new DiskGeometry();

            try {
                handle = Natives.CreateFile2(@"\\.\PhysicalDrive0", 0x80000000 /* GENERIC_READ */,
                    0x02 /* FILE_SHARE_WRITE */, 3 /* OPEN_EXISTING */, IntPtr.Zero);
                nativeError = Marshal.GetLastWin32Error();
                if ((IntPtr.Zero == handle) || (0 != nativeError)) {
                    Console.WriteLine("Physical drive opening failed. Error 0x{0:X8}", nativeError);
                    return 1;
                }
                geometry.Acquire(handle);
                PartitionManager partitionManager = new PartitionManager(handle, geometry);
                partitionManager.Discover();
                byte* sector = null;
                byte* mftRecord = null;
                try {
                    foreach (PartitionManager.PartitionBase partition in partitionManager.EnumeratePartitions()) {
                        if (partition.Active) {
                            NTFSPartition ntfsPartition = partition as NTFSPartition;
                            if (null == ntfsPartition) { throw new NotSupportedException(); }
                            ntfsPartition.InterpretBootSector();

                            ulong mftRecordLBA = ntfsPartition.StartSector + (ntfsPartition.MFTClusterNumber * ntfsPartition.SectorsPerCluster);
                            mftRecord = partitionManager.Read(mftRecordLBA, 2);
                            NtfsFileRecordHeader* recordHeader = (NtfsFileRecordHeader*)mftRecord;
                            Helpers.Dump(mftRecord, 2 * geometry.BytesPerSector);
                            continue;
                        }
                        // For now we don't interpret nonactive partitions.
                    }
                }
                finally {
                    if (null != mftRecord) { Marshal.FreeCoTaskMem((IntPtr)mftRecord); }
                    if (null != sector) { Marshal.FreeCoTaskMem((IntPtr)sector); }
                }
                return 0;
            }
            finally {
                if (IntPtr.Zero == handle) {
                    Natives.CloseHandle(handle);
                    handle = IntPtr.Zero;
                }
            }
        }
    }
}
