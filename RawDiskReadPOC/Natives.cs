using System;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC
{
    internal static class Natives
    {
        [DllImport("KERNEL32.DLL", SetLastError = true)]
        internal static extern void CloseHandle(
            [In] IntPtr hObject);

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateFile2(
            [In] string lpFileName,
            [In] uint dwDesiredAccess,
            [In] uint dwShareMode,
            [In] uint dwCreationDisposition,
            [In] IntPtr /* LPCREATEFILE2_EXTENDED_PARAMETERS */ pCreateExParams);

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern unsafe bool ReadFile(
            [In] IntPtr hFile,
            [In] void* lpBuffer,
            [In] uint nNumberOfBytesToRead,
            [Out] out uint lpNumberOfBytesRead,
            [In] IntPtr /* LPOVERLAPPED */ lpOverlapped);
    }
}
