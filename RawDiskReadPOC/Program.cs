using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

using RawDiskReadPOC.NTFS;

namespace RawDiskReadPOC
{
    public static class Program
    {
        internal static int TrackedPartitionIndex => 2;

        private static void DisplayVersion()
        {
            Assembly entryAssembly = Assembly.GetEntryAssembly();
            AssemblyName entryAssemblyName = entryAssembly.GetName();
            Console.WriteLine("{0} v{1}", entryAssemblyName.Name, entryAssemblyName.Version.ToString());
            if (FeaturesContext.InvariantChecksEnabled) {
                Console.WriteLine("Invariant checks enabled.");
            }
        }

        private static unsafe void FindFile(string filename)
        {
            throw new NotImplementedException();
        }

        private static unsafe void InterpretActivePartitions()
        {
            byte* sector = null;
            byte* mftRecord = null;
            try {
                foreach (GenericPartition partition in _partitionManager.EnumeratePartitions()) {
                    if (!partition.ShouldCapture) { continue; }
                    NtfsPartition ntfsPartition = partition as NtfsPartition;
                    NtfsPartition.Current = ntfsPartition;
                    if (null == ntfsPartition) { throw new NotSupportedException(); }
                    ntfsPartition.InterpretBootSector();
                    ntfsPartition.CaptureMetadataFilePointers();
                }
                return;
            }
            finally {
                if (null != mftRecord) { Marshal.FreeCoTaskMem((IntPtr)mftRecord); }
                if (null != sector) { Marshal.FreeCoTaskMem((IntPtr)sector); }
            }
        }

        public static unsafe int Main(string[] args)
        {
            // TODO : Configure TrackedPartitionIndex from command line arguments.
            AppDomain.CurrentDomain.FirstChanceException += delegate (object sender, FirstChanceExceptionEventArgs e) {
                Exception ex = e.Exception;
                return;
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) {
                Exception ex = e.ExceptionObject as Exception;
                return;
            };
            DisplayVersion();

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
                _partitionManager = new PartitionManager(handle, geometry);
                _partitionManager.Discover();
                InterpretActivePartitions();
                if (FeaturesContext.InvariantChecksEnabled) {
                    NtfsMFTFileRecord.AssertMFTRecordCachingInvariance(_partitionManager);
                }
                foreach (GenericPartition partition in _partitionManager.EnumeratePartitions()) {
                    if (!partition.ShouldCapture) { continue; }
                    NtfsPartition ntfsPartition = partition as NtfsPartition;
                    NtfsPartition.Current = ntfsPartition;

                    // Basic functionnality tests. Don't remove.
                    //ntfsPartition.CountFiles();
                    //ntfsPartition.MonitorBadClusters();
                    //ntfsPartition.ReadBitmap();

                    // Locate file.
                    NtfsIndexEntry* pageFileSysEntry = ntfsPartition.FindFile("pagefile.sys");
                    throw new NotImplementedException();
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

        private static PartitionManager _partitionManager;
    }
}
