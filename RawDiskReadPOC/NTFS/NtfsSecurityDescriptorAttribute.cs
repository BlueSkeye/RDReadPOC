using System;

namespace RawDiskReadPOC.NTFS
{
    internal struct NtfsSecurityDescriptorAttribute
    {
        internal void Dump()
        {
            Header.AssertResident();
            Header.Dump();
            Console.WriteLine("\tTODO dump content");
            return;
        }

        internal NtfsResidentAttribute Header;
    }
}
