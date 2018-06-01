
namespace RawDiskReadPOC.NTFS
{
    internal class NtfsAttribute
    {
        internal NtfsAttributeType AttributeType;
        /// <summary>The size, in bytes, of the resident part of the attribute.</summary>
        internal uint Length;
        /// <summary>Specifies, when true, that the attribute value is nonresident.</summary>
        internal byte Nonresident;
        /// <summary>The size, in characters, of the name (if any) of the attribute.</summary>
        internal byte NameLength;
        /// <summary>The offset, in bytes, from the start of the structure to the attribute name.
        /// The attribute name is stored as a Unicode string.</summary>
        internal ushort NameOffset;
        /// <summary>A bit array of flags specifying properties of the attribute. The values
        /// defined include: Compressed 0x0001</summary>
        internal ushort Flags;
        /// <summary>A numeric identifier for the instance of the attribute.</summary>
        internal ushort AttributeNumber;
    }
}
