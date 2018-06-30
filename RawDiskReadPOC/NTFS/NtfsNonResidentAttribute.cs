using System;
using System.Collections.Generic;
using System.IO;

namespace RawDiskReadPOC.NTFS
{
    internal struct NtfsNonResidentAttribute
    {
        internal void AssertNonResident()
        {
            if (0 == Header.Nonresident) { throw new ApplicationException(); }
        }

        /// <summary>Returns a set of items each of which describes a range of adjacent logical clusters and
        /// the logical number of the first cluster in the range.</summary>
        /// <returns>A set of cluster renges.</returns>
        internal unsafe List<LogicalChunk> DecodeRunArray()
        {
            // TODO : Data runs may change over time when file is modified. How can we detect this
            // and reuse the already decoded array if we are sure the file is untouched since last
            // decoding ?
            List<LogicalChunk> chunks = new List<LogicalChunk>();
            fixed (NtfsNonResidentAttribute* pAttribute = &this) {
                ulong previousRunLCN = 0;
                byte* pDecoded = ((byte*)pAttribute) + pAttribute->RunArrayOffset;
                while (true) {
                    byte headerByte = *(pDecoded++);
                    if (0 == headerByte) { break; }
                    byte runLengthBytesCount = (byte)(headerByte & 0x0F);
                    byte runOffsetBytesCount = (byte)((headerByte & 0xF0) >> 4);
                    if (runOffsetBytesCount > sizeof(ulong)) { throw new NotSupportedException(); }
                    ulong thisRunLCN = 0;
                    ulong rawValue = *((ulong*)pDecoded);
                    ulong thisRunLength = rawValue & (ulong)((1 << (8 * runLengthBytesCount)) - 1);
                    int shifting = ((sizeof(ulong) - (runOffsetBytesCount + runLengthBytesCount))) * 8;
                    ulong captured = rawValue << shifting;
                    long relativeOffset = ((long)captured >> (shifting + (8 * runLengthBytesCount)));
                    pDecoded += runOffsetBytesCount + runLengthBytesCount;

                    if (long.MaxValue < previousRunLCN) {
                        throw new NotSupportedException();
                    }
                    long candidateRunLCN = (long)previousRunLCN + relativeOffset;
                    if (0 > candidateRunLCN) {
                        throw new ApplicationException();
                    }
                    thisRunLCN = (ulong)candidateRunLCN;

                    if (0 == thisRunLCN) {
                        // Sparse run.
                        throw new NotImplementedException();
                    }
                    chunks.Add(new LogicalChunk() {
                        ClustersCount = thisRunLength,
                        FirstLogicalClusterNumber = thisRunLCN
                    });
                    previousRunLCN = thisRunLCN;
                }
                if (FeaturesContext.InvariantChecksEnabled) {
                    if (this.HighVcn < this.LowVcn) {
                        throw new ApplicationException();
                    }
                }
                ulong totalVCNs = this.HighVcn - this.LowVcn + 1;
                ulong vcnSumInChunks = 0;
                foreach (LogicalChunk chunk in chunks) {
                    vcnSumInChunks += chunk.ClustersCount;
                }
                if (vcnSumInChunks != totalVCNs) {
                    throw new ApplicationException();
                }
                return chunks;
            }
        }

        internal void Dump()
        {
            Header.Dump();
            Console.WriteLine("\tVCN 0x{0:X8}-0x{1:X8}, ROff {2}, CU {3}",
                LowVcn, HighVcn, RunArrayOffset, CompressionUnit);
            Console.WriteLine("\tAsize 0x{0:X8}, Dsize 0x{1:X8}",
                AllocatedSize, DataSize);
            Console.WriteLine("\tIsize 0x{0:X8}, Csize 0x{1:X8}",
                InitializedSize, CompressedSize);
            return;
        }

        /// <summary>Open a data stream on the data part of this attribute.</summary>
        /// <param name="chunks">Optional parameter.</param>
        /// <returns></returns>
        internal Stream OpenDataStream(List<LogicalChunk> chunks = null)
        {
            if (0 != this.CompressionUnit) { throw new NotSupportedException(); }
            if (null == chunks) { chunks = DecodeRunArray(); }
            return new NonResidentDataStream(chunks);
        }

        /// <summary>An ATTRIBUTE structure containing members common to resident and
        /// nonresident attributes.</summary>
        internal NtfsAttribute Header;
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
            internal NonResidentDataStream(List<LogicalChunk> chunks)
            {
                _partition = NtfsPartition.Current ?? throw new InvalidOperationException();
                _chunks = chunks ?? throw new InvalidOperationException();
                _chunkEnumerator = _chunks.GetEnumerator();
                // Optimization.
                _clusterSize = _partition.ClusterSize;
                MAX_READ_SECTORS = (int)(_partition.SectorsPerCluster * 8);
                BUFFER_SIZE = (int)(MAX_READ_SECTORS * _partition.BytesPerSector);
                // Compute length.
                _length = 0;
                foreach(LogicalChunk scannedChunk in chunks) {
                    _length += (long)(scannedChunk.ClustersCount * _clusterSize);
                }
            }

            public override bool CanRead
            {
                get { return true; }
            }

