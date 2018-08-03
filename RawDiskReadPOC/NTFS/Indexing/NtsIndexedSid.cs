using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawDiskReadPOC.NTFS.Indexing
{
    /// <summary>$O index in FILE_Extend/$Quota: SID of the owner of the user_id.</summary>
    internal struct NtsIndexedSid
    {
        internal NtfsIndexEntryHeader Header;
    }
}
