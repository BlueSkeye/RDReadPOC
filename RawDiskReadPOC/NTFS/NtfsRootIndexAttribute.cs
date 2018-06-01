
namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>An INDEX_ROOT structure is followed by a sequence of DIRECTORY_ENTRY structures.</remarks>
    internal class NtfsRootIndexAttribute
    {
        /// <summary>The type of the attribute that is indexed</summary>
        internal NtfsAttributeType Type;
        /// <summary>A numeric identifier of the collation rule used to sort the index entries.</summary>
        internal uint CollationRule;
        /// <summary>The number of bytes per index block.</summary>
        internal uint BytesPerIndexBlock;
        /// <summary>The number of clusters per index block.</summary>
        internal uint ClustersPerIndexBlock;
        /// <summary>A DIRECTORY_INDEX structure.</summary>
        internal NtfsDirectoryEntry DirectoryIndex;
    }
}
