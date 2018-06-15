using System;

namespace RawDiskReadPOC
{
    internal class AssertionException : ApplicationException
    {
        internal AssertionException(string message)
            : base(message)
        {
        }
    }
}
