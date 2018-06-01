
namespace RawDiskReadPOC.NTFS
{
    internal class ReparsePointAttribute
    {
        /// <summary>The reparse tag identifies the type of reparse point.The high order three bits of the
        /// tag indicate whether the tag is owned by Microsoft, whether there is a high latency in
        /// accessing the file data, and whether the filename is an alias for another object.</summary>
        internal uint ReparseTag;
        /// <summary>The size, in bytes, of the reparse data in the ReparseData member.</summary>
        internal ushort ReparseDataLength;
        internal ushort Reserved;
        /// <summary>The reparse data.The interpretation of the data depends upon the type of the reparse
        /// point.</summary>
        internal byte ReparseData;
    }
}
