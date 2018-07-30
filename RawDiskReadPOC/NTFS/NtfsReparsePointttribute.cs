using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>NOTE: Can be resident or non-resident.</remarks>
    internal struct NtfsReparsePointttribute
    {
        internal Tags ReparseTag;       /* Reparse point type (inc. flags). */
        internal ushort ReparseDataLength;   /* Byte size of reparse data. */
        internal ushort _filler1; /* Align to 8-byte boundary. */
        internal byte data; /* Meaning depends on reparse_tag. */

        /// <summary>The reparse point tag defines the type of the reparse point. It also
        /// includes several flags, which further describe the reparse point. The reparse
        /// point tag is an unsigned 32-bit value divided in three parts:
        /// 1. The least significant 16 bits (i.e.bits 0 to 15) specifiy the type of the
        ///    reparse point.
        /// 2. The 13 bits after this (i.e.bits 16 to 28) are reserved for future use.
        /// 3. The most significant three bits are flags describing the reparse point.
        /// They are defined as follows:
        /// bit 29: Name surrogate bit.If set, the filename is an alias for another objec
        ///         in the system.
        /// bit 30: High-latency bit. If set, accessing the first byte of data will be slow.
        ///         (E.g.the data is stored on a tape drive.)
        /// bit 31: Microsoft bit.If set, the tag is owned by Microsoft. User defined tags
        ///         have to use zero here.</summary>
        [Flags()]
        internal enum Tags : uint
        {
            IsAlias = 0x20000000,
            IsHighLatency = 0x40000000,
            IsMicrosoft = 0x80000000,
            ReservedZero = 0x00000000,
            ReservedOne = 0x00000001,
            ReservedRange = 0x00000001,

            NativeStructureStorage = 0x68000005,
            NativeStructureStorageRecover = 0x68000006,
            SIS = 0x68000007,
            DFS = 0x68000008,

            MountPoint = 0x88000003,
            HSM = 0xa8000004,
            SymbolicLink = 0xe8000000,

            ValidValues = 0xe000ffff
        }
    }
}
