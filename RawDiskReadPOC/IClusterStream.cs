using System;

namespace RawDiskReadPOC
{
    internal interface IClusterStream : IDisposable
    {
        IPartitionClusterData ReadNextCluster();
        /// <summary>Only meaningfull for non resident sparse data attribute.</summary>
        void SeekToNextNonEmptyCluster();
    }
}
