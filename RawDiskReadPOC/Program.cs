using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

using RawDiskReadPOC.NTFS;
using RawDiskReadPOC.NTFS.Indexing;

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
            FeaturesContext.Display();
        }

        private static unsafe void FindFile(string filename)
        {
            throw new NotImplementedException();
        }

        private static void InstallExceptionHandlers()
        {
            AppDomain.CurrentDomain.FirstChanceException +=
                delegate (object sender, FirstChanceExceptionEventArgs e) {
                    Exception ex = e.Exception;
                    return;
                };
            AppDomain.CurrentDomain.UnhandledException +=
                delegate (object sender, UnhandledExceptionEventArgs e) {
                    Exception ex = e.ExceptionObject as Exception;
                    return;
                };
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
            InstallExceptionHandlers();
            using (PartitionDataDisposableBatch mainBatch = PartitionDataDisposableBatch.CreateNew()) {
                DisplayVersion();

                IntPtr handle = IntPtr.Zero;
                int nativeError;
                DiskGeometry geometry = new DiskGeometry();

                try {
                    handle = Natives.CreateFile2(@"\\.\PhysicalDrive0", 0x80000000 /* GENERIC_READ */,
                        0x02 /* FILE_SHARE_WRITE */, 3 /* OPEN_EXISTING */, IntPtr.Zero);
                    nativeError = Marshal.GetLastWin32Error();
                    if ((IntPtr.Zero == handle) || (0 != nativeError)) {
                        Console.WriteLine("[-] Physical drive opening failed. Error 0x{0:X8}", nativeError);
                        return 1;
                    }
                    Console.WriteLine("[+] Physical drive opening succeeded.");
                    geometry.Acquire(handle);
                    _partitionManager = new PartitionManager(handle, geometry);
                    _partitionManager.Discover();
                    InterpretActivePartitions();
                    if (FeaturesContext.InvariantChecksEnabled) {
                        NtfsMFTFileRecord.AssertMFTRecordCachingInvariance(_partitionManager);
                    }
                    // TODO : Configure TrackedPartitionIndex from command line arguments.
                    foreach (GenericPartition partition in _partitionManager.EnumeratePartitions()) {
                        if (!partition.ShouldCapture) { continue; }
                        NtfsPartition ntfsPartition = partition as NtfsPartition;
                        NtfsPartition.Current = ntfsPartition;

                        // Basic functionnality tests. Don't remove.
                        //ntfsPartition.CountFiles();
                        //ntfsPartition.MonitorBadClusters();
                        //ntfsPartition.ReadBitmap();

                        // Dump bad clusters.
                        ntfsPartition.DumpBadClusters();

                        // Dump UsnJournal
                        PrototypeUsnJournal();
                        new NtfsUsnJournalReader(ntfsPartition).Run();
                    
                        // Dump LogFile
                        // new NtfsLogFileReader(ntfsPartition).Run();

                        // Locate file.
                        // string fileName = @"TEMP\AsciiTes.txt";
                        string fileName = @"$Extend\$UsnJrnl";
                        NtfsIndexEntryHeader* fileDescriptor = ntfsPartition.FindFile(fileName);
                        if (null == fileDescriptor) {
                            throw new System.IO.FileNotFoundException(fileName);
                        }
                        IPartitionClusterData fileData = null;
                        NtfsFileRecord* usnJournalFileRecord =
                            ntfsPartition.GetFileRecord(fileDescriptor->FileReference, ref fileData);
                        if ((null == usnJournalFileRecord) || (null == fileData)) {
                            throw new ApplicationException();
                        }
                        try {
                            usnJournalFileRecord->EnumerateRecordAttributes(
                                delegate (NtfsAttribute* attribute, Stream dataStream) {
                                    attribute->Dump();
                                    return true;
                                });
                            // For debugging purpose.
                            // fileRecord->BinaryDumpContent();

                            // TODO : Do something with the file.
                        }
                        finally {
                            if (null != fileData) { fileData.Dispose(); }
                        }
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

        private static unsafe void PrototypeUsnJournal()
        {
            List<Tuple<uint, ulong>> records = new List<Tuple<uint, ulong>>() {
                new Tuple<uint, ulong>(0x00000000, 0x10000000417F8),
                new Tuple<uint, ulong>(0x00831030, 0x133000000030345),
                new Tuple<uint, ulong>(0x00835CE0, 0x79200000000288E),
                new Tuple<uint, ulong>(0x00839E60, 0x16C0000000059EB),
                new Tuple<uint, ulong>(0x00842DE0, 0x7340000000057C6),
                new Tuple<uint, ulong>(0x00844160, 0x600000018405F),
                new Tuple<uint, ulong>(0x0084A215, 0x8F000000036956),
                new Tuple<uint, ulong>(0x0084EF80, 0x2B0000000020AD),
                new Tuple<uint, ulong>(0x00852F71, 0x3D00000000024B8),
                new Tuple<uint, ulong>(0x00857E50, 0x50B000000007274),
                new Tuple<uint, ulong>(0x00858DB0, 0x235000000000705),
                new Tuple<uint, ulong>(0x0085CD90, 0xD20000000086D7),
                new Tuple<uint, ulong>(0x00860A90, 0x7900000000840C),
                new Tuple<uint, ulong>(0x00862160, 0x6E500000002FC3E),
                new Tuple<uint, ulong>(0x00868190, 0x7F000000004050),
                new Tuple<uint, ulong>(0x0086C570, 0x49E000000003C3E),
                new Tuple<uint, ulong>(0x0086D8F0, 0x160000000F353D),
                new Tuple<uint, ulong>(0x00871DB0, 0x9D000000004053),
                new Tuple<uint, ulong>(0x00875B90, 0x19F000000001DD5),
                new Tuple<uint, ulong>(0x00877B0C, 0x387000000031EA2),
                new Tuple<uint, ulong>(0x0087BE50, 0x840000000041B7),
                new Tuple<uint, ulong>(0x00883CC0, 0x2C4000000006741),
                new Tuple<uint, ulong>(0x00888210, 0x93000000007190),
                new Tuple<uint, ulong>(0x00889710, 0xF30000000DD118),
                new Tuple<uint, ulong>(0x0089759B, 0xA2B000000006C37),
                new Tuple<uint, ulong>(0x0089F050, 0xD400000002EF59),
                new Tuple<uint, ulong>(0x008A3480, 0x4000000030266),
                new Tuple<uint, ulong>(0x008A4780, 0x3D000000122430),
                new Tuple<uint, ulong>(0x008A8FF0, 0x4A0000000F8276),
                new Tuple<uint, ulong>(0x008ACDF0, 0x7000000030267),
                new Tuple<uint, ulong>(0x008B1FF0, 0xF000000030265),
                new Tuple<uint, ulong>(0x008B32F2, 0x10000001E9963),
                new Tuple<uint, ulong>(0x008B7272, 0x5000000030269),
                new Tuple<uint, ulong>(0x008BB022, 0x6000000030268),
                new Tuple<uint, ulong>(0x008BC2A0, 0xC000000121FED),
                new Tuple<uint, ulong>(0x008C4B70, 0x3700000016AC1D),
                new Tuple<uint, ulong>(0x008CEDF0, 0x5000000184830),
                new Tuple<uint, ulong>(0x008DEAD0, 0x1D0000000004401),
                new Tuple<uint, ulong>(0x008E2D20, 0x3B400000000157B),
                new Tuple<uint, ulong>(0x008E4120, 0x3DC0000000045DC),
                new Tuple<uint, ulong>(0x008E8FC0, 0x2800000000E8AE),
                new Tuple<uint, ulong>(0x008F123D, 0x52F0000000010F0),
                new Tuple<uint, ulong>(0x008F72F0, 0x147000000033E17),
                new Tuple<uint, ulong>(0x008FB610, 0x5A0000000305FD),
                new Tuple<uint, ulong>(0x008FCA00, 0x144000000003871),
                new Tuple<uint, ulong>(0x00901140, 0x1E2000000033E33),
                new Tuple<uint, ulong>(0x009054B0, 0x29000000033E34),
                new Tuple<uint, ulong>(0x00906AB0, 0x3420000000035B2),
                new Tuple<uint, ulong>(0x0090B250, 0xD7000000004C47),
                new Tuple<uint, ulong>(0x0090F710, 0xA9000000005C58),
                new Tuple<uint, ulong>(0x00913660, 0xC9000000002451),
                new Tuple<uint, ulong>(0x0091A1E0, 0xA8000000004E3B),
                new Tuple<uint, ulong>(0x009280D4, 0x2920000000DD0CA),
                new Tuple<uint, ulong>(0x0092C280, 0x276000000031B0A),
                new Tuple<uint, ulong>(0x00934D30, 0x6F0000000F179E),
                new Tuple<uint, ulong>(0x0093B220, 0xB20000000D3A3F),
                new Tuple<uint, ulong>(0x0093F0C4, 0x5400000002BC62),
                new Tuple<uint, ulong>(0x00940380, 0xA7000000004A8C),
                new Tuple<uint, ulong>(0x00945C6F, 0x6F000000008D46),
                new Tuple<uint, ulong>(0x0094C310, 0x3A0000000D9AA7),
                new Tuple<uint, ulong>(0x00954910, 0x4C0000000D9A91)
            };

            NtfsPartition partition = NtfsPartition.Current;
            ulong mftEntrySize = partition.ClusterSize / partition.MFTEntryPerCluster;
            IPartitionClusterData data = null;
            try {
                ulong baseFileRecord = 0;
                foreach (Tuple<uint, ulong> record in records) {
                    ulong fileReference = record.Item2;
                    NtfsFileRecord* fileRecord = partition.GetFileRecord(fileReference, ref data);
                    if (0 == baseFileRecord) {
                        baseFileRecord = fileRecord->BaseFileRecord;
                    }
                    if (fileRecord->BaseFileRecord != baseFileRecord) {
                        fileRecord->BinaryDump();
                        fileRecord->Dump();
                        throw new ApplicationException();
                    }
                    fileRecord->EnumerateRecordAttributes(delegate (NtfsAttribute* value, Stream attributeDataStream) {
                        if (null != attributeDataStream) {
                            throw new NotSupportedException();
                        }
                        if (value->IsResident) {
                            throw new NotSupportedException();
                        }
                        IClusterStream dataStream = (IClusterStream)((NtfsNonResidentAttribute*)value)->OpenDataClusterStream();
                        dataStream.SeekToNextNonEmptyCluster();
                        NtfsUsnJournalReader.UsnRecordV2* currentUsnRecord = null;
                        while (true) {
                            using (IPartitionClusterData clusterData = dataStream.ReadNextCluster()) {
                                if (null == clusterData) {
                                    // Done with this stream.
                                    return true;
                                }
                                for (uint offset = 0; offset < clusterData.DataSize; offset += currentUsnRecord->RecordLength) {
                                    currentUsnRecord = (NtfsUsnJournalReader.UsnRecordV2*)(clusterData.Data + offset);
                                    if (0 == currentUsnRecord->RecordLength) {
                                        break;
                                    }
                                    if (2 != currentUsnRecord->MajorVersion) {
                                        Helpers.BinaryDump((byte*)currentUsnRecord,
                                            (uint)Marshal.SizeOf<NtfsUsnJournalReader.UsnRecordV2>());
                                        throw new NotSupportedException();
                                    }
                                    if (0 != currentUsnRecord->MinorVersion) {
                                        throw new NotSupportedException();
                                    }
                                    currentUsnRecord->Dump();
                                }
                            }
                        }
                    },
                    NtfsAttributeType.AttributeData, null);
                    fileRecord->DumpAttributes();
                }
                return;
            }
            finally {
                if (null != data) { data.Dispose(); }
            }
        }

        private static PartitionManager _partitionManager;
    }
}
