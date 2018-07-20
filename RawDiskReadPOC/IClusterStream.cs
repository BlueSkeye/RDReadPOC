using System;

namespace RawDiskReadPOC
{
    internal interface IClusterStream : IDisposable
    {
        IPartitionClusterData ReadNextCluster();
    }
}
