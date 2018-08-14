using System;
using System.Collections.Generic;
using System.IO;

namespace RawDiskReadPOC.NTFS
{
    internal struct NtfsNonResidentAttribute
    {
        internal bool IsResident
        {
            get { return Header.IsResident; }
        }

        /// <summary>Return number of bytes used for disk storage of this non resident attribute value.
        /// This knowledge is required for <see cref="NtfsRecord"/> fixup application.</summary>
        internal uint OnDiskSize
        {
            get { return (0 == CompressionUnit) ? (uint)InitializedSize : (uint)CompressedSize; }
        }

        internal void AssertNonResident()
        {
            if (IsResident) { throw new ApplicationException(); }
        }

        internal void AssertVCN(ulong candidate)
        {
            if (candidate < LowVcn) { throw new ApplicationException(); }
            if (candidate < HighVcn) { throw new ApplicationException(); }
        }

        /// <summary>Returns a set of items each of which describes a range of adjacent logical clusters and
        /// the logical number of the first cluster in the range.</summary>
        /// <returns>A set of cluster renges.</returns>
        internal unsafe List<LogicalChunk> DecodeRunArray()
        {
            // TODO : Data runs may change over time when file is modified. How can we detect this
            // and reuse the already decoded array if we are sure the file is untouched since last
            // decoding ?
            // Also note that there is a small chance the fidsk layout will change between the time
            // we read the chunk list and the time we use it in a data stream for example. This may
            // lead to discrepency or crashes.
            List<LogicalChunk> chunks = new List<LogicalChunk>();
            fixed (NtfsNonResidentAttribute* pAttribute = &this) {
                ulong previousNonSparseRunLCN = 0;
                byte* pDecoded = ((byte*)pAttribute) + pAttribute->RunArrayOffset;
                while (true) {
                    byte headerByte = *(pDecoded++);
                    if (0 == headerByte) { break; }
                    byte runLengthBytesCount = (byte)(headerByte & 0x0F);
                    byte runOffsetBytesCount = (byte)((headerByte & 0xF0) >> 4);
                    bool isSparse = false;
                    ulong thisRunLCN;
                    if (sizeof(ulong) < (runOffsetBytesCount + runLengthBytesCount )) {
                        throw new NotSupportedException();
                    }
                    ulong rawValue = *((ulong*)pDecoded);
                    ulong thisRunLength = rawValue & ((1UL << (8 * runLengthBytesCount)) - 1);
                    if (0 == runOffsetBytesCount) {
                        isSparse = true;
                        thisRunLCN = ulong.MaxValue;
                    }
                    else {
                        int shifting = ((sizeof(ulong) - (runOffsetBytesCount + runLengthBytesCount))) * 8;
                        ulong captured = rawValue << shifting;
                        long relativeOffset = ((long)captured >> (shifting + (8 * runLengthBytesCount)));
                        if (long.MaxValue < previousNonSparseRunLCN) {
                            throw new NotSupportedException();
                        }
                        thisRunLCN = (ulong)((long)previousNonSparseRunLCN + relativeOffset);
                        if (0 > thisRunLCN) {
                            throw new ApplicationException();
                        }
                    }
                    pDecoded += runOffsetBytesCount + runLengthBytesCount;
                    chunks.Add(new LogicalChunk(isSparse) {
                        ClustersCount = thisRunLength,
                        FirstLogicalClusterNumber = thisRunLCN
                    });
                    if (!isSparse) {
                        previousNonSparseRunLCN = thisRunLCN;
                    }
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
            Console.WriteLine(Helpers.Indent(1) + "VCN 0x{0:X8}-0x{1:X8}, ROff {2}, CU {3}",
                LowVcn, HighVcn, RunArrayOffset, CompressionUnit);
            Console.WriteLine(Helpers.Indent(1) + "Asize 0x{0:X8}, Dsize 0x{1:X8}",
                AllocatedSize, DataSize);
            Console.WriteLine(Helpers.Indent(1) + "Isize 0x{0:X8}, Csize 0x{1:X8}",
                InitializedSize, CompressedSize);
            return;
        }

        internal unsafe IPartitionClusterData GetData()
        {
            using (Stream input = OpenDataClusterStream()) {
                NonResidentDataStream dataStream = (NonResidentDataStream)input;
                long length = dataStream.Length;
                if (uint.MaxValue < length) {
                    throw new ApplicationException();
                }
                IPartitionClusterData result = NtfsPartition.Current.GetBuffer((uint)length);
                byte* rawResult = result.Data;
                IPartitionClusterData inputData;
                while (null != (inputData = ((IClusterStream)dataStream).ReadNextCluster())) {
                    try {
                        uint inputDataLength = inputData.DataSize;
                        if (int.MaxValue < inputDataLength) {
                            throw new ApplicationException();
                        }
                        Helpers.Memcpy(inputData.Data, rawResult, (int)inputDataLength);
                        rawResult += inputDataLength;
                    }
                    finally {
                        inputData.Dispose();
                    }
                }
                return result;
            }
        }

        /// <summary>Open a data stream on the data part of this attribute.</summary>
        /// <param name="chunks">Optional parameter.</param>
        /// <returns></returns>
        internal Stream OpenDataStream(List<LogicalChunk> chunks = null)
        {
            if (0 != this.CompressionUnit) {
                throw new NotSupportedException("Compressed streams not supported.");
            }
            if (null == chunks) { chunks = DecodeRunArray(); }
            return new NonResidentDataStream(chunks, false);
        }

        /// <summary>Open a data stream on the data part of this attribute.</summary>
        /// <param name="chunks">Optional parameter.</param>
        /// <returns></returns>
        internal Stream OpenDataClusterStream(List<LogicalChunk> chunks = null)
        {
            if (0 != this.CompressionUnit) {
                throw new NotSupportedException("Compressed streams not supported.");
            }
            if (null == chunks) { chunks = DecodeRunArray(); }
            return new NonResidentDataStream(chunks, true);
        }

        /// <summary>An <see cref="NtfsAttribute"/> structure containing members common to resident
        /// and nonresident attributes.</summary>
        internal NtfsAttribute Header;
        /// <summary>Lowest valid virtual cluster number (VCN) for this portion of the attribute
        /// value or 0 if this is the only extent(usually the case). - Only when an attribute list
        /// is used does lowest_vcn != 0 ever occur.</summary>
        internal ulong LowVcn;
        /// <summary>Highest valid vcn of this extent of the attribute value. - Usually there is
        /// only one portion, so this usually equals the attribute value size in clusters minus 1.
        /// Can be -1 for zero length files.Can be 0 for "single extent" attributes.</summary>
        internal ulong HighVcn;
        /// <summary>Byte offset from the beginning of the structure to the mapping pairs array
        /// which contains the mappings between the vcns and the logical cluster numbers(lcns).
        /// When creating, place this at the end of this record header aligned to 8-byte boundary.</summary>
        internal ushort RunArrayOffset;
        /// <summary>The compression unit expressed as the log to the base 2 of the number of
        /// clusters in a compression unit. 0 means not compressed.  (This effectively limits the
        /// compression unit size to be a power of two clusters.)  WinNT4 only uses a value of 4.
        /// Sparse files also have this set to 0 on XPSP2.</summary>
        internal byte CompressionUnit;
        internal byte _filler1;
        internal uint _filler2;
        /* The sizes below are only used when lowest_vcn is zero, as otherwise it would
           be difficult to keep them up-to-date.*/
        /// <summary>Byte size of disk space allocated to hold the attribute value.Always is 
        /// multiple of the cluster size. When a file is compressed, this field is a multiple of
        /// the compression block size (2^compression_unit) and it represents the logically allocated
        /// space rather than the actual on disk usage. For this use the compressed_size (see below).</summary>
        internal ulong AllocatedSize;
        /// <summary>Byte size of the attribute value.Can be larger than allocated_size if
        /// attribute value is compressed or sparse.</summary>
        internal ulong DataSize;
        /// <summary>Byte size of initialized portion of the attribute value.Usually equals
        /// data_size.</summary>
        internal ulong InitializedSize;
        /// <summary>Byte size of the attribute value after compression. Only present when compressed
        /// or sparse. Always is a multiple of the cluster size. Represents the actual amount of disk
        /// space being used on the disk.</summary>
        internal ulong CompressedSize;

        internal class LogicalChunk
        {
            internal LogicalChunk(bool isSparse)
            {
                IsSparse = isSparse;
            }

            internal ulong ClustersCount;
            internal ulong FirstLogicalClusterNumber;
            internal bool IsSparse;

            public override string ToString()
            {
                return string.Format("L={0} LCN={1:X8}{2}",
                    ClustersCount, FirstLogicalClusterNumber, IsSparse ? " (S)" : string.Empty);
            }
        }

        private class NonResidentDataStream : Stream, IClusterStream
        {
            internal NonResidentDataStream(List<LogicalChunk> chunks, bool clusterStreamBehavior)
            {
                _partition = NtfsPartition.Current ?? throw new InvalidOperationException();
                _chunks = chunks ?? throw new InvalidOperationException();
                _chunkEnumerator = _chunks.GetEnumerator();
                // Optimization.
                _clusterSize = _partition.ClusterSize;
                MAX_READ_SECTORS = _partition.SectorsPerCluster;
                BUFFER_SIZE = (int)(MAX_READ_SECTORS * _partition.BytesPerSector);
                // Compute length.
                _length = 0;
                foreach (LogicalChunk scannedChunk in chunks) {
                    _length += (long)(scannedChunk.ClustersCount * _clusterSize);
                }
                _clusterStreamBehavior = clusterStreamBehavior;
            }

            public override bool CanRead
            {
                get { return true; }
            }

            public override bool CanSeek
            {
                get { return !_clusterStreamBehavior; }
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

            IPartitionClusterData IClusterStream.ReadNextCluster()
            {
                if (!_clusterStreamBehavior) {
                    throw new NotSupportedException("Not a cluster stream.");
                }
                int clusterReadResult = _ReadNextCluster();
                IPartitionClusterData result = _clusterData;
                _clusterData = null;
                if (0 >= clusterReadResult) {
                    if (null != result) {
                        result.Dispose();
                    }
                    return null;
                }
                return result;
            }

            private int _ReadNextCluster()
            {
                int result = 0;
                uint sectorsPerCluster = _partition.SectorsPerCluster;
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
                if (FeaturesContext.InvariantChecksEnabled) {
                    if (null == _currentChunk) {
                        throw new ApplicationException();
                    }
                    if (_currentChunkClusterIndex >= _currentChunk.ClustersCount) {
                        throw new ApplicationException();
                    }
                }
                ulong remainingClustersInChunk = _currentChunk.ClustersCount - _currentChunkClusterIndex;
                remainingSectorsInChunk = remainingClustersInChunk * sectorsPerCluster;

                // How many blocks should we read ?
                readSectorsCount = MAX_READ_SECTORS;
                if (FeaturesContext.InvariantChecksEnabled) {
                    if (readSectorsCount > remainingSectorsInChunk) {
                        throw new ApplicationException();
                    }
                }
                if (FeaturesContext.InvariantChecksEnabled) {
                    if (0 != (readSectorsCount % sectorsPerCluster)) {
                        throw new ApplicationException();
                    }
                }
                // Perform read and reinitialize some internal values.
                ulong readFromSector = readFromCluster * sectorsPerCluster;
                _clusterData = (_currentChunk.IsSparse)
                    ? _partition.ReadSparseSectors(readSectorsCount)
                    : _partition.ReadSectors(readFromSector, readSectorsCount);
                if (null == _clusterData) {
                    throw new ApplicationException();
                }
                _clusterDataPosition = 0;
                _currentChunkClusterIndex++;
                if (FeaturesContext.InvariantChecksEnabled) {
                    if (int.MaxValue < _clusterData.DataSize) {
                        throw new ApplicationException();
                    }
                }
                return (int)_clusterData.DataSize;
            }

            public override unsafe int Read(byte[] buffer, int offset, int count)
            {
                if (_clusterStreamBehavior) {
                    throw new NotSupportedException("Not supported by cluster stream.");
                }
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
                            if (0 >= _ReadNextCluster()) {
                                return result;
                            }
                            _clusterDataPosition = 0;
                            _currentChunkClusterIndex++;
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
                if (_clusterStreamBehavior) {
                    throw new NotSupportedException("Not supported by cluster stream.");
                }
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

            /// <summary>Only meaningfull for non resident sparse data attribute.</summary>
            void IClusterStream.SeekToNextNonEmptyCluster()
            {
                if (!_clusterStreamBehavior) {
                    throw new NotSupportedException("Not a cluster stream.");
                }
                if ((null != _currentChunk) && _currentChunk.IsSparse) {
                    _currentChunk = null;
                }
                while (true) {
                    if (null != _currentChunk) {
                        if (_currentChunk.IsSparse) {
                            _currentChunk = null;
                        }
                        else {
                            if (_currentChunkClusterIndex < _currentChunk.ClustersCount) {
                                // Some clusters remaining in current chunk.
                                break;
                            }
                        }
                    }
                    // Need to go on with next chunk.
                    if (!_chunkEnumerator.MoveNext()) {
                        // No more data available from the partition.
                        _currentChunk = null;
                        return;
                    }
                    _currentChunk = _chunkEnumerator.Current;
                    if (!_currentChunk.IsSparse) {
                        _currentChunkClusterIndex = 0;
                        _currentChunkRemainingBytesCount = _clusterSize * _currentChunk.ClustersCount;
                        ulong seekTo = (_currentChunk.ClustersCount * _partition.SectorsPerCluster * _partition.BytesPerSector);
                        if (long.MaxValue < seekTo) {
                            throw new NotImplementedException();
                        }
                        _position += (long)seekTo;
                        return;
                    }
                    else {
                        ulong moveCount = _currentChunk.ClustersCount * _partition.SectorsPerCluster * _partition.BytesPerSector;
                        if (long.MaxValue < moveCount) {
                            throw new ApplicationException();
                        }
                        _position += (long)moveCount;
                    }
                }
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
            private readonly uint MAX_READ_SECTORS;
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
            private bool _clusterStreamBehavior;
            /// <summary>Current chunk we are reading from.</summary>
            private LogicalChunk _currentChunk;
            /// <summary>Index [0..ClustersCount[ of the first cluster that has not yet been copied
            /// into the local buffer. We never partially read a cluster from the underlying partition,
            /// hence no additional offset is required.</summary>
            private ulong _currentChunkClusterIndex;
            private ulong _currentChunkRemainingBytesCount;
            private long _length;
            /// <summary>The partition this stream belongs to?</summary>
            private NtfsPartition _partition;
            /// <summary>Current position in this stream.</summary>
            private long _position = 0;
        }
    }
}
