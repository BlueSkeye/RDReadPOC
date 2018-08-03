
namespace RawDiskReadPOC.NTFS
{
    internal struct NtfsGUID
    {
        /* GUID structures store globally unique identifiers(GUID). A GUID is a 128-bit value
         * consisting of one group of eight hexadecimal digits, followed by three groups of
         * four hexadecimal digits each, followed by one group of twelve hexadecimal digits.
         * GUIDs are Microsoft's implementation of the distributed computing environment(DCE)
         * universally unique identifier (UUID).
         * Example of a GUID in string format:
         *	514AFB70-78F2-400E-82E4-E251889DD21D
         * And the same as a sequence of bytes on disk in hex:
         *	70FB4A51F2780E4082E4E251889DD21D*/
        /// <summary>The first eight hexadecimal digits of the GUID.</summary>
        internal int data1;
        /// <summary>The first group of four hexadecimal digits.</summary>
        internal short data2;
        /// <summary>The second group of four hexadecimal digits.</summary>
        internal short data3;
        /// <summary>The first two bytes are the third group of four hexadecimal digits.
        /// </summary>
        internal short data4;
        /// <summary>The remaining six bytes are the final 12 hexadecimal digits.</summary>
        internal byte data5;
        internal byte data6;
        internal byte data7;
        internal byte data8;
        internal byte data9;
        internal byte data10;
    }
}
