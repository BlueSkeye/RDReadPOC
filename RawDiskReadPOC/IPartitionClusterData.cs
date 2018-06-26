using System;

namespace RawDiskReadPOC
{
    internal interface IPartitionClusterData : IDisposable
    {
        uint DataSize { get; }
        unsafe byte* Data { get; }
    }
}
