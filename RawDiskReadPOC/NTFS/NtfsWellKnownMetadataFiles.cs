
namespace RawDiskReadPOC.NTFS
{
    internal enum NtfsWellKnownMetadataFiles : int
    {
        MFT = 0,
        MFTMirror,
        LogFile,
        Volume,
        AttributesDefinition,
        Root,
        Bitmap,
        Boot,
        BadClusters,
        Secure,
        UpperCase,
        Extend,
        Quota,
        ObjectIdentifiers,
        Reparse,
        RmMetadata,
        Repair
    }
}
