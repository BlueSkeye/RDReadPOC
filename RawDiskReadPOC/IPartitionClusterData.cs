using System;

namespace RawDiskReadPOC
{
    internal delegate void IPartitionClusterDataDisposedDelegate(IPartitionClusterData data);

    internal interface IPartitionClusterData : IDisposable
    {
        event IPartitionClusterDataDisposedDelegate Disposed;
        uint DataSize { get; }
        unsafe byte* Data { get; }
        IPartitionClusterData NextInChain { get; }

        void BinaryDump();
        void BinaryDumpChain();
        IPartitionClusterData Zeroize();
    }
}