            public override bool CanSeek
            {
                get { return true; }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override long Length => _length;

            public override long Position
            {
                get { return _position; }
                set => throw new NotSupportedException();
            }

            protected unsafe override void Dispose(bool disposing)
            {
                if (null != _clusterData) { _clusterData.Dispose(); }
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
                    // How many bytes are still expected according to Read request ?
                    ulong remainingExpectedBytes = (ulong)count;
                    while (0 < remainingExpectedBytes) {
                        if ((null == _clusterData) || (_clusterDataPosition >= _clusterData.DataSize)) {
                            // No more available data in local buffer. Must trigger another read from
                            // underlying partition.
                            ulong readFromCluster;
                            uint readSectorsCount;
                            ulong remainingSectorsInChunk;

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
                                readFromCluster = _currentChunk.FirstLogicalClusterNumber;
                            }
                            ulong remainingClustersInChunk = _currentChunk.ClustersCount - _currentChunkClusterIndex;
                            remainingSectorsInChunk = remainingClustersInChunk * sectorsPerCluster;

                            // How many blocks should we read ?
                            readSectorsCount = (uint)MAX_READ_SECTORS;
                            if (readSectorsCount > remainingSectorsInChunk) {
                                readSectorsCount = (uint)remainingSectorsInChunk;
                            }
                            if (FeaturesContext.InvariantChecksEnabled) {
                                if (0 != (readSectorsCount % sectorsPerCluster)) {
                                    throw new ApplicationException();
                                }
                            }
                            // Perform read and reinitialize some internal values.
                            ulong readFromSector = readFromCluster * sectorsPerCluster;
                            _clusterData = _partition.ReadSectors(readFromSector, readSectorsCount);
                            if (null == _clusterData) {
                                throw new ApplicationException();
                            }
                            _clusterDataPosition = 0;
                            _currentChunkClusterIndex += (readSectorsCount / sectorsPerCluster);
                        }
                        ulong readCount = remainingExpectedBytes;
                        if (_currentChunkRemainingBytesCount < remainingExpectedBytes) {
                            readCount = _currentChunkRemainingBytesCount;
                        }

                        // Copy data from local buffer to target one.
                        if (int.MaxValue < readCount) { throw new ApplicationException(); }
                        Helpers.Memcpy(_clusterData.Data + _clusterDataPosition, pBuffer + offset,
                            (int)readCount);
                        // Adjust values for next round.
                        _currentChunkRemainingBytesCount -= _clusterData.DataSize;
                        ulong effectiveRead = (remainingExpectedBytes < _clusterData.DataSize)
                            ? remainingExpectedBytes
                            : _clusterData.DataSize;
                        if (FeaturesContext.InvariantChecksEnabled) {
                            if (int.MaxValue < effectiveRead) { throw new ApplicationException(); }
                        }
                        _clusterDataPosition += (int)effectiveRead;
                        remainingExpectedBytes -= effectiveRead;
                        result += (int)effectiveRead;
                    }
                }
                return result;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin) {
                    case SeekOrigin.Begin:
                        if (0 > offset) {
                            throw new ArgumentException("invalid offset.");
                        }
                        break;
                    case SeekOrigin.Current:
                    case SeekOrigin.End:
                        throw new NotSupportedException();
                    default:
                        throw new ArgumentException();
                }
                // Find target chunk.
                ulong distanceToTarget = (ulong)offset;
                foreach (LogicalChunk scannedChunk in _chunks) {
                    ulong chunkSize = scannedChunk.ClustersCount * _clusterSize;
                    if (chunkSize < distanceToTarget) {
                        distanceToTarget -= chunkSize;
                        continue;
                    }
                    _currentChunk = scannedChunk;
                    _currentChunkClusterIndex = (distanceToTarget / _clusterSize);
                    _clusterDataPosition = (int)(distanceToTarget % _clusterSize);
                    _position = offset;
                    return offset;
                }
                _position = long.MaxValue;
                return long.MaxValue;
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
            private readonly int MAX_READ_SECTORS;
            /// <summary>An enumerator for logical chunks this stream is build upon.</summary>
            private IEnumerator<LogicalChunk> _chunkEnumerator;
            /// <summary></summary>
            private List<LogicalChunk> _chunks;
            private IPartitionClusterData _clusterData;
            /// <summary>Index in <see cref="_clusterData"/> buffer for the next not yet read byte. 
            /// This value accuracy is only guaranteed upon Read function entrance. It is not accurate
            /// inside the method itself until method exit.</summary>
            private int _clusterDataPosition;
            /// <summary>Cluster size captured at stream creation time for optimization purpose.</summary>
            private ulong _clusterSize;
            /// <summary>Current chunk we are reading from.</summary>
            private LogicalChunk _currentChunk;
            /// <summary>Index [0..ClustersCount[ of the first cluster that has not yet been copied
            /// into the local buffer. We never partially read a cluster from the underlying partition,
            /// hence no additional offset is required.</summary>
            private ulong _currentChunkClusterIndex;
            private ulong _currentChunkRemainingBytesCount;
            private long _length;
            ///// <summary>A local buffer used for data capture from the underlying partition. The
            ///// local buffer is created on first read and remains alive until stream disposal.</summary>
            //private unsafe byte* _localBuffer;
            ///// <summary>The local buffer may contain less bytes from the underlying partition than
            ///// its actual size. This member tracks how many bytes are really in the buffer.</summary>
            //private uint _localBufferBytesCount;
            /// <summary>The partition this stream belongs to?</summary>
            private NtfsPartition _partition;
            /// <summary>Current position in this stream.</summary>
            private long _position = 0;
        }
    }
}
