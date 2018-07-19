using System;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct NtfsDirectoryIndexEntry
    {
        internal unsafe string Name
        {
            get
            {
                if (0 < GenericEntry.EntryLength) {
                    fixed (NtfsDirectoryIndexEntry* pDirectoryEntry = &this) {
                        NtfsFileNameAttribute* pFileName =
                            (NtfsFileNameAttribute*)((byte*)pDirectoryEntry + sizeof(NtfsIndexEntry));
                        return pFileName->GetName();
                    }
                }
                return null;
            }
        }

        internal unsafe void Dump()
        {
            GenericEntry.Dump();
            Console.WriteLine("\t\t\tFRN 0x{0:X16}",
                FileReferenceNumber);
            Console.WriteLine("\t\t\tName : {0}", Name ?? "UNNAMED");
        }

        [FieldOffset(0)]
        internal NtfsIndexEntry GenericEntry;
        /// <summary>The file reference number of the file described by the directory entry</summary>
        [FieldOffset(0)]
        internal ulong FileReferenceNumber;
    }
}
