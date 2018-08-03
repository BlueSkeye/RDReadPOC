using System;

namespace RawDiskReadPOC.NTFS.Indexing
{
    /// <summary>The system file FILE_Extend/$Quota contains two indexes $O and $Q. Quotas
    /// are on a per volume and per user basis. The $Q index contains one entry for each
    /// existing user_id on the volume. The index key is the user_id of the user/group
    /// owning this quota control entry, i.e.the key is the owner_id. The user_id of the
    /// owner of a file, i.e.the owner_id, is found in the standard information attribute.
    /// The collation rule for $Q is COLLATION_NTOFS_ULONG. The $O index contains one entry
    /// for each user/group who has been assigned a quota on that volume. The index key
    /// holds the SID of the user_id the entry belongs to, i.e.the owner_id. The collation
    /// rule for $O is COLLATION_NTOFS_SID. The $O index entry data is the user_id of the
    /// user corresponding to the SID. This user_id is used as an index into $Q to find the
    /// quota control entry associated with the SID. The $Q index entry data is the quota
    /// control entry and is defined below.</summary>
    internal struct NtfsQuotaControlEntry
    {
        /// <summary>Currently equals 2.</summary>
        internal int Version;
        /// <summary>Flags describing this quota entry.</summary>
        internal _Flags Flags;
        /// <summary>How many bytes of the quota are in use.</summary>
        internal ulong BytesUsed;
        /// <summary>Last time this quota entry was changed.</summary>
        internal long ChangeTime;
        /// <summary>Soft quota (-1 if not limited).</summary>
        internal long Threshold;
        /// <summary>Hard quota (-1 if not limited).</summary>
        internal long Limit;
        /// <summary>How long the soft quota has been exceeded.</summary>
        internal long ExceededTime;
        /// <summary>The SID of the user/object associated with this quota entry.Equals
        /// zero for the quota defaults entry (and in fact on a WinXP volume, it is not
        /// present at all).</summary>
        internal SID Sid;

        [Flags()]
        internal enum _Flags
        {
            DefaultLimits = 0x00000001,
            LimitReached = 0x00000002,
            IDDeleted = 0x00000004,

            /// <summary>This is a bit mask for the user quota flags.</summary>
            UserMask = 0x00000007,

            /* These flags are only present in the quota defaults index entry, i.e. in the
             * entry where owner_id = QUOTA_DEFAULTS_ID. */
            TrackingEnabled = 0x00000010,
            EnforcmentEnabled = 0x00000020,
            TrackingRequested = 0x00000040,
            LogThreshold = 0x00000080,

            LogLimi = 0x00000100,
            OutOfData = 0x00000200,
            Corrupt = 0x00000400,
            PendingDeletes = 0x00000800
        }
    }
}
