
namespace RawDiskReadPOC.NTFS.Indexing
{
    /// <summary>$SDH index in $Secure. The keys are sorted first by hash and then by security_id.
    /// The collation rule is COLLATION_NTOFS_SECURITY_HASH.</summary>
    internal struct NtfsIndexedSecurityIdentifierHash
    {
        internal NtfsIndexEntryHeader Header;
        // The key of the indexed attribute. NOTE: Only present if INDEX_ENTRY_END bit in flags
        // is not set.
        /// <summary>Hash of the security descriptor.</summary>
        internal int Hash;
        /// <summary>The security_id assigned to the descriptor.</summary>
        internal int SecurityId;

        /// <summary></summary>
        internal struct Data
        {
            /// <summary>Hash of the security descriptor.</summary>
            internal int Hash;
            /// <summary>The security_id assigned to the descriptor.</summary>
            internal int SecurityId;
            /// <summary>Byte offset of this entry in the $SDS stream.</summary>
            internal ulong Offset;
            /// <summary>Size in bytes of this entry in $SDS stream.</summary>
            internal uint Length;
            /// <summary>Effectively padding, this is always either "II" in Unicode or zero.
            /// This field is not counted in the data_length specified by the index entry.</summary>
            internal byte Magic0;
            internal byte Magic1;
        }
    }
}
