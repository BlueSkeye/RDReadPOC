using System;
using System.Text;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Size is 0x10/16 bytes.</remarks>
    internal struct NtfsAttribute
    {
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
