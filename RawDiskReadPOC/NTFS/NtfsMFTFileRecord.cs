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
            _localBuffer = (NtfsFileRecord*)(byte*)Marshal.AllocCoTaskMem(copiedBytesCount);
            Helpers.Memcpy(rawData, (byte*)_localBuffer, copiedBytesCount);
            return;
        }

        internal unsafe NtfsFileRecord* RecordBase
        {
            get { return (NtfsFileRecord*)_localBuffer; }
        }

        internal static unsafe void AssertMFTRecordCachingInvariance(PartitionManager manager)
        {
            if (null == manager) { throw new ArgumentNullException(); }
            foreach (GenericPartition partition in manager.EnumeratePartitions()) {
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
            if (_gcPreventer.ContainsKey(owner)) {
                throw new InvalidOperationException();
            }
            NtfsMFTFileRecord result = new NtfsMFTFileRecord(rawData);
            result.AssertNoOverflowingAttribute();
            _gcPreventer.Add(owner, result);
            return result;
        }

        internal unsafe void EnumerateRecordAttributes(RecordAttributeEnumeratorCallbackDelegate callback)
        {
            _localBuffer->EnumerateRecordAttributes(callback);
        }

        internal unsafe void EnumerateRecords(FileRecordEnumeratorDelegate callback)
        {
            NtfsNonResidentAttribute* dataAttribute =
                (NtfsNonResidentAttribute*)RecordBase->GetAttribute(NtfsAttributeType.AttributeData);
            dataAttribute->AssertNonResident();
            if (null == dataAttribute) {
                throw new ApplicationException();
            }
            NtfsPartition partition = NtfsPartition.Current;
            ulong clusterSize = partition.ClusterSize;
            ulong mftRecordPerCluster = clusterSize / partition.MFTEntrySize;
            ulong sectorsPerMFTRecord = partition.MFTEntrySize / partition.BytesPerSector;
            if (FeaturesContext.InvariantChecksEnabled) {
                if (0 != (clusterSize % partition.MFTEntrySize)) {
                    throw new ApplicationException();
                }
                if (0 != (partition.MFTEntrySize % partition.BytesPerSector)) {
                    throw new ApplicationException();
                }
            }
            ulong recordsPerCluster = clusterSize / NtfsFileRecord.RECORD_SIZE;
            byte[] localBuffer = new byte[clusterSize];
            Stream mftDataStream = dataAttribute->OpenDataStream();
            try {
                NtfsBitmapAttribute* bitmap = (NtfsBitmapAttribute*)RecordBase->GetAttribute(NtfsAttributeType.AttributeBitmap);
                if (null == bitmap) { throw new AssertionException("Didn't find the $MFT bitmap attribute."); }
                IEnumerator<bool> bitmapEnumerator = bitmap->GetContentEnumerator();
                ulong recordIndex = 0;
                while (bitmapEnumerator.MoveNext()) {
                    recordIndex++;
                    if (!bitmapEnumerator.Current) {
                        continue;
                    }
                    ulong targetClusterIndex = mftRecordPerCluster / recordIndex ;
                    ulong sectorIndexInCluster = (recordIndex % mftRecordPerCluster) * sectorsPerMFTRecord;
                    ulong targetPosition = targetClusterIndex * clusterSize;
                    if (long.MaxValue < targetPosition) {
                        throw new ApplicationException();
                    }
                    mftDataStream.Seek((long)(targetPosition), SeekOrigin.Begin);
                    int readCount = mftDataStream.Read(localBuffer, 0, (int)clusterSize);
                    if (0 == readCount) {
                        break;
                    }
                    if ((int)clusterSize != readCount) {
                        throw new ApplicationException();
                    }
                    fixed(byte* nativeBuffer = localBuffer) {
                        Helpers.BinaryDump(nativeBuffer, (uint)clusterSize);
                        byte* nativeRecord = nativeBuffer + (NtfsFileRecord.RECORD_SIZE * sectorIndexInCluster);
                        // TODO Make sure the result is inside the buffer.
                        if (!callback((NtfsFileRecord*)nativeRecord)) { break; }
                    }
                }
                if (null != mftDataStream) { mftDataStream.Close(); }
            }
            catch {
                if (null != mftDataStream) { mftDataStream.Close(); }
                throw;
            }
        }

        internal unsafe static NtfsMFTFileRecord GetMFTRecord(GenericPartition ownedBy)
        {
            NtfsMFTFileRecord result;
            if (!_gcPreventer.TryGetValue(ownedBy, out result)) {
                throw new InvalidOperationException();
            }
            return result;
        }

        internal void GetRecordEnumerator()
        {
            throw new NotImplementedException();
        }

        private static Dictionary<GenericPartition, NtfsMFTFileRecord> _gcPreventer =
            new Dictionary<GenericPartition, NtfsMFTFileRecord>();
        internal unsafe NtfsFileRecord* _localBuffer;
    }
}
