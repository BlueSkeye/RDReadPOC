using System;
using System.Threading;

using RawDiskReadPOC.NTFS.Indexing;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>
    /// v2: http://msdn.microsoft.com/en-us/library/aa365722(v=vs.85).aspx
    /// v3: http://msdn.microsoft.com/en-us/library/hh802708(v=vs.85).aspx
    /// Example of other code:
    /// http://code.google.com/p/parser-usnjrnl/
    /// </remarks>
    internal class NtfsUsnJournalReader
    {
        internal NtfsUsnJournalReader(NtfsPartition partition)
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
                // Note : We could also use the NtfsWellKnownMetadataFiles.Extend entry to
                // locate the directory, then find the $UsnJrnl entry directly from there.
                string fileName = @"$UsnJrnl";
                NtfsIndexEntryHeader* fileDescriptor = partition.FindFile(fileName, NtfsWellKnownMetadataFiles.Extend);
                if (null == fileDescriptor) {
                    throw new System.IO.FileNotFoundException(fileName);
                }
                IPartitionClusterData fileData = null;
                try {
                    NtfsFileRecord* fileRecord =
                        partition.GetFileRecord(fileDescriptor->FileReference, out fileData);
                    fileRecord->AssertRecordType();
                    fileRecord->DumpAttributes();
                    NtfsAttribute* rawAttribute = fileRecord->GetAttribute(NtfsAttributeType.AttributeData);
                    if ("$Max" != rawAttribute->Name) {
                        throw new ApplicationException();
                    }
                    if (rawAttribute->IsResident) {
                        NtfsResidentAttribute* reMaxAttribute = (NtfsResidentAttribute*)rawAttribute;
                        if (FeaturesContext.InvariantChecksEnabled) {
                            if (0x20 != reMaxAttribute->ValueLength) {
                                throw new ApplicationException();
                            }
                        }
                        MaxAttribute* maxAttribute = (MaxAttribute*)((byte*)reMaxAttribute + reMaxAttribute->ValueOffset);
                        int i = 1;
                    }
                    else {
                        throw new NotSupportedException();
                    }
                    throw new NotImplementedException();
                }
                finally {
                    if (null != fileData) { fileData.Dispose(); }
                }
            }
            finally {
                if (null != clusterData) {
                    clusterData.Dispose();
                }
            }
        }

        private struct MaxAttribute
        {
            /// <summary>The maximum size of log data.</summary>
            internal ulong MaximumSize;
            /// <summary>The size of allocated area when new log data is saved</summary>
            internal ulong AllocationSize;
            /// <summary>The creation time of "$UsnJrnl" file(FILETIME)</summary>
            internal ulong UsnId;
            /// <summary>The least value of USN in current records With this value,
            /// investigator can approach the start point of first record within "$J"
            /// attribute</summary>
            internal ulong LowestValidUsn;
        }
    }
}
