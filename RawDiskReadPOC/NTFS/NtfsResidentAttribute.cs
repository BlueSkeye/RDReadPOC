
namespace RawDiskReadPOC.NTFS
{
    internal class NtfsResidentAttribute
    {
        /// <summary>An ATTRIBUTE structure containing members common to resident and
        /// nonresident attributes.</summary>
        internal NtfsAttribute Attribute;
        /// <summary>The size, in bytes, of the attribute value.</summary>
        internal uint ValueLength;
        /// <summary>The offset, in bytes, from the start of the structure to the attribute
        /// value.</summary>
        internal ushort ValueOffset;
        /// <summary>A bit array of flags specifying properties of the attribute. The values
        /// defined include: Indexed 0x0001</summary>
        internal ushort Flags;
    }
}
