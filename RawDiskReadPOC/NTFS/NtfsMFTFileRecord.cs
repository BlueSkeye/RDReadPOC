using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>An extension of the NtfsFileRecord structure providing methods specific to this
    /// record.</summary>
    internal class NtfsMFTFileRecord
    {
        internal unsafe delegate bool FileRecordEnumeratorDelegate(NtfsFileRecord* record);

        private unsafe NtfsMFTFileRecord(byte* rawData)
        {
            // Capture raw data.
            NtfsFileRecord* record = (NtfsFileRecord*)rawData;
            int copiedBytesCount = (int)record->BytesAllocated;
            _localBuffer = (byte*)Marshal.AllocCoTaskMem(copiedBytesCount);
            Helpers.Memcpy(rawData, _localBuffer, copiedBytesCount);
            return;
        }

        internal unsafe NtfsFileRecord* RecordBase
        {
            get { return (NtfsFileRecord*)_localBuffer; }
        }

        internal static unsafe void AssertMFTRecordCachingInvariance(PartitionManager manager)
        {
            if (null == manager) { throw new ArgumentNullException(); }
            foreach (PartitionManager.PartitionBase partition in manager.EnumeratePartitions()) {
                if (!partition.ShouldCapture) { continue; }
                for (int index= 0; index < 5; index++) {
                    GC.Collect();
                    // We want to make sure the returned value is always the same pointer, otherwise
                    // we will eat memory.
                    NtfsMFTFileRecord c1 = GetMFTRecord(partition);
                    NtfsMFTFileRecord c2 = GetMFTRecord(partition);
                    if (!object.ReferenceEquals(c1, c2)) {
                        throw new AssertionException("MFT file record caching is not GC resistant.");
                    }
                }
            }
        }

        private unsafe void AssertNoOverflowingAttribute()
        {
            NtfsFileRecord.EnumerateRecordAttributes(this.RecordBase,
                delegate (NtfsAttribute* attribute) {
                    if (NtfsAttributeType.AttributeAttributeList == attribute->AttributeType) {
                        throw new AssertionException("$MFT record not expected to contain an attribute list attribute");
                    }
                    return true;
                });
        }

        internal static unsafe NtfsMFTFileRecord Create(NtfsPartition owner, byte* rawData)
        {
            if (null == owner) { throw new ArgumentNullException(); }
            if (_gcPreventer.ContainsKey(owner.StartSector)) {
                throw new InvalidOperationException();
            }
            NtfsMFTFileRecord result = new NtfsMFTFileRecord(rawData);
            result.AssertNoOverflowingAttribute();
            _gcPreventer.Add(owner.StartSector, result);
            return result;
        }

        internal unsafe void EnumerateRecords(NtfsPartition partition, FileRecordEnumeratorDelegate callback)
        {
            NtfsNonResidentAttribute* dataAttribute =
                (NtfsNonResidentAttribute*)RecordBase->GetAttribute(NtfsAttributeType.AttributeData);
            dataAttribute->AssertNonResident();
            if (null == dataAttribute) {
                throw new ApplicationException();
            }
            ulong clusterSize = partition.ClusterSize;
            ulong recordsPerCluster = clusterSize / RECORD_SIZE;
            byte[] localBuffer = new byte[clusterSize];
            Stream dataStream = dataAttribute->OpenDataStream(partition);
            try {
                NtfsBitmapAttribute* bitmap = (NtfsBitmapAttribute*)RecordBase->GetAttribute(NtfsAttributeType.AttributeBitmap);
                if (null == bitmap) { throw new AssertionException("Didn't find the $MFT bitmap attribute."); }
                ulong currentClusterIndex = 0;
                bool endOfStream = false;
                foreach(ulong itemIndex in bitmap->EnumerateUsedItemIndex(partition)) {
                    ulong targetClusterIndex = itemIndex / recordsPerCluster;
                    ulong recordIndexInCluster = itemIndex % recordsPerCluster;
                    // TODO : Seek is not supported, so we need to read the whold stream.
                    // CONSIDER : Implement Seek
                    while (currentClusterIndex <= targetClusterIndex) {
                        int readCount = dataStream.Read(localBuffer, 0, (int)clusterSize);
                        if (0 == readCount) {
                            endOfStream = true;
                            break;
                        }
                        if ((int)clusterSize != readCount) {
                            throw new ApplicationException();
                        }
                        currentClusterIndex++;
                    }
                    if (endOfStream) { break; }
                    fixed(byte* nativeBuffer = localBuffer) {
                        byte* nativeRecord = nativeBuffer + (RECORD_SIZE * recordIndexInCluster);
                        // TODO Make sure the result is inside the buffer.
                        if (!callback((NtfsFileRecord*)nativeRecord)) { break; }
                    }
                }
                if (null != dataStream) { dataStream.Close(); }
            }
            catch {
                if (null != dataStream) { dataStream.Close(); }
                throw;
            }
        }

        internal unsafe static NtfsMFTFileRecord GetMFTRecord(PartitionManager.PartitionBase ownedBy)
        {
            NtfsMFTFileRecord result;
            if (!_gcPreventer.TryGetValue(ownedBy.StartSector, out result)) {
                throw new InvalidOperationException();
            }
            return result;
        }

        internal void GetRecordEnumerator()
        {
            throw new NotImplementedException();
        }

        private const ulong RECORD_SIZE = 1024;
        private static Dictionary<uint, NtfsMFTFileRecord> _gcPreventer =
            new Dictionary<uint, NtfsMFTFileRecord>();
        internal unsafe byte* _localBuffer;
    }
}
