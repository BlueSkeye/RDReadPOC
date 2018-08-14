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

        private static unsafe bool IsDollarJAttribute(NtfsAttribute* candidate)
        {
            bool isJAttribute = "$J" == candidate->Name;
            return isJAttribute;
        }

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
                        partition.GetFileRecord(fileDescriptor->FileReference, ref fileData);
                    fileRecord->AssertRecordType();
                    NtfsAttribute* jAttribute = fileRecord->GetAttribute(NtfsAttributeType.AttributeData,
                        1, _isDollarJAttributeNameFilter);
                    if (null == jAttribute) {
                        throw new ApplicationException();
                    }
                    jAttribute->Dump();
                    if (jAttribute->IsResident) {
                        NtfsResidentAttribute* jReAttribute = (NtfsResidentAttribute*)jAttribute;
                        jReAttribute->Dump();
                    }
                    else {
                        NtfsNonResidentAttribute* jNrAttribute = (NtfsNonResidentAttribute*)jAttribute;
                        jNrAttribute->Dump();
                    }
                    throw new NotImplementedException();
                    NtfsAttribute* rawAttribute =
                        fileRecord->GetAttribute(NtfsAttributeType.AttributeData, 1);
                    if (null == rawAttribute) {
                        throw new ApplicationException();
                    }
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
                    }
                    else {
                        throw new NotSupportedException();
                    }
                    rawAttribute = fileRecord->GetAttribute(NtfsAttributeType.AttributeData, 2);
                    if (null == rawAttribute) {
                        throw new ApplicationException();
                    }
                    if ("$J" != rawAttribute->Name) {
                        throw new ApplicationException();
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

        private static unsafe NtfsFileRecord.AttributeNameFilterDelegate _isDollarJAttributeNameFilter = IsDollarJAttribute;

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

        [Flags()]
        internal enum UsnReason : uint
        {
            /// <summary>The data in the file or directory is overwritten.</summary>
            Overwwrite = 0x00000001,
            /// <summary>The file or directory is extended (added to).</summary>
            DataExtend = 0x00000002,
            /// <summary>The file or directory is truncated.</summary>
            DataTruncation = 0x00000004,
            /// <summary>The data in one or more named data streams for a file is overwritten.</summary>
            NamedDataStreamOverwritten = 0x00000010,
            /// <summary>The one or more named data streams for a file are extended (added to)</summary>
            DataStreamExtended = 0x00000020,
            /// <summary>The one or more named data streams for a file is truncated.</summary>
            NamedDataStreamTruncated = 0x00000040,
            /// <summary>The file or directory is created for the first time.</summary>
            Create = 0x00000100,
            /// <summary>The file or directory is deleted.</summary>
            Deleted = 0x00000200,
            /// <summary>The user made a change to the extended attributes of a file or directory. These
            /// NTFS file system attributes are not accessible to Windows-based applications.</summary>
            ExtendedAttributeChange = 0x00000400,
            /// <summary>A change is made in the access rights to a file or directory.</summary>
            SecurityChanged = 0x00000800,
            /// <summary>The file or directory is renamed, and the file name in the USN_RECORD_V2
            /// structure is the previous name.</summary>
            RenamedToOldName = 0x00001000,
            /// <summary>A file or directory is renamed, and the file name in the USN_RECORD_V2 structure
            /// is the new name.</summary>
            RenamedToNewName = 0x00002000,
            /// <summary>A user changes the FILE_ATTRIBUTE_NOT_CONTENT_INDEXED attribute. That is, the
            /// user changes the file or directory from one where content can be indexed to one where
            /// content cannot be indexed, or vice versa.Content indexing permits rapid searching of
            /// data by building a database of selected content.</summary>
            IndexableChange = 0x00004000,
            /// <summary>A user has either changed one or more file or directory attributes (for example,
            /// the read-only, hidden, system, archive, or sparse attribute), or one or more time stamps.</summary>
            InfoChanged = 0x00008000,
            /// <summary>An NTFS file system hard link is added to or removed from the file or directory.
            /// An NTFS file system hard link, similar to a POSIX hard link, is one of several directory
            /// entries that see the same file or directory.</summary>
            HardLinkChange = 0x00010000,
            /// <summary>The compression state of the file or directory is changed from or to compressed.</summary>
            CompressionChange = 0x00020000,
            /// <summary>The file or directory is encrypted or decrypted.</summary>
            EncryptionChange = 0x00040000,
            /// <summary>The object identifier of a file or directory is changed.</summary>
            ObjectIdChanged = 0x00080000,
            /// <summary>The reparse point that is contained in a file or directory is changed, or a
            /// reparse point is added to or deleted from a file or directory.</summary>
            ReparsePointChanged = 0x00100000,
            /// <summary>A named stream is added to or removed from a file, or a named stream is renamed.</summary>
            StreamChange = 0x00200000,
            /// <summary>The given stream is modified through a TxF transaction.</summary>
            StreamChangeWithTXF = 0x00400000,
            /// <summary>A user changed the state of the FILE_ATTRIBUTE_INTEGRITY_STREAM attribute for
            /// the given stream. On the ReFS file system, integrity streams maintain a checksum of all
            /// data for that stream, so that the contents of the file can be validated during read or
            /// write operations.</summary>
            IntegrityChange = 0x00800000,
            /// <summary>The file or directory is closed.</summary>
            Close = 0x80000000,
        }

        /// <summary></summary>
        /// <remarks>Documented in 
        /// https://docs.microsoft.com/en-us/windows/desktop/api/winioctl/ns-winioctl-usn_record_v2
        /// </remarks>
        internal struct UsnRecordV2
        {
            internal uint RecordLength;
            internal ushort MajorVersion;
            internal ushort MinorVersion;
            internal ulong FileReferenceNumber;
            internal ulong ParentFileReferenceNumber;
            internal ulong Usn;
            internal ulong TimeStamp;
            internal UsnReason Reason;
            internal uint SourceInfo;
            internal uint SecurityId;
            internal uint FileAttributes;
            internal ushort FileNameLength;
            internal ushort FileNameOffset;
            // char[] FileName
        }
    }
}
