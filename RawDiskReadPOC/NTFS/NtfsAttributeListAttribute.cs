
namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>The attribute list attribute is always nonresident and consists of an array of
    /// ATTRIBUTE_LIST structures. An attribute list attribute is only needed when the attributes
    /// of a file do not fit in a single MFT record. Possible reasons for overflowing a single
    /// MFT entry include:
    /// - The file has a large numbers of alternate names (hard links)
    /// - The attribute value is large, and the volume is badly fragmented
    /// - The file has a complex security descriptor (does not affect NTFS 3.0)
    /// - The file has many streams</remarks>
    internal class NtfsAttributeListAttribute
    {
        /// <summary>The type of the attribute.</summary>
        internal NtfsAttributeType AttributeType;
        /// <summary>The size, in bytes, of the attribute list entry</summary>
        internal ushort Length;
        /// <summary>The size, in characters, of the name (if any) of the attribute.</summary>
        internal byte NameLength;
        /// <summary>The offset, in bytes, from the start of the ATTRIBUTE_LIST structure to the
        /// attribute name.The attribute name is stored as a Unicode string.</summary>
        internal byte NameOffset;
        /// <summary>The lowest valid Virtual Cluster Number (VCN) of this portion of the
        /// attribute value.</summary>
        internal ulong LowVcn;
        /// <summary>The file reference number of the MFT entry containing the NONRESIDENT_ATTRIBUTE
        /// structure for this portion of the attribute value.</summary>
        internal ulong FileReferenceNumber;
        /// <summary>A numeric identifier for the instance of the attribute.</summary>
        internal ushort AttributeNumber;
        internal ushort Alignment1;
        internal ushort Alignment2;
        internal ushort Alignment3;
    }
}
