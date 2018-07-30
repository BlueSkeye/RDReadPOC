
namespace RawDiskReadPOC.NTFS
{
    /// <summary>System files mft record numbers. All these files are always marked as used
    /// in the bitmap attribute of the mft; presumably in order to avoid accidental
    /// allocation for random other mft records.Also, the sequence number for each of the
    /// system files is always equal to their mft record number and it is never modified.</summary>
    internal enum NtfsWellKnownMetadataFiles : int
    {
        /// <summary>Master file table (mft). Data attribute contains the entries and bitmap
        /// attribute records which ones are in use (bit==1).</summary>
        MFT = 0,
        /// <summary>Mft mirror: copy of first four mft records in data attribute. If cluster
        /// size > 4kiB, copy of first N mft records, with
        /// N = cluster_size / mft_record_size.</summary>
        MFTMirror,
        /// <summary>Journalling log in data attribute.</summary>
        LogFile,
        /// <summary>Volume name attribute and volume information attribute (flags and ntfs
        /// version). Windows refers to this file as volume DASD (Direct Access Storage
        /// Device).</summary>
        Volume,
        /// <summary>Array of attribute definitions in data attribute.</summary>
        AttributesDefinition,
        /// <summary>Root directory.</summary>
        Root,
        /// <summary>Allocation bitmap of all clusters (lcns) in data attribute.summary>
        Bitmap,
        /// <summary>Boot sector (always at cluster 0) in data attribute.</summary>
        Boot,
        /// <summary>Contains all bad clusters in the non-resident data attribute.</summary>
        BadClusters,
        /// <summary>Shared security descriptors in data attribute and two indexes into the
        /// descriptors. Appeared in Windows 2000. Before that, this file was named $Quota
        /// but was unused.</summary>
        Secure,
        /// <summary>Uppercase equivalents of all 65536 Unicode characters in data attribute.</summary>
        UpperCase,
        /// <summary>Directory containing other system files (eg. $ObjId, $Quota, $Reparse
        /// and $UsnJrnl). This is new to NTFS3.0.</summary>
        Extend,
        Quota,
        ObjectIdentifiers,
        Reparse,
        RmMetadata,
        Repair
    }
}
