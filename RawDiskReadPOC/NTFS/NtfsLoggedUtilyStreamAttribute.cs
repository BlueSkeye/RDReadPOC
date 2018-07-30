using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>NOTE: Can be resident or non-resident.
    /// Operations on this attribute are logged to the journal ($LogFile) like normal metadata
    /// changes.
    /// Used by the Encrypting File System (EFS). All encrypted files have this attribute with
    /// the name $EFS.</summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct NtfsLoggedUtilyStreamAttribute
    {
        internal void Dump()
        {
            Stream input = null;

            try {
                if (0 == ResidentHeader.Header.Nonresident) {
                    ResidentHeader.AssertResident();
                    ResidentHeader.Dump();
                    input = ResidentHeader.OpenDataStream();
                }
                else {
                    NonResidentHeader.AssertNonResident();
                    NonResidentHeader.Dump();
                    input = NonResidentHeader.OpenDataStream();
                }
                int @byte;
                int bytesOnLine = 0;
                while (-1 != (@byte = input.ReadByte())) {
                    if (16 <= bytesOnLine++) {
                        Console.WriteLine();
                        bytesOnLine = 1;
                    }
                    if (1 == bytesOnLine) {
                        Console.Write(Helpers.Indent(1));
                    }
                    Console.Write("{0:X2} ", @byte);
                }
                Console.WriteLine();
            }
            finally { if (null != input) { input.Close(); } }
            return;
        }

        [FieldOffset(0)]
        internal NtfsNonResidentAttribute NonResidentHeader;
        [FieldOffset(0)]
        internal NtfsResidentAttribute ResidentHeader;
    }
}
