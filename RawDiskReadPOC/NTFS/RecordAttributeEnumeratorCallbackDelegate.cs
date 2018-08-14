using System.IO;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <param name="value"></param>
    /// <param name="attributeDataStream"></param>
    /// <returns></returns>
    internal unsafe delegate bool RecordAttributeEnumeratorCallbackDelegate(NtfsAttribute* value);
}
