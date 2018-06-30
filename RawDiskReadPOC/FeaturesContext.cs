
namespace RawDiskReadPOC
{
    internal static class FeaturesContext
    {
        internal static bool DataPoolChecksEnabled =>
#if CHK_DATAPOOL
            true;
#else
            false;
#endif

        internal static bool InvariantChecksEnabled =>
#if CHK_INVARIANTS
            true;
#else
            false;
#endif        
    }
}
