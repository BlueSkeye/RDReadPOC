using System;
using System.Text;

namespace RawDiskReadPOC
{
    internal static class Helpers
    {
        internal static unsafe void BinaryDump(byte* buffer, uint size)
        {
            DumpedAddress = buffer;
            BinaryDump(size);
        }

        internal static unsafe void BinaryDump(uint size)
        {
            for(uint index = 0; index < size; index++) {
                if (0 == (index % 16)) {
                    if (0 != index) {
                        Console.WriteLine();
                    }
                    Console.Write("{0:X3} ({0:D3}) : ", index);
                }
                Console.Write("{0:X2} ", DumpedAddress[index]);
            }
            Console.WriteLine();
        }

        internal static unsafe void Memcpy(byte* from, byte* to, int length)
        {
            while (sizeof(ulong) <= length) {
                *((ulong*)to) = *((ulong*)from);
                to += sizeof(ulong);
                from += sizeof(ulong);
                length -= sizeof(ulong);
            }
            while (0 < length--) { *to++ = *from++; }
            return;
        }

        internal static unsafe string uint32ToUnicodeString(uint value)
        {
            uint inverted = ((value & 0xFFFF) << 16) | (value >> 16);
            byte* buffer = (byte*)&inverted;
            return Encoding.Unicode.GetString(buffer, sizeof(uint));
        }

        internal static unsafe byte* DumpedAddress;
    }
}
