using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>Provides additional methods specific to the bitmap attribute.</summary>
    internal struct NtfsBitmapAttribute
    {
        internal NtfsNonResidentAttribute nonResidentHeader;
    }
}
