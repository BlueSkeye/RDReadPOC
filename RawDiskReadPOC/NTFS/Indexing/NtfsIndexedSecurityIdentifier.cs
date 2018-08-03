
namespace RawDiskReadPOC.NTFS.Indexing
{
    /* $SII index in $Secure. */
    internal struct NtfsIndexedSecurityIdentifier
    {
        internal NtfsIndexHeader Header;
        /// <summary>The key of the indexed attribute. NOTE: Only present if INDEX_ENTRY_END
        /// bit in flags is not set. The security_id assigned to the descriptor.
        /// The collation type is COLLATION_NTOFS_ULONG.</summary>
        internal uint SecurityId;
    }
}
