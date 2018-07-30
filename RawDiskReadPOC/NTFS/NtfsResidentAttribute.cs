using System;
using System.IO;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>Size is 0x18/24 bytes = 16 + 8</remarks>
    internal struct NtfsResidentAttribute
    {
        internal bool IsResident
        {
            get { return Header.IsResident; }
        }

        internal void AssertResident()
        {
            if (!IsResident) {
                throw new AssertionException("Non resident attribute found which was expected to be resident.");
            }
        }

        internal void Dump()
        {
            Header.Dump(false);
            Console.WriteLine("VL {0}, VO 0x{1:X4}, Flg {2}",
                ValueLength, ValueOffset, Flags);
        }

        /// <summary>WARNING, the caller must also fix the structure before invoking this function and
        /// keep it fixed all along while using the returned value.</summary>
        /// <returns></returns>
        internal unsafe void* GetValue()
        {
            fixed(NtfsResidentAttribute* pThis = &this) {
                return (byte*)pThis + this.ValueOffset;
            }
        }

        /// <summary>Open a data stream on the data part of this attribute.</summary>
        /// <param name="chunks">Optional parameter.</param>
        /// <returns></returns>
        internal unsafe Stream OpenDataStream()
        {
            fixed(NtfsResidentAttribute* rawAttribute = &this) {
                byte* rawValue = (byte*)rawAttribute + ValueOffset;
                return new ResidentDataStream(rawValue, ValueLength);
            }
        }

        /// <summary>An <see cref="NtfsAttribute"/> structure containing members common to resident
        /// and nonresident attributes.</summary>
        internal NtfsAttribute Header;
        /// <summary>Byte size of attribute value.</summary>
        internal uint ValueLength;
        /// <summary>Byte offset of the attribute value from the start of the attribute record.When
        /// creating, align to 8-byte boundary if we have a name present as this might not have a
        /// length of a multiple of 8-bytes.</summary>
        internal ushort ValueOffset;
        /// <summary>A bit array of flags specifying properties of the attribute. The values
        /// defined include: Indexed 0x0001</summary>
        internal ushort Flags;
        /// <summary>Reserved/alignment to 8-byte boundary.</summary>
        internal sbyte _filler;

        internal enum ResidentAttributeFlags : ushort
        {
            None = 0,
            /// <summary>Attribute is referenced in an index (has implications for deleting and
            /// modifying the attribute).</summary>
            IsIndexed = 0x01,
        }

        private class ResidentDataStream : Stream
        {
            internal unsafe ResidentDataStream(byte* rawValue, uint length)
            {
                _rawValue = rawValue;
                _length = length;
            }

            public override long Length => _length;

            public override bool CanRead
            {
                get { return true; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override long Position
            {
                get { return _position; }
                set => throw new NotSupportedException();
            }

            protected unsafe override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override unsafe int Read(byte[] buffer, int offset, int count)
            {
                // Arguments validation.
                if (null == buffer) { throw new ArgumentNullException(); }
                if (0 > offset) { throw new ArgumentOutOfRangeException(); }
                if (0 > count) { throw new ArgumentOutOfRangeException(); }
                if ((buffer.Length - offset) < count) { throw new ArgumentException(); }
                uint remaining = (uint)(_length - _position);
                uint readCount = (count < remaining) ? (uint)count : remaining;
                if (int.MaxValue < readCount) { throw new ApplicationException(); }
                fixed (byte* pBuffer = buffer) {
                    Helpers.Memcpy(_rawValue + _position, pBuffer + offset, (int)readCount);
                    _position += readCount;
                    return (int)readCount;
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new InvalidOperationException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            private uint _length;
            /// <summary>Current position in this stream.</summary>
            private long _position = 0;
            private unsafe byte* _rawValue;
        }
    }
}
