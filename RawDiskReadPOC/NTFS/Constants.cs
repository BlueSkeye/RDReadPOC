using System.Text;

namespace RawDiskReadPOC.NTFS
{
    internal static class Constants
    {
        /// <summary>BAAD Found in all ntfs record containing records.
        /// Failed multi sector transfer was detected.</summary>
        internal static readonly uint BaadRecordNumber = 0x44414142;
        /// <summary>CHKD in $LogFile/$DATA. Record modified by CHKDSK.EXE
        /// (May be found in $MFT/$DATA, also?)</summary>
        internal static readonly uint ChkdRecordNumber = 0x444b4843;
        /// <summary>Found in $LogFile/$DATA when a page is full of 0xff bytes and is thus
        /// not initialized.Page must be initialized before using it.</summary>
        internal static readonly uint EmpryRecordMarker = 0xFFFFFFFF;
        /// <summary>FILE in $MFT/$DATA. </summary>
        internal static readonly uint FileRecordMarker = 0x454C4946;
        /// <summary>? (NTFS 3.0+?) in $MFT/$DATA. </summary>
        internal static readonly uint HoleRecordMarker = 0x454c4f48;
        /// <summary>INDX in $MFT/$DATA. </summary>
        internal static readonly uint IndxRecordMarker = 0x58444e49;
        /// <summary>The NTFS oem_id "NTFS    "</summary>
        internal static readonly byte[] OEMID = Encoding.ASCII.GetBytes("NTFS    ");
        /// <summary>RCDR in $LogFile/$DATA. log record page.</summary>
        internal static readonly uint RcrdRecordMarker = 0x44524352;
        /// <summary>RSTR in $LogFile/$DATA. log restart page.</summary>
        internal static readonly uint RstrRecordMarker = 0x52545352;
        internal const byte SID_REVISION = 1; /* Current revision level. */
        internal const int SID_MAX_SUB_AUTHORITIES = 15;    /* Maximum number of those. */
        internal const int SID_RECOMMENDED_SUB_AUTHORITIES = 1;	/* Will change to around 6 in a future revision. */
    }
}
