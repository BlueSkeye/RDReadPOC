using System;

namespace RawDiskReadPOC.NTFS.Indexing
{
    /* $I30 index in directories. */
    internal struct NtfsIndexedFileNameAttribute
    {
        internal unsafe string Name
        {
            get
            {
                if (0 < Header.EntryLength)
                {
                    fixed (NtfsIndexedFileNameAttribute* pEntry = &this) {
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
            fixed (NtfsIndexedFileNameAttribute* rawData = &this) {
                Helpers.BinaryDump((byte*)rawData, rawData->Header.EntryLength);
            }
        }

        internal unsafe void Dump()
        {
            Header.Dump();
            Console.WriteLine(Helpers.Indent(3) + "Name : {0}", Name ?? "UNNAMED");
        }

        internal NtfsIndexEntryHeader Header;
        /// <summary>The key of the indexed attribute. NOTE: Only present if INDEX_ENTRY_END
        /// bit in flags is not set. NOTE: On NTFS versions before 3.0 the only valid key is
        /// the FILENAME_ATTR.</summary>
        internal NtfsFileNameAttribute Filename;
    }
}
