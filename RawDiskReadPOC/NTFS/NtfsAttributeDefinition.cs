using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>The data attribute of FILE_AttrDef contains a sequence of attribute
    /// definitions for the NTFS volume.With this, it is supposed to be safe for an older
    /// NTFS driver to mount a volume containing a newer NTFS version without damaging it
    /// (that's the theory. In practice it's: not damaging it too much). Entries are sorted
    /// by attribute type. The flags describe whether the attribute can be resident / 
    /// non-resident and possibly other things, but the actual bits are unknown.</summary>
    internal struct NtfsAttributeDefinition
    {
        /// <summary>Unicode name of the attribute. Zero terminated.</summary>
        internal SixtyFourCharactersUnicodeString AttributeName;
        /// <summary>Type of the attribute.</summary>
        internal NtfsAttributeType Type;
        /// <summary>Default display rule. FIXME: What does it mean? (AIA)</summary>
        internal uint DisplayRule;
        /// <summary>Default collation rule.</summary>
        internal NtfsCollation CollationRule;
        /// <summary>Flags describing the attribute.</summary>
        internal DefinitionFlags Flags;
        /// <summary>Optional minimum attribute size.</summary>
        internal long MinimumSize;
        /// <summary>Maximum size of attribute.</summary>
        internal long MaximumSize;

        /// <summary>Very dirty trick for a fixed size 64 unicode characters string
        /// definition.</summary>
        internal struct SixtyFourCharactersUnicodeString
        {
            internal ulong _filler1;
            internal ulong _filler2;
            internal ulong _filler3;
            internal ulong _filler4;
            internal ulong _filler5;
            internal ulong _filler6;
            internal ulong _filler7;
            internal ulong _filler8;
            internal ulong _filler9;
            internal ulong _filler10;
            internal ulong _filler11;
            internal ulong _filler12;
            internal ulong _filler13;
            internal ulong _filler14;
            internal ulong _filler15;
            internal ulong _filler16;
        }

        /// <summary>The flags (32-bit) describing attribute properties in the attribute definition
        /// structure.
        /// FIXME: This information is based on Regis's information and, according to him, it is not
        /// certain and probably incomplete. The INDEXABLE flag is fairly certainly correct as only
        /// the file name attribute has this flag set and this is the only attribute indexed in NT4.</summary>
        [Flags()]
        internal enum DefinitionFlags : uint
        {
            /// <summary>Attribute can be indexed.</summary>
            Indexable = 0x02,
            /// <summary>Attribute type can be present multiple times in the mft records of an inode.</summary>
            Multiple = 0x04,
            /// <summary>Attribute value must contain at least one non-zero byte.</summary>
            NotZero = 0x08,
            /// <summary>Attribute must be indexed and the attribute value must be unique for the
            /// attribute type in all of the mft records of an inode.</summary>
            IndexedUnique = 0x10,
            /// <summary>Attribute must be named and the name must be unique for the attribute type
            /// in all of the mft records of an inode.</summary>
            NamedUnique= 0x20,
            /// <summary>Attribute must be resident.</summary>
            Resident = 0x40,
            /// <summary>Always log modifications to this attribute, regardless of whether it is
            /// resident or non-resident.  Without this, only log modifications if the attribute is
            /// resident.</summary>
            AlwaysLog = 0x80
        }
    }
}
