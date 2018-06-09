using System;
using System.Collections.Generic;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>An extension of the NtfsFileRecord structure providing methods specific to this
    /// record.</summary>
    internal struct NtfsMFTFileRecord
    {
        private unsafe NtfsMFTFileRecord(byte* rawData)
        {
            RecordBase = *((NtfsRecord*)rawData);
        }

        internal readonly NtfsRecord RecordBase;

        internal static unsafe void AssertMFTRecordCachingInvariance(PartitionManager manager)
        {
            if (null == manager) { throw new ArgumentNullException(); }
            foreach (PartitionManager.PartitionBase partition in manager.EnumeratePartitions()) {
                if (!partition.ShouldCapture) { continue; }
                for (int index= 0; index < 5; index++) {
                    GC.Collect();
                    // We want to make sure the returned value is always the same pointer, otherwise
                    // we will eat memory.
                    NtfsMFTFileRecord* c1 = GetMFTRecord(partition);
                    NtfsMFTFileRecord* c2 = GetMFTRecord(partition);
                    if ((ulong)c1 != (ulong)c2) { throw new ApplicationException(); }
                }
            }
        }

        internal static unsafe NtfsMFTFileRecord* Create(NtfsPartition owner, byte* rawData)
        {
            if (null == owner) { throw new ArgumentNullException(); }
            if (_gcPreventer.ContainsKey(owner.StartSector)) {
                throw new InvalidOperationException();
            }
            // TODO / WARNING : Should be tested with GC.
            NtfsMFTFileRecord result = new NtfsMFTFileRecord(rawData);
            _gcPreventer.Add(owner.StartSector, result);
            return &result;
        }

        internal unsafe static NtfsMFTFileRecord* GetMFTRecord(PartitionManager.PartitionBase ownedBy)
        {
            NtfsMFTFileRecord result;
            if (!_gcPreventer.TryGetValue(ownedBy.StartSector, out result)) {
                throw new InvalidOperationException();
            }
            return &result;
        }

        internal void GetRecordEnumerator()
        {
            throw new NotImplementedException();
        }

        private static Dictionary<uint, NtfsMFTFileRecord> _gcPreventer =
            new Dictionary<uint, NtfsMFTFileRecord>();
    }
}
