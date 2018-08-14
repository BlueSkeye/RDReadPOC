using System;
using System.Text;

using RawDiskReadPOC.NTFS.Indexing;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Size is 0x10/16 bytes.</remarks>
    internal struct NtfsAttribute
    {
        internal bool IsLast
        {
            get { return (NtfsAttributeType.EndOfListMarker == AttributeType); }
        }

        internal bool IsResident
        {
            get { return (0 == Nonresident); }
        }

        /// <summary>Returns attribute name or a null reference if the name is undefined.</summary>
        internal unsafe string Name
        {
            get
            {
                if (0 == NameLength) { return null; }
                fixed (NtfsAttribute* ptr = &this) {
                    return Encoding.Unicode.GetString((byte*)ptr + NameOffset, sizeof(char) * NameLength);
                }
            }
        }

        internal unsafe void BinaryDump()
        {
            fixed (void* dumped = &this) {
                Helpers.BinaryDump((byte*)dumped, Length);
            }
        }

        internal unsafe void Dump(bool redirectToTypeDumper = false)
        {
            if (!redirectToTypeDumper) {
                _Dump();
                return;
            }
            fixed (NtfsAttribute* rawAttribute = &this) {
                void* rawValue = null;
                if (0 != rawAttribute->Nonresident) {
                    ((NtfsResidentAttribute*)rawAttribute)->Dump();
                    rawValue = rawAttribute->GetValue();
                }
                switch (AttributeType) {
                    case NtfsAttributeType.AttributeBitmap:
                        ((NtfsBitmapAttribute*)rawAttribute)->Dump();
                        return;
                    case NtfsAttributeType.AttributeFileName:
                        ((NtfsFileNameAttribute*)rawValue)->Dump();
                        return;
                    case NtfsAttributeType.AttributeIndexAllocation:
                        ((NtfsIndexAllocationAttribute*)rawAttribute)->Dump();
                        return;
                    case NtfsAttributeType.AttributeIndexRoot:
                        ((NtfsRootIndexAttribute*)rawAttribute)->Dump();
                        return;
                    case NtfsAttributeType.AttributeLoggedUtilityStream:
                        ((NtfsLoggedUtilyStreamAttribute*)rawAttribute)->Dump();
                        return;
                    case NtfsAttributeType.AttributeSecurityDescriptor:
                        ((NtfsSecurityDescriptorAttribute*)rawValue)->Dump();
                        return;
                    case NtfsAttributeType.AttributeStandardInformation:
                        ((NtfsStandardInformationAttribute*)rawAttribute)->Dump();
                        return;

                    case NtfsAttributeType.AttributeAttributeList:
                    case NtfsAttributeType.AttributeObjectId:
                    case NtfsAttributeType.AttributeVolumeName:
                    case NtfsAttributeType.AttributeVolumeInformation:
                    case NtfsAttributeType.AttributeData:
                    case NtfsAttributeType.AttributeReparsePoint:
                    case NtfsAttributeType.AttributeEAInformation:
                    case NtfsAttributeType.AttributeEA:
                    case NtfsAttributeType.AttributePropertySet:
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        private void _Dump()
        {
            Console.WriteLine("T:{0}, L={1}, {2}, Flg 0x{3:X4}, Att# {4} ({5})",
                AttributeType, Length, (0 == Nonresident) ? "Re" : "NR", Flags,
                AttributeNumber, Name ?? "UNNAMED");
        }

        /// <summary>Get the resident part size of this attribute.</summary>
        /// <returns></returns>
        internal unsafe uint GetResidentSize()
        {
            return Length;
        }

        internal unsafe void* GetValue()
        {
            if (0 != Nonresident) {
                throw new NotImplementedException();
            }
            fixed(NtfsAttribute* pThis = &this) {
                return ((byte*)pThis) + ((NtfsResidentAttribute*)pThis)->ValueOffset;
            }
        }

        /// <summary>The (32-bit) type of the attribute.</summary>
        internal NtfsAttributeType AttributeType;
        /// <summary>Byte size of the resident part of the attribute(aligned to 8-byte boundary). Used
        /// to get to the next attribute.</summary>
        internal uint Length;
        /// <summary>If 0, attribute is resident. If 1, attribute is non-resident.</summary>
        internal byte Nonresident;
        /// <summary>Unicode character size of name of attribute. 0 if unnamed.</summary>
        internal byte NameLength;
        /// <summary>If name_length != 0, the byte offset to the beginning of the name from the
        /// attribute record.Note that the name is stored as a Unicode string. When creating, place
        /// offset just at the end of the record header. Then, follow with attribute value or mapping
        /// pairs array, resident and non-resident attributes respectively, aligning to an 8-byte
        /// boundary.</summary>
        internal ushort NameOffset;
        /// <summary>Flags describing the attribute.</summary>
        internal ushort Flags;
        /// <summary>The instance of this attribute record. This number is unique within this mft
        /// record (see MFT_RECORD/next_attribute_instance notes in in mft.h for more details).</summary>
        internal ushort AttributeNumber;
    }
}
