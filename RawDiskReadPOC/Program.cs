using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RawDiskReadPOC
{
    public static class Program
    {
        public static unsafe int Main(string[] args)
        {
            IntPtr handle = IntPtr.Zero;
            int nativeError;
            DiskGeometry geometry = new DiskGeometry();

            try {
                handle = Natives.CreateFile2(@"\\.\PhysicalDrive0", 0x80000000 /* GENERIC_READ */,
                    0x02 /* FILE_SHARE_WRITE */, 3 /* OPEN_EXISTING */, IntPtr.Zero);
                geometry.Acquire(handle);
                nativeError = Marshal.GetLastWin32Error();
                if ((IntPtr.Zero == handle) || (0 != nativeError)) {
                    Console.WriteLine("Physical drive opening failed. Error 0x{0:X8}", nativeError);
                    return 1;
                }
                byte[] buffer = new byte[4096];
                fixed(void* pBuffer = buffer) {
                    uint numBytesRead;
                    if (!Natives.ReadFile(handle, pBuffer, (uint)buffer.Length, out numBytesRead, IntPtr.Zero)) {
                        nativeError = Marshal.GetLastWin32Error();
                        Console.WriteLine("Initial read failed. Error 0x{0:X8}", nativeError);
                        return 2;
                    }
                }
            }
            finally {
                if (IntPtr.Zero == handle) {
                    Natives.CloseHandle(handle);
                    handle = IntPtr.Zero;
                }
            }
            return 0;
        }
    }
}
