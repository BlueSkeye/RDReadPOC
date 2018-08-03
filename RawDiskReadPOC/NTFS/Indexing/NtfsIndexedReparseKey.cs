
namespace RawDiskReadPOC.NTFS.Indexing
{
    /// <summary>$R index in FILE_Extend/$Reparse.</summary>
    internal struct NtfsIndexedReparseKey
    {
        internal NtfsIndexEntryHeader Header;
        /// <summary>The system file FILE_Extend/$Reparse contains an index named $R
        /// listing all reparse points on the volume. The index entry keys are as defined
        /// below. Note, that there is no index data associated with the index entries.
        /// The index entries are sorted by the index key file_id. The collation rule is
        /// COLLATION_NTOFS_ULONGS.FIXME: Verify whether the reparse_tag is not the primary
        /// key / is not a key at all. (AIA). Reparse point type (inc. flags).
        /// The key of the indexed attribute. NOTE: Only present if INDEX_ENTRY_END bit
        /// in flags is not set.</summary>
        internal int ReparseTag;
        /// <summary>Mft record of the file containing the reparse point attribute.</summary>
        internal long FileId;
    }
}
