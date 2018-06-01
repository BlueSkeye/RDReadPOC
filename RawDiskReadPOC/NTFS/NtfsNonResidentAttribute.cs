
namespace RawDiskReadPOC.NTFS
{
    internal class NtfsNonResidentAttribute
    {
        /// <summary>An ATTRIBUTE structure containing members common to resident and
        /// nonresident attributes.</summary>
        internal NtfsAttribute Attribute;
        /// <summary>The lowest valid Virtual Cluster Number (VCN) of this portion of the
        /// attribute value. Unless the attribute value is very fragmented(to the extent
        /// that an attribute list is needed to describe it), there is only one portion of
        /// the attribute value, and the value of LowVcn is zero.</summary>
        internal ulong LowVcn;
        /// <summary>The highest valid VCN of this portion of the attribute value.</summary>
        internal ulong HighVcn;
        /// <summary>The offset, in bytes, from the start of the structure to the run array that
        /// contains the mappings between VCNs and Logical Cluster Numbers(LCNs).</summary>
        internal ushort RunArrayOffset;
        /// <summary>The compression unit for the attribute expressed as the logarithm to the
        /// base two of the number of clusters in a compression unit. If CompressionUnit is zero,
        /// the attribute is not compressed.</summary>
        internal byte CompressionUnit;
        internal byte Alignment1;
        internal uint Alignment2;
        /// <summary>The size, in bytes, of disk space allocated to hold the attribute value</summary>
        internal ulong AllocatedSize;
        /// <summary>The size, in bytes, of the attribute value.This may be larger than the AllocatedSize
        /// if the attribute value is compressed or sparse.</summary>
        internal ulong DataSize;
        /// <summary>The size, in bytes, of the initialized portion of the attribute value.</summary>
        internal ulong InitializedSize;
        /// <summary>The size, in bytes, of the attribute value after compression. This member is only
        /// present when the attribute is compressed.</summary>
        internal ulong CompressedSize;
    }
}
