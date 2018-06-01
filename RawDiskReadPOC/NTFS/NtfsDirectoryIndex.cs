
namespace RawDiskReadPOC.NTFS
{
    internal class NtfsDirectoryIndex
    {
        /// <summary>The offset, in bytes, from the start of the structure to the first DIRECTORY_ENTRY
        /// structure.</summary>
        internal uint EntriesOffset;
        /// <summary>The size, in bytes, of the portion of the index block that is in use.</summary>
        internal uint IndexBlockLength;
        /// <summary>The size, in bytes, of disk space allocated for the index block</summary>
        internal uint AllocatedSize;
        /// <summary>A bit array of flags specifying properties of the index.The values defined include:
        /// SmallDirectory 0x0000 Directory fits in index root
        /// LargeDirectory 0x0001 Directory overflows index root</summary>
        internal uint Flags;
    }
}
