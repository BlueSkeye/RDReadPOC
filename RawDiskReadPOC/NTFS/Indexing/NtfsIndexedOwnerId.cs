
namespace RawDiskReadPOC.NTFS.Indexing
{
    /// <summary>$Q index in FILE_Extend/$Quota: user_id of the owner of the quota control
    /// entry in the data part of the index.</summary>
    internal struct NtfsIndexedOwnerId
    {
        internal NtfsIndexEntryHeader Header;
        /// <summary>The key of the indexed attribute. NOTE: Only present if INDEX_ENTRY_END
        /// bit in flags is not set.</summary>
        internal int OwnerId;
    }
}
