using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>Provides additional methods specific to the bitmap attribute.</summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct NtfsBitmapAttribute
    {
        internal bool IsResident
        {
            get { return (0 == ResidentHeader.Header.Nonresident); }
        }

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

        internal IEnumerator<bool> GetContentEnumerator()
        {
            return new ContentEnumerator(this);
        }

        [FieldOffset(0)]
        internal NtfsNonResidentAttribute NonResidentHeader;
        [FieldOffset(0)]
        internal NtfsResidentAttribute ResidentHeader;

        private class ContentEnumerator : IEnumerator<bool>
        {
            internal ContentEnumerator(NtfsBitmapAttribute bitmap)
            {
                if (bitmap.IsResident) {
                    throw new NotImplementedException();
                }
                _dataStream = bitmap.NonResidentHeader.OpenDataStream();
                _buffer = new byte[NtfsPartition.Current.ClusterSize];
                _state = -1;
            }

            public bool Current
            {
                get
                {
                    if (0 != _state) {
                        throw new InvalidOperationException();
                    }
                    return (0 != (_buffer[_bufferPosition] & (byte)(1 << _scanMask)));
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                if (null != _dataStream) {
                    _dataStream.Close();
                }
                return;
            }

            public bool MoveNext()
            {
                switch (_state) {
                    case -1:
                        _scanMask = 7;
                        _bufferAvailableCount = 0;
                        _bufferPosition = 0;
                        _state = 0;
                        goto case 0;
                    case 0:
                        if (7 > _scanMask) {
                            _scanMask++;
                            return true;
                        }
                        if (++_bufferPosition < _bufferAvailableCount) {
                            _scanMask = 0;
                            return true;
                        }
                        _bufferAvailableCount = _dataStream.Read(_buffer, 0, _buffer.Length);
                        if (0 >= _bufferAvailableCount) {
                            _state = 1;
                            return false;
                        }
                        _bufferPosition = 0;
                        _scanMask = 0;
                        return true;
                    case 1:
                        return false;
                    default:
                        throw new ApplicationException();
                }
            }

            public void Reset()
            {
                _state = -1;
            }

            private byte[] _buffer;
            private int _bufferAvailableCount;
            private int _bufferPosition;
            private Stream _dataStream;
            private int _scanMask;
            /// <summary>-1 => before first, 0 => in the middle, 1 => eos reached</summary>
            private int _state;
        }
    }
}
