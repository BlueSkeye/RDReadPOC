using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>The SID structure is a variable-length structure used to uniquely identify
    /// users or groups.SID stands for security identifier. The standard textual
    /// representation of the SID is of the form:
    /// S-R-I-S-S...
    /// Where:
    /// - The first "S" is the literal character 'S' identifying the following digits as
    ///   a SID.
    /// - R is the revision level of the SID expressed as a sequence of digits either in
    ///   decimal or hexadecimal (if the later, prefixed by "0x").
    /// - I is the 48-bit identifier_authority, expressed as digits as R above.
    /// - S... is one or more sub_authority values, expressed as digits as above.
    /// Example SID; the domain-relative SID of the local Administrators group on Windows
    /// NT/2k:
    /// S-1-5-32-544
    /// This translates to a SID with:
    /// revision = 1,
    /// sub_authority_count = 2,
    /// identifier_authority = { 0, 0, 0, 0, 0, 5 },	// SECURITY_NT_AUTHORITY
    /// sub_authority[0] = 32,			// SECURITY_BUILTIN_DOMAIN_RID
    /// sub_authority[1] = 544			// DOMAIN_ALIAS_RID_ADMINS
    /// $O index in FILE_Extend/$Quota: SID of the owner of the user_id.</summary>
    internal struct SID
    {
        internal byte revision;
        internal byte sub_authority_count;
        internal SIDIdentifierAuthority identifier_authority;
        /// <summary>An array of at least one sub_authority.</summary>
        internal int SubAuthorities;
    }

    /// <summary>The SID_IDENTIFIER_AUTHORITY is a 48-bit value used in the SID structure.
    /// NOTE: This is stored as a big endian number, hence the high_part comes before the
    /// low_part.</summary>
    internal struct SIDIdentifierAuthority
    {
        internal byte value0;
        internal byte value1;
        internal byte value2;
        internal byte value3;
        internal byte value4;
        internal byte value5;
    }
}
