
namespace RawDiskReadPOC.NTFS
{
    /// <summary>System defined attributes (32-bit). Each attribute type has a corresponding
    /// attribute name (Unicode string of maximum 64 character length) as described by the
    /// attribute definitions present in the data attribute of the $AttrDef system file. On
    /// NTFS 3.0 volumes the names are just as the types are named in the below defines
    /// exchanging AT_ for the dollar sign($).  If that is not a revealing choice of symbol
    /// I do not know what is...</summary>
    internal enum NtfsAttributeType : uint
    {
        Unused = 0,
        AttributeStandardInformation = 0x10,
        AttributeAttributeList = 0x20,
        AttributeFileName = 0x30,
        AttributeObjectId = 0x40,
        AttributeSecurityDescriptor = 0x50,
        AttributeVolumeName = 0x60,
        AttributeVolumeInformation = 0x70,
        AttributeData = 0x80,
        AttributeIndexRoot = 0x90,
        AttributeIndexAllocation = 0xA0,
        AttributeBitmap = 0xB0,
        AttributeReparsePoint = 0xC0,
        AttributeEAInformation = 0xD0,
        AttributeEA = 0xE0,
        AttributePropertySet = 0xF0,
        AttributeLoggedUtilityStream = 0x100,
        AttributeNone = 0xFFFFFFFF,
    }
}
