﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC.NTFS
{
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
                        Console.Write("\t");
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