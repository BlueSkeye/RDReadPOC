using System;

namespace RawDiskReadPOC
{
    internal static class Helpers
    {
        internal static unsafe void BinaryDump(byte* buffer, uint size)
        {
            for(uint index = 0; index < size; index++) {
                if (0 == (index % 16)) {
                    if (0 != index) {
                        Console.WriteLine();
                    }
                    Console.Write("{0:X3} ({0:D3}) : ", index);
                }
                Console.Write("{0:X2} ", buffer[index]);
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
    }
}
