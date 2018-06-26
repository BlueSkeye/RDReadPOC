using System;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Until NTFS version 3.0, only filename attributes were indexed. If the HasTrailingVcn flag
    /// of a DIRECTORY_ENTRY structure is set, the last eight bytes of the directory entry contain the VCN
    /// of the index block that holds the entries immediately preceding the current entry</remarks>
    [StructLayout(LayoutKind.Explicit)]
    internal struct NtfsDirectoryIndexEntry
    {
        internal unsafe void Dump()
        {
            GenericEntry.Dump();
            Console.WriteLine("\t\tFRN 0x{0:X16}",
                FileReferenceNumber);
            if (0 < GenericEntry.EntryLength) {
                fixed(NtfsDirectoryIndexEntry* pDirectoryEntry = &this) {
                    NtfsFileNameAttribute* pFileName =
                        (NtfsFileNameAttribute*)((byte*)pDirectoryEntry + sizeof(NtfsIndexEntry));
                    Console.WriteLine("\t\tName : {0}",
                        pFileName->GetName());
                }
            }
            else {
                Console.WriteLine("\t\tUNNAMED");
            }
        }

        [FieldOffset(0)]
        internal NtfsIndexEntry GenericEntry;
        /// <summary>The file reference number of the file described by the directory entry</summary>
        [FieldOffset(0)]
        internal ulong FileReferenceNumber;
    }
}
