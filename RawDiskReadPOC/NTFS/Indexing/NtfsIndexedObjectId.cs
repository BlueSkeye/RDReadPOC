
namespace RawDiskReadPOC.NTFS.Indexing
{
    /// <summary>$O index in FILE_Extend/$ObjId: The object_id of the mft record found in
    /// the data part of the index.</summary>
    internal struct NtfsIndexedObjectId
    {
        /// <summary>The key of the indexed attribute. NOTE: Only present if INDEX_ENTRY_END
        /// bit in flags is not set.</summary>
        internal NtfsIndexEntryHeader Header;
        internal NtfsGUID Guid;
    }

    /// <summary>FILE_Extend/$ObjId contains an index named $O. This index contains all
    /// object_ids present on the volume as the index keys and the corresponding
    /// mft_record numbers as the index entry data parts.The data part(defined below) also
    /// contains three other object_ids:
    /// birth_volume_id - object_id of FILE_Volume on which the file was first created.
    /// Optional(i.e.can be zero).
    /// birth_object_id - object_id of file when it was first created.Usually equals the
    /// object_id.Optional (i.e.can be zero).
    /// domain_id   - Reserved(always zero).</summary>
    internal struct NtfsIndexedObjectData
    {
        /// <summary>Mft record containing the object_id in the index entry key. </summary>
        internal long mft_reference;
        internal NtfsGUID BirthVolumeId;
        internal NtfsGUID BirthObjectId;
        internal NtfsGUID DomainId;
    }
}
