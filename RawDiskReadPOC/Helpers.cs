﻿using System;
using System.Text;

namespace RawDiskReadPOC
{
    internal static class Helpers
    {
        static Helpers()
        {
            _Indentations = new string[10];
            for(int index = 0; index < _Indentations.Length; index++) {
                _Indentations[index] = new string(' ', index * 2);
            }
        }

        internal static unsafe void BinaryDump(byte* buffer, uint size)
        {
            DumpedAddress = buffer;
            BinaryDump(size);
        }

        internal static unsafe void BinaryDump(byte[] buffer, uint size)
        {
            BinaryDump(buffer, 0, size);
        }

        internal static unsafe void BinaryDump(byte[] buffer, uint offset, uint size)
        {
            if ((offset + size) > buffer.Length) {
                throw new ArgumentException();
            }
            fixed(byte* rawData = buffer) {
                DumpedAddress = rawData + offset;
                BinaryDump(size);
            } 
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

        internal static string DecodeTime(ulong value)
        {
            if (value > long.MaxValue) { throw new ArgumentOutOfRangeException(); }
            return (Epoch + new TimeSpan((long)value)).ToString("yyyy-MM-dd HH:mm:ss");
        }

        internal static uint GetChainItemsCount(this IPartitionClusterData data)
        {
            uint result = 0;
            for (IPartitionClusterData scannedData = data; null != scannedData; scannedData = scannedData.NextInChain) {
                result++;
            }
            return result;
        }

        internal static uint GetChainLength(this IPartitionClusterData data)
        {
            uint result = 0;
            for(IPartitionClusterData scannedData = data; null != scannedData; scannedData = scannedData.NextInChain) {
                result += scannedData.DataSize;
            }
            return result;
        }

        internal static string Indent(int count)
        {
            if (0 > count) {
                throw new ArgumentOutOfRangeException("count");
            }
            if (count >= _Indentations.Length) {
                throw new ArgumentOutOfRangeException("count");
            }
            return _Indentations[count];
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

        internal static unsafe void Zeroize(byte* data, uint size)
        {
            ulong* target = (ulong*)data;
            for (; size >= sizeof(ulong); size -= sizeof(ulong)) {
                (*(target++)) = 0;
            }
            while (0 < size--) {
                *(target++) = 0;
            }
        }

        internal static unsafe byte* DumpedAddress;
        private static readonly DateTime Epoch = new DateTime(1601, 1, 1);
        private static string[] _Indentations;
    }
}
