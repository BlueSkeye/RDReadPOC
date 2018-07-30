using System;

namespace RawDiskReadPOC.NTFS.Indexing
{
    /// <summary></summary>
    internal struct NtfsFilenameIndexEntry
    {
        internal unsafe string Name
        {
            get
            {
                if (0 < Header.EntryLength) {
                    fixed (NtfsFilenameIndexEntry* pEntry = &this) {
                        NtfsFileNameAttribute* pFileName =
                            (NtfsFileNameAttribute*)((byte*)pEntry + sizeof(NtfsIndexEntryHeader));
                        return pFileName->GetName();
                    }
                }
                return null;
            }
        }

        internal unsafe void BinaryDump()
        {
            fixed (NtfsFilenameIndexEntry* rawData = &this) {
                Helpers.BinaryDump((byte*)rawData, rawData->Header.EntryLength);
            }
        }

        internal unsafe void Dump()
        {
            Header.Dump();
            Console.WriteLine(Helpers.Indent(3) + "Name : {0}", Name ?? "UNNAMED");
        }

        internal NtfsIndexEntryHeader Header;
    }
}
