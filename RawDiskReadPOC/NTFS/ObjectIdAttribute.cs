using System;

namespace RawDiskReadPOC.NTFS
{
    /// <summary></summary>
    /// <remarks>The object identifier attribute is always resident.</remarks>
    internal class ObjectIdAttribute
    {
        /// <summary>The unique identifier assigned to the file</summary>
        internal Guid ObjectId;
        // The three following fields may also be an unstructured 48 bytes extended info.
        /// <summary>The unique identifier of the volume on which the file was first created. Need not be
        /// present.</summary>
        internal Guid BirthVolumeId;
        /// <summary>The unique identifier assigned to the file when it was first created. Need not be
        /// present.</summary>
        internal Guid BirthObjectId;
        /// <summary>Reserved. Need not be present.</summary>
        internal Guid DomainId;
    }
}
