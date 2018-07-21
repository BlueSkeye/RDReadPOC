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

        internal unsafe void BinaryDump()
        {
            fixed (NtfsDirectoryIndexEntry* rawData = &this) {
                Helpers.BinaryDump((byte*)rawData, rawData->GenericEntry.EntryLength);
            }
        }

        internal unsafe ulong ChildNodeVCN
        {
            get
            {
                // The child entry VCN is an ulong at the very end of the entry.
                fixed(NtfsDirectoryIndexEntry* pEntry = &this) {
                    return *(ulong*)((byte*)pEntry + pEntry->GenericEntry.EntryLength - sizeof(ulong));
                }
            }
        }

        internal unsafe void Dump()
        {
            GenericEntry.Dump();
            Console.WriteLine(Helpers.Indent(3) + "FRN 0x{0:X16}",
                FileReferenceNumber);
            Console.WriteLine(Helpers.Indent(3) + "Name : {0}", Name ?? "UNNAMED");
        }

        [FieldOffset(0)]
        internal NtfsIndexEntry GenericEntry;
        /// <summary>The file reference number of the file described by the directory entry</summary>
        [FieldOffset(0)]
        internal ulong FileReferenceNumber;
    }
}
