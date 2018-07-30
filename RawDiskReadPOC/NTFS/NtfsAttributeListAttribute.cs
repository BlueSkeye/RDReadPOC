using System;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Can be either resident or non-resident.
    /// Value consists of a sequence of variable length, 8-byte aligned, ATTR_LIST_ENTRY
    /// records.
    /// The list is not terminated by anything at all! The only way to know when the end is
    /// reached is to keep track of the current offset and compare it to the attribute value
    /// size.
    /// The attribute list attribute contains one entry for each attribute of the file in
    /// which the list is located, except for the list attribute itself. The list is sorted:
    /// first by attribute type, second by attribute name (if present), third by instance
    /// number. The extents of one non-resident attribute (if present) immediately follow
    /// after the initial extent. They are ordered by lowest_vcn and have their instace set
    /// to zero. It is not allowed to have two attributes with all sorting keys equal.
    /// Further restrictions:
    /// If not resident, the vcn to lcn mapping array has to fit inside the base mft record.
    /// The attribute list attribute value has a maximum size of 256kb. This is imposed by
    /// the Windows cache manager.
    /// Attribute lists are only used when the attributes of mft record do not fit inside
    /// the mft record despite all attributes (that can be made non-resident) having been
    /// made non-resident. This can happen e.g.when:
    /// - File has a large number of hard links (lots of filename attributes present).
    /// - The mapping pairs array of some non-resident attribute becomes so large due to
    ///   fragmentation that it overflows the mft record.
    /// - The security descriptor is very complex(not applicable to NTFS 3.0 volumes).
    /// - There are many named streams.</remarks>
    internal struct NtfsAttributeListAttribute
    {
        /// <summary>Returns attribute name or a null reference if the name is undefined.</summary>
        internal unsafe string Name
        {
            get
            {
                if (0 == NameLength) { return null; }
                fixed (NtfsAttributeListAttribute* ptr = &this) {
                    return Encoding.Unicode.GetString((byte*)ptr + NameOffset, sizeof(char) * NameLength);
                }
            }
        }

        internal unsafe void BinaryDump()
        {
            fixed(NtfsAttributeListAttribute* ptr = &this) {
                Helpers.BinaryDump((byte*)ptr, this.EntryLength);
            }
        }

        internal void Dump()
        {
            Console.WriteLine("T:{0}, L:{1}, VCN:0x{2:X8}, FRN:0x{3:X8}, #{4} ({5})",
                AttributeType, EntryLength, LowVcn, FileReferenceNumber,
                AttributeNumber, Name);
        }

        /// <summary>Type of referenced attribute.</summary>
        internal NtfsAttributeType AttributeType;
        /// <summary>Byte size of this entry (8-byte aligned).</summary>
        internal ushort EntryLength;
        /// <summary>Size in Unicode chars of the name of the attribute or 0 if unnamed.</summary>
        internal byte NameLength;
        /// <summary>Byte offset to beginning of attribute name (always set this to where
        /// the name would start even if unnamed).</summary>
        internal byte NameOffset;
        /// <summary>Lowest virtual cluster number of this portion of the attribute value.
        /// This is usually 0. It is non-zero for the case where one attribute does not fit
        /// into one mft record and thus several mft records are allocated to hold this
        /// attribute.In the latter case, each mft record holds one extent of the attribute
        /// and there is one attribute list entry for each extent.
        /// NOTE: This is DEFINITELY a signed value! The windows driver uses cmp, followed
        /// by jg when comparing this, thus it treats it as signed.</summary>
        internal ulong LowVcn;
        /// <summary>The reference of the mft record holding the <see cref="NtfsAttribute"/>
        /// for this portion of the attribute value.</summary>
        internal ulong FileReferenceNumber;
        /// <summary>If lowest_vcn = 0, the instance of the attribute being referenced;
        /// otherwise 0.</summary>
        internal ushort AttributeNumber;
        // The name if any starts here. 
    }
}
