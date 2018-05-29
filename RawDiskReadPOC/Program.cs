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

            try {
                handle = CreateFile2(@"\\.\PhysicalDrive0", 0x80000000 /* GENERIC_READ */,
                    0x02 /* FILE_SHARE_WRITE */, 3 /* OPEN_EXISTING */, IntPtr.Zero);
                nativeError = Marshal.GetLastWin32Error();
                if ((IntPtr.Zero == handle) || (0 != nativeError)) {
                    Console.WriteLine("Physical drive opening failed. Error 0x{0:X8}", nativeError);
                    return 1;
                }
                byte[] buffer = new byte[4096];
                fixed(void* pBuffer = buffer) {
                    uint numBytesRead;
                    if (!ReadFile(handle, pBuffer, (uint)buffer.Length, out numBytesRead, IntPtr.Zero)) {
                        nativeError = Marshal.GetLastWin32Error();
                        Console.WriteLine("Initial read failed. Error 0x{0:X8}", nativeError);
                        return 2;
                    }
                }
            }
            finally {
                if (IntPtr.Zero == handle) {
                    CloseHandle(handle);
                    handle = IntPtr.Zero;
                }
            }
            return 0;
        }

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        private static extern void CloseHandle(
            [In] IntPtr hObject);

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile2(
            [In] string lpFileName,
            [In] uint dwDesiredAccess,
            [In] uint dwShareMode,
            [In] uint dwCreationDisposition,
            [In] IntPtr /* LPCREATEFILE2_EXTENDED_PARAMETERS */ pCreateExParams);

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern unsafe bool ReadFile(
            [In] IntPtr hFile,
            [In] void* lpBuffer,
            [In] uint nNumberOfBytesToRead,
            [Out] out uint lpNumberOfBytesRead,
            [In] IntPtr /* LPOVERLAPPED */ lpOverlapped);
    }
}
