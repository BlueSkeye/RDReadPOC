using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>NOTE: Always resident.</remarks>
    internal struct NtfsObjectIdAttribute
    {
        /// <summary>Unique id assigned to the file.</summary>
        internal Guid ObjectId;

        // The following fields are optional. The attribute value size is 16 bytes, i.e.
        // sizeof(Guid), if these are not present at all.Note, the entries can be present
        // but one or more (or all) can be zero meaning that that particular value(s)
        // is(are) not defined.

        /// <summary>Unique id of volume on which the file was first created.</summary>
        internal Guid BirthVolumeId;
        /// <summary>Unique id of file when it was first created.</summary>
        internal Guid BirthObjectId;
        /// <summary>Reserved, zero.</summary>
        internal Guid DomainId;
    }
}
