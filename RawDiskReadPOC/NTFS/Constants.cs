using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawDiskReadPOC.NTFS
{
    internal static class Constants
    {
        internal static readonly uint FileRecordMarker = 0x454C4946; // FILE
        internal static readonly byte[] OEMID = Encoding.ASCII.GetBytes("NTFS    ");
    }
}
