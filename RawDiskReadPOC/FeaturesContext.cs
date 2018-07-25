using System;

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

        internal static bool FindFileAlgorithmTrace
        {
            get { return _findFileAlgorithmTrace; }
            set { _findFileAlgorithmTrace = value; }
        }

        private static bool _findFileAlgorithmTrace =
#if TRC_FINDFILE
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

        internal static void Display()
        {
            if (FeaturesContext.DataPoolChecksEnabled) {
                Console.WriteLine("DatePool checks enabled.");
            }
            Console.WriteLine("FindFile algorithm trace {0}.",
                FeaturesContext.FindFileAlgorithmTrace ? "enabled" : "disabled");
            if (FeaturesContext.InvariantChecksEnabled) {
                Console.WriteLine("Invariant checks enabled.");
            }
        }
    }
}
