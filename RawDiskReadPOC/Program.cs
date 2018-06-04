using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC
{
    public static class Program
    {
        private static unsafe void CountFiles()
        {
            foreach (PartitionManager.PartitionBase partition in _partitionManager.EnumeratePartitions()) {
                NTFSPartition ntfsPartition = partition as NTFSPartition;
                if (null == ntfsPartition) { throw new NotSupportedException(); }
                if (!partition.Active) { continue; }
                ulong filesCount = ntfsPartition.CountFiles();
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
                foreach (PartitionManager.PartitionBase partition in _partitionManager.EnumeratePartitions()) {
                    if (!partition.Active) { continue; }
                    NTFSPartition ntfsPartition = partition as NTFSPartition;
                    if (null == ntfsPartition) { throw new NotSupportedException(); }
                    ntfsPartition.InterpretBootSector();
                    ntfsPartition.CaptureMetadataFilePointers();
                    ntfsPartition.MonitorBadClusters();
                    ntfsPartition.ReadBitmap();
                    // ntfsPartition.DumpFirstFileNames();
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
                _partitionManager = new PartitionManager(handle, geometry);
                _partitionManager.Discover();
                InterpretActivePartitions();
                //CountFiles();
                //FindFile(@"C:\Hyberfil.sys");
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
