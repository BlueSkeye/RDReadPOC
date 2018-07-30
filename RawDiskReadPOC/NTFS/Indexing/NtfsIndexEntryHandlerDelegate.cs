
namespace RawDiskReadPOC.NTFS.Indexing
{
    internal unsafe delegate bool NtfsIndexEntryHandlerDelegate(NtfsIndexEntryHeader* entry);
}