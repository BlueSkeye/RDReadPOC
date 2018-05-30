using System;

namespace RawDiskReadPOC
{
    internal static class Helpers
    {
        internal static unsafe void Dump(byte* buffer, uint size)
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
    }
}
