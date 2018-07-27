using System;
using System.Threading;

namespace RawDiskReadPOC.NTFS
{
    internal class NtfsLogFileReader
    {
        internal NtfsLogFileReader(NtfsPartition partition)
        {
            Partition = partition ?? throw new ArgumentNullException("partition");
        }

        internal NtfsPartition Partition { get; private set;}

        internal void Run(bool background = false)
        {
            if (background) {
                new Thread(_Run) {
                    IsBackground = true
                }.Start();
            }
            else {
                _Run();
            }
        }

        private unsafe void _Run()
        {
            NtfsPartition partition = Partition;
            IPartitionClusterData clusterData = null;
            try {
                NtfsFileRecord* fileRecord = partition.GetFileRecord(
                    NtfsWellKnownMetadataFiles.LogFile, out clusterData);
                fileRecord->AssertRecordType();
                throw new NotImplementedException();
            }
            finally {
                if (null != clusterData) {
                    clusterData.Dispose();
                }
            }
        }
    }
}
