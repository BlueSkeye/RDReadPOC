using System;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC
{
    internal static class Natives
    {
        internal static uint CTL_CODE(FILE_DEVICE_TYPE DeviceType, ushort Function, IOCTL_METHOD Method,
            IOCTL_ACCESS Access) 
        {
            return (uint)(((ushort)DeviceType << 16) | ((ushort)Access << 14) | (Function << 2) | (byte)Method);
        }

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

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        internal static unsafe extern bool DeviceIoControl(
            [In] IntPtr hDevice,               // handle to a partition
            [In] uint IOCTL_STORAGE_QUERY_PROPERTY, // dwIoControlCode
            [In] IntPtr lpInBuffer,            // input buffer - STORAGE_PROPERTY_QUERY structure
            [In] uint nInBufferSize,         // size of input buffer
            [In] void* lpOutBuffer,           // output buffer - see Remarks
            [In] uint nOutBufferSize,        // size of output buffer
            [Out] out uint lpBytesReturned,       // number of bytes returned
            [In] IntPtr /* LPOVERLAPPED */ lpOverlapped );        // OVERLAPPED structure

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern unsafe bool ReadFile(
            [In] IntPtr hFile,
            [In] void* lpBuffer,
            [In] uint nNumberOfBytesToRead,
            [Out] out uint lpNumberOfBytesRead,
            [In] IntPtr /* LPOVERLAPPED */ lpOverlapped);

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool SetFilePointerEx(
            [In] IntPtr hFile,
            [In] long liDistanceToMove,
            [Out] out ulong lpNewFilePointer,
            [In] uint dwMoveMethod);
        internal const uint FILE_BEGIN = 0;
        internal const uint FILE_CURRENT = 0;
        internal const uint FILE_END = 0;

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode)]
        internal static extern void SetLastError(
            [In] uint errorCode);

        internal enum FILE_DEVICE_TYPE : ushort
        {
            //#define FILE_DEVICE_BEEP                0x00000001
            //#define FILE_DEVICE_CD_ROM              0x00000002
            //#define FILE_DEVICE_CD_ROM_FILE_SYSTEM  0x00000003
            //#define FILE_DEVICE_CONTROLLER          0x00000004
            //#define FILE_DEVICE_DATALINK            0x00000005
            //#define FILE_DEVICE_DFS                 0x00000006
            FILE_DEVICE_DISK = 0x00000007,
            IOCTL_DISK_BASE = FILE_DEVICE_DISK,
            //#define FILE_DEVICE_DISK_FILE_SYSTEM    0x00000008
            //#define FILE_DEVICE_FILE_SYSTEM         0x00000009
            //#define FILE_DEVICE_INPORT_PORT         0x0000000a
            //#define FILE_DEVICE_KEYBOARD            0x0000000b
            //#define FILE_DEVICE_MAILSLOT            0x0000000c
            //#define FILE_DEVICE_MIDI_IN             0x0000000d
            //#define FILE_DEVICE_MIDI_OUT            0x0000000e
            //#define FILE_DEVICE_MOUSE               0x0000000f
            //#define FILE_DEVICE_MULTI_UNC_PROVIDER  0x00000010
            //#define FILE_DEVICE_NAMED_PIPE          0x00000011
            //#define FILE_DEVICE_NETWORK             0x00000012
            //#define FILE_DEVICE_NETWORK_BROWSER     0x00000013
            //#define FILE_DEVICE_NETWORK_FILE_SYSTEM 0x00000014
            //#define FILE_DEVICE_NULL                0x00000015
            //#define FILE_DEVICE_PARALLEL_PORT       0x00000016
            //#define FILE_DEVICE_PHYSICAL_NETCARD    0x00000017
            //#define FILE_DEVICE_PRINTER             0x00000018
            //#define FILE_DEVICE_SCANNER             0x00000019
            //#define FILE_DEVICE_SERIAL_MOUSE_PORT   0x0000001a
            //#define FILE_DEVICE_SERIAL_PORT         0x0000001b
            //#define FILE_DEVICE_SCREEN              0x0000001c
            //#define FILE_DEVICE_SOUND               0x0000001d
            //#define FILE_DEVICE_STREAMS             0x0000001e
            //#define FILE_DEVICE_TAPE                0x0000001f
            //#define FILE_DEVICE_TAPE_FILE_SYSTEM    0x00000020
            //#define FILE_DEVICE_TRANSPORT           0x00000021
            //#define FILE_DEVICE_UNKNOWN             0x00000022
            //#define FILE_DEVICE_VIDEO               0x00000023
            //#define FILE_DEVICE_VIRTUAL_DISK        0x00000024
            //#define FILE_DEVICE_WAVE_IN             0x00000025
            //#define FILE_DEVICE_WAVE_OUT            0x00000026
            //#define FILE_DEVICE_8042_PORT           0x00000027
            //#define FILE_DEVICE_NETWORK_REDIRECTOR  0x00000028
            //#define FILE_DEVICE_BATTERY             0x00000029
            //#define FILE_DEVICE_BUS_EXTENDER        0x0000002a
            //#define FILE_DEVICE_MODEM               0x0000002b
            //#define FILE_DEVICE_VDM                 0x0000002c
            FILE_DEVICE_MASS_STORAGE = 0x0000002D,
            IOCTL_STORAGE_BASE = FILE_DEVICE_MASS_STORAGE,
            //#define FILE_DEVICE_SMB                 0x0000002e
            //#define FILE_DEVICE_KS                  0x0000002f
            //#define FILE_DEVICE_CHANGER             0x00000030
            //#define FILE_DEVICE_SMARTCARD           0x00000031
            //#define FILE_DEVICE_ACPI                0x00000032
            //#define FILE_DEVICE_DVD                 0x00000033
            //#define FILE_DEVICE_FULLSCREEN_VIDEO    0x00000034
            //#define FILE_DEVICE_DFS_FILE_SYSTEM     0x00000035
            //#define FILE_DEVICE_DFS_VOLUME          0x00000036
            //#define FILE_DEVICE_SERENUM             0x00000037
            //#define FILE_DEVICE_TERMSRV             0x00000038
            //#define FILE_DEVICE_KSEC                0x00000039
            //#define FILE_DEVICE_FIPS                0x0000003A
            //#define FILE_DEVICE_INFINIBAND          0x0000003B
            //#define FILE_DEVICE_VMBUS               0x0000003E
            //#define FILE_DEVICE_CRYPT_PROVIDER      0x0000003F
            //#define FILE_DEVICE_WPD                 0x00000040
            //#define FILE_DEVICE_BLUETOOTH           0x00000041
            //#define FILE_DEVICE_MT_COMPOSITE        0x00000042
            //#define FILE_DEVICE_MT_TRANSPORT        0x00000043
            //#define FILE_DEVICE_BIOMETRIC		      0x00000044
            //#define FILE_DEVICE_PMI                 0x00000045
        }

        internal enum IOCTL_ACCESS : ushort
        {
            FILE_ANY_ACCESS = 0,
            FILE_SPECIAL_ACCESS = FILE_ANY_ACCESS,
            FILE_READ_ACCESS = 0x0001,    // file & pipe
            FILE_WRITE_ACCESS = 0x0002,    // file & pipe
        }

        internal enum IOCTL_METHOD : byte
        {
            METHOD_BUFFERED = 0,
            METHOD_IN_DIRECT = 1,
            METHOD_OUT_DIRECT = 2,
            METHOD_NEITHER = 3,
        }
    }
}
