using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC.NTFS
{
    internal struct NtfsNonResidentAttribute
    {
        /// <summary>An ATTRIBUTE structure containing members common to resident and
        /// nonresident attributes.</summary>
        internal NtfsAttribute Attribute;
        /// <summary>The lowest valid Virtual Cluster Number (VCN) of this portion of the
        /// attribute value. Unless the attribute value is very fragmentedc(to the extent
        /// that an attribute list is needed to describe it), there is only one portion of
        /// the attribute value, and the value of LowVcn is zero.</summary>
        internal ulong LowVcn;
        /// <summary>The highest valid VCN of this portion of the attribute value.</summary>
        internal ulong HighVcn;
        /// <summary>The offset, in bytes, from the start of the structure to the run array that
        /// contains the mappings between VCNs and Logical Cluster Numbers(LCNs).</summary>
        internal ushort RunArrayOffset;
        /// <summary>The compression unit for the attribute expressed as the logarithm to the
        /// base two of the number of clusters in a compression unit. If CompressionUnit is zero,
        /// the attribute is not compressed.</summary>
        internal byte CompressionUnit;
        internal byte Alignment1;
        internal uint Alignment2;
        /// <summary>The size, in bytes, of disk space allocated to hold the attribute value</summary>
        internal ulong AllocatedSize;
        /// <summary>The size, in bytes, of the attribute value.This may be larger than the AllocatedSize
        /// if the attribute value is compressed or sparse.</summary>
        internal ulong DataSize;
        /// <summary>The size, in bytes, of the initialized portion of the attribute value.</summary>
        internal ulong InitializedSize;
        /// <summary>The size, in bytes, of the attribute value after compression. This member is only
        /// present when the attribute is compressed.</summary>
        internal ulong CompressedSize;

        /// <summary>Returns an ordered set of items each of which describes a range of adjacent logical
        /// clusters and the logical number of the first cluster in the range.</summary>
        /// <returns></returns>
        internal unsafe List<LogicalChunk> DecodeRunArray()
        {
            // TODO : Data runs may change over time when file is modified. How can we detect this
            // and reuse the already decoded array if we are sure the file is untouched since last
            // decoding ?
            List<LogicalChunk> chunks = new List<LogicalChunk>();
            fixed (NtfsNonResidentAttribute* pAttribute = &this) {
                ulong previousRunLCN = 0;
                byte* pDecodedByte = ((byte*)pAttribute) + pAttribute->RunArrayOffset;
                while (true) {
                    byte headerByte = *(pDecodedByte++);
                    if (0 == headerByte) { break; }
                    byte lengthBytesCount = (byte)(headerByte & 0x0F);
                    byte offsetLength = (byte)((headerByte & 0xF0) >> 4);
                    if (offsetLength > sizeof(ulong)) { throw new NotSupportedException(); }
                    ulong length = 0;
                    ulong thisRunLCN = 0;
                    // TODO : Inefficient decoding. Find something better.
                    for (int index = 0; index < lengthBytesCount; index++) {
                        length += (ulong)((*(pDecodedByte++)) << (8 * index));
                    }

                    int shifting = ((sizeof(ulong) - offsetLength)) * 8;
                    ulong rawValue = *((ulong*)pDecodedByte);
                    ulong captured = (*((ulong*)pDecodedByte)) << shifting;
                    long relativeOffset = ((long)captured >> shifting);
                    pDecodedByte += offsetLength;

                    // TODO : Inefficient addition. Find something better.
                    if (0 <= relativeOffset) {
                        thisRunLCN = previousRunLCN + (ulong)relativeOffset;
                    }
                    else {
                        thisRunLCN = previousRunLCN - (ulong)(-relativeOffset);
                    }

                    if (0 == thisRunLCN) {
                        // Sparse run.
                        throw new NotImplementedException();
                    }
                    chunks.Add(new LogicalChunk() {
                        ClustersCount = length,
                        FirstLogicalClusterNumber = thisRunLCN
                    });
                    previousRunLCN = thisRunLCN;
                }
                return chunks;
            }
        }

        /// <summary>Open a data stream on the data part of this attribute.</summary>
        /// <param name="partition">The partition this attribute is located in. This parameter is
        /// used for reading the underlying media.</param>
        /// <param name="chunks">Optional parameter.</param>
        /// <returns></returns>
        internal Stream OpenDataStream(NtfsPartition partition, List<LogicalChunk> chunks = null)
        {
            if (null == partition) { throw new ArgumentNullException(); }
            if (0 != this.CompressionUnit) { throw new NotSupportedException(); }
            if (null == chunks) { chunks = DecodeRunArray(); }
            return new NonResidentDataStream(partition, chunks);
        }

        internal class LogicalChunk
        {
            internal ulong ClustersCount;
            internal ulong FirstLogicalClusterNumber;

            public override string ToString()
            {
                return string.Format("L={0} LCN={1:X8}", ClustersCount, FirstLogicalClusterNumber);

            }
        }

        private class NonResidentDataStream : Stream
        {
            internal NonResidentDataStream(NtfsPartition partition, List<LogicalChunk> chunks)
            {
                _partition = partition ?? throw new InvalidOperationException();
                _chunks = chunks ?? throw new InvalidOperationException();
                _chunkEnumerator = _chunks.GetEnumerator();
                // Optimization.
                _clusterSize = partition.ClusterSize;
                MAX_READ_BLOCKS = (int)(partition.SectorsPerCluster * 8);
                BUFFER_SIZE = (int)(MAX_READ_BLOCKS * _partition.BytesPerSector);
            }

            public override long Length => throw new NotImplementedException();

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
                get
                {
                    return _position;
                }
                set => throw new NotSupportedException();
            }

            protected unsafe override void Dispose(bool disposing)
            {
                if (null != _localBuffer) { Marshal.FreeCoTaskMem((IntPtr)_localBuffer); }
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
                int result = 0;
                uint sectorsPerCluster = _partition.SectorsPerCluster;
                fixed (byte* pBuffer = buffer) {
                    // Initialization
                    if (null == _localBuffer) {
                        // Allocation on first invocation of this method. Will be freed on disposal.
                        _localBuffer = (byte*)Marshal.AllocCoTaskMem(BUFFER_SIZE);
                    }
                    // How many bytes are still expected according to Read request ?
                    ulong remainingExpectedBytes = (ulong)count;
                    while (0 < remainingExpectedBytes) {
                        if (_localBufferPosition >= _localBufferBytesCount) {
                            // No more available data in local buffer. Must trigger another read from
                            // underlying partition.
                            ulong readFromCluster;
                            uint readBlocksCount;
                            ulong remainingBlocksInChunk;

                            if ((null != _currentChunk) && (_currentChunkClusterIndex < _currentChunk.ClustersCount)) {
                                // Some clusters remaining in current chunk.
                                readFromCluster = _currentChunkClusterIndex + _currentChunk.FirstLogicalClusterNumber;
                            }
                            else {
                                // Need to go on with next chunk.
                                if (!_chunkEnumerator.MoveNext()) {
                                    // No more data available from the partition.
                                    return result;
                                }
                                _currentChunk = _chunkEnumerator.Current;
                                _currentChunkClusterIndex = 0;
                                _currentChunkRemainingBytesCount = _clusterSize * _currentChunk.ClustersCount;
                                _partition.SeekTo(_currentChunk.FirstLogicalClusterNumber * sectorsPerCluster);
                                readFromCluster = _currentChunk.FirstLogicalClusterNumber;
                            }
                            ulong remainingClustersInChunk = _currentChunk.ClustersCount - _currentChunkClusterIndex;
                            remainingBlocksInChunk = remainingClustersInChunk * sectorsPerCluster;

                            // How many blocks should we read ?
                            readBlocksCount = (uint)MAX_READ_BLOCKS;
                            if (readBlocksCount > remainingBlocksInChunk) {
                                readBlocksCount = (uint)remainingBlocksInChunk;
                            }
                            // Invariant control.
                            if (0 != (readBlocksCount % sectorsPerCluster)) {
                                throw new ApplicationException();
                            }
                            // Perform read and reinitialize some internal values.
                            ulong readFromBlock = readFromCluster * sectorsPerCluster;
                            _partition.ReadBlocks(readFromBlock, out _localBufferBytesCount,
                                readBlocksCount, _localBuffer);
                            _localBufferPosition = 0;
                            _currentChunkClusterIndex += (readBlocksCount / sectorsPerCluster);
                        }
                        ulong readCount = remainingExpectedBytes;
                        if (_currentChunkRemainingBytesCount < remainingExpectedBytes) {
                            readCount = _currentChunkRemainingBytesCount;
                        }

                        // Copy data from local buffer to target one.
                        if (int.MaxValue < readCount) { throw new ApplicationException(); }
                        ulong quickMoveCount = readCount / 8;
                        ulong* pQuickBufferTo = (ulong*)pBuffer;
                        ulong* pQuickBufferFrom = (ulong*)(_localBuffer + _localBufferPosition);
                        while (0 < quickMoveCount) {
                            *(pQuickBufferTo++) = *(pQuickBufferFrom++);
                            quickMoveCount--;
                        }
                        readCount -= (8 * quickMoveCount);
                        while (0 < readCount) {
                            *((byte*)pQuickBufferTo++) = *((byte*)pQuickBufferFrom++);
                            readCount--;
                        }
                        // Check invariant
                        if (0 != readCount) { throw new ApplicationException(); }

                        // Adjust values for next round.
                        _currentChunkRemainingBytesCount -= _localBufferBytesCount;
                        ulong effectiveRead = (remainingExpectedBytes < _localBufferBytesCount)
                            ? remainingExpectedBytes
                            : _localBufferBytesCount;
                        // Invariant check.
                        if (int.MaxValue < effectiveRead) { throw new ApplicationException(); }
                        _localBufferPosition += (int)effectiveRead;
                        remainingExpectedBytes -= effectiveRead;
                        result += (int)effectiveRead;
                    }
                }
                return result;
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

            private readonly int BUFFER_SIZE;
            private readonly int MAX_READ_BLOCKS;
            /// <summary>An enumerator for logical chunks this stream is build upon.</summary>
            private IEnumerator<LogicalChunk> _chunkEnumerator;
            /// <summary></summary>
            private List<LogicalChunk> _chunks;
            /// <summary>Cluster size captured at stream creation time for optimization purpose.</summary>
            private ulong _clusterSize;
            /// <summary>Current chunk we are reading from.</summary>
            private LogicalChunk _currentChunk;
            /// <summary>Index [0..ClustersCount[ of the first cluster that has not yet been copied
            /// into the local buffer. We never partially read a cluster from the underlying partition,
            /// hence no additional offset is required.</summary>
            private ulong _currentChunkClusterIndex;
            private ulong _currentChunkRemainingBytesCount;
            /// <summary>A local buffer used for data capture from the underlying partition. The
            /// local buffer is created on first read and remains alive until stream disposal.</summary>
            private unsafe byte* _localBuffer;
            /// <summary>The local buffer may contain less bytes from the underlying partition than
            /// its actual size. This member tracks how many bytes are really in the buffer.</summary>
            private uint _localBufferBytesCount;
            /// <summary>Index in local buffer for the next not yet read byte. This value accuracy
            /// is only guaranteed upon Read function entrance. It is not accurate inside the method
            /// itself until method exit.</summary>
            private int _localBufferPosition;
            /// <summary>The partition this stream belongs to?</summary>
            private NtfsPartition _partition;
            /// <summary>Current position in this stream.</summary>
            private long _position = 0;
        }
    }
}
