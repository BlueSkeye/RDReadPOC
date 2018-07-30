
namespace RawDiskReadPOC.NTFS
{
    /// <summary>The collation rules for sorting views/indexes/etc (32-bit).</summary>
    internal enum NtfsCollation : uint
    {
        /// <summary>Collate by binary compare where the first byte is most significant.</summary>
        Binary = 0,
        /// <summary>Collate filenames as Unicode strings. The collation is done very much
        /// like UnicodeString. In fact I have no idea what the difference is. Perhaps the
        /// difference is that filenames would treat some special characters in an odd way
        /// (see unistr.c::ntfs_collate_names() and unistr.c::legal_ansi_char_array[] for
        /// what I mean but COLLATION_UNICODE_STRING would not give any special treatment
        /// to any characters at all, but this is speculation.</summary>
        FileName = 1,
        /// <summary>Collate Unicode strings by comparing their binary Unicode values,
        /// except that when a character can be uppercased, the upper case value collates
        /// before the lower case one.</summary>
        UnicodeString = 2,
        /// <summary>Sorting is done according to ascending uint key values. E.g. used for
        /// $SII index in FILE_Secure, which sorts by security_id uint.</summary>
        SecureFileUint = 0x10,
        /// <summary>Sorting is done according to ascending SID values. E.g. used for $O
        /// index in FILE_Extend/$Quota.</summary>
        SecureFileSID = 0x11,
        /// <summary>Sorting is done first by ascending hash values and second by ascending
        /// security_id values. E.g. used for $SDH index in FILE_Secure.</summary>
        SecurityFileHash = 0x12,
        /// <summary>Sorting is done according to a sequence of ascending uint key values.
        /// E.g. used for $O index in FILE_Extend/$ObjId, which sorts by object_id (ushort),
        /// by splitting up the object_id in four uint values and using them as individual
        /// keys. E.g.take the following two security_ids, stored as follows on disk:
        /// 1st: a1 61 65 b7 65 7b d4 11 9e 3d 00 e0 81 10 42 59
        /// 2nd: 38 14 37 d2 d2 f3 d4 11 a5 21 c8 6b 79 b1 97 45
        /// To compare them, they are split into four le32 values each, like so:
        /// 1st: 0xb76561a1 0x11d47b65 0xe0003d9e 0x59421081
        /// 2nd: 0xd2371438 0x11d4f3d2 0x6bc821a5 0x4597b179
        /// Now, it is apparent why the 2nd object_id collates after the 1st: the first uint
        /// value of the 1st object_id is less than the first uint of the 2nd object_id. If
        /// the first uint values of both object_ids were equal then the second uint values
        /// would be compared, etc.</summary>
        SecurityFileMultipleUints = 0x13,
    }
}
