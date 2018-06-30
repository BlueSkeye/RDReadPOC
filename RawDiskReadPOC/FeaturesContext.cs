
namespace RawDiskReadPOC
{
    internal static class FeaturesContext
    {
        internal static bool InvariantChecksEnabled =>
#if CHK_INVARIANTS
            true;
#else
            false;
#endif
    }
}
