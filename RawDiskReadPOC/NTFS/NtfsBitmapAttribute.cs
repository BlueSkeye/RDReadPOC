﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>Provides additional methods specific to the bitmap attribute.</summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct NtfsBitmapAttribute
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

        /// <summary>Open the data stream for this bitmap attribute and scan each bit
        /// yielding either true or false if bit is set or cleared.</summary>
        /// <returns></returns>
        internal IEnumerable<ulong> EnumerateUsedItemIndex()
        {
            ulong indexValue = 0;
            using (Stream input = NonResidentHeader.OpenDataStream()) {
                int @byte;
                while (-1 != (@byte = input.ReadByte())) {
                    if (0 == @byte) { continue; }
                    for(int index = 0; index < 8; index++) {
                        if (0 != ((byte)@byte & (1 << index))) { yield return indexValue; }
                        indexValue++;
                    }
                }
            }
            yield break;
        }

        [FieldOffset(0)]
        internal NtfsNonResidentAttribute NonResidentHeader;
        [FieldOffset(0)]
        internal NtfsResidentAttribute ResidentHeader;
    }
}
