using System.Collections.Generic;
using System.IO;

namespace RawDiskReadPOC.NTFS
{
    /// <summary>Provides additional methods specific to the bitmap attribute.</summary>
    internal struct NtfsBitmapAttribute
    {
        internal void AssertNonResident()
        {
            if (0 == nonResidentHeader.Attribute.Nonresident) {
                // TODO : Consider handling resident bitmap attribute.
                // Technically speaking this should be a NotSupportedException.
                throw new AssertionException("Bitmap attribute was expected to be non resident.");
            }
        }

        /// <summary>Open the data stream for this bitmap attribute and scan each bit
        /// yielding either true or false if bit is set or cleared.</summary>
        /// <param name="partition"></param>
        /// <returns></returns>
        internal IEnumerable<ulong> EnumerateUsedItemIndex(NtfsPartition partition)
        {
            ulong indexValue = 0;
            using (Stream input = nonResidentHeader.OpenDataStream(partition)) {
                int @byte;
                while (-1 != (@byte = input.ReadByte())) {
                    if (0 == @byte) { continue; }
                    for(int index = 0; index < 8; index++) {
                        if (0 != ((byte)@byte & (1 << index))) { yield return indexValue; }
                        indexValue++;
                    }
                }
            }
            yield break;
        }

        internal NtfsNonResidentAttribute nonResidentHeader;
    }
}
