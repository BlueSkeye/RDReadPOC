using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Size is 0x18/24 bytes = 16 + 8</remarks>
    internal struct NtfsResidentAttribute
    {
        internal void AssertResident()
        {
            if (0 != Attribute.Nonresident) {
                throw new AssertionException("Non resident attribute found which was expected to be resident.");
            }
        }

        internal void Dump()
        {
            Attribute.Dump();
            Console.WriteLine("VL {0}, VO 0x{1:X4}, Flg {2}",
                ValueLength, ValueOffset, Flags);
        }
        
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
