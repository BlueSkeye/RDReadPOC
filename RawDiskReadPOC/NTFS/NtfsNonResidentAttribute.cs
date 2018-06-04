using System;

namespace RawDiskReadPOC.NTFS
{
    internal struct NtfsNonResidentAttribute
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

        internal unsafe void DecodeRunArray()
        {
            fixed (NtfsNonResidentAttribute* pAttribute = &this) {
                ulong previousRunLCN = 0;
                byte* pDecodedByte = ((byte*)pAttribute) + pAttribute->RunArrayOffset;
                Helpers.Dump(pDecodedByte, 64);
                while (true) {
                    byte headerByte = *(pDecodedByte++);
                    if (0 == headerByte) { break; }
                    byte lengthBytesCount = (byte)(headerByte & 0x0F);
                    byte offsetLength = (byte)((headerByte & 0xF0) >> 4);
                    if (offsetLength > sizeof(ulong)) { throw new NotSupportedException(); }
                    ulong length = 0;
                    ulong thisRunLCN = 0;
                    // TODO : Inefficient decoding. Find something better.
                    for (int index = 0; index < lengthBytesCount; index++) {
                        length += (ulong)((*(pDecodedByte++)) << (8 * index));
                    }

                    int shifting = ((sizeof(ulong) - offsetLength)) * 8;
                    ulong rawValue = *((ulong*)pDecodedByte);
                    ulong captured = (*((ulong*)pDecodedByte)) << shifting;
                    long relativeOffset = ((long)captured >> shifting);
                    pDecodedByte += offsetLength;

                    // TODO : Inefficient addition. Find something better.
                    if (0 <= relativeOffset) {
                        thisRunLCN = previousRunLCN + (ulong)relativeOffset;
                    }
                    else {
                        thisRunLCN = previousRunLCN - (ulong)(-relativeOffset);
                    }

                    if (0 == thisRunLCN) {
                        // Sparse run.
                        throw new NotImplementedException();
                    }
                    Console.WriteLine("L={0} LCN={1:X8}", length, thisRunLCN);
                    previousRunLCN = thisRunLCN;
                }
            }
        }
    }
}
