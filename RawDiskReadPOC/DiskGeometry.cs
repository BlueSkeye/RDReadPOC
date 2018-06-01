using System;
using System.Runtime.InteropServices;

namespace RawDiskReadPOC
{
    internal class DiskGeometry
    {
        internal uint BytesPerSector { get; private set; }

        internal ulong Cylinders { get; private set; }

        internal ulong DiskSize { get; private set; }

        internal IntPtr Handle {  get { return _hStorage; } }

        internal MEDIA_TYPE MediaType { get; private set; }

        internal uint SectorsPerTrack { get; private set; }

        internal uint TracksPerCylinder { get; private set; }

        /// <summary></summary>
        /// <remarks>See https://msdn.microsoft.com/en-us/library/windows/desktop/cc644950(v=vs.85).aspx</remarks>
        internal unsafe void Acquire(IntPtr handle)
        {
            _hStorage = handle;
            byte[] buffer = new byte[ConservativeExtendedGeometryStructureSize];
            fixed(void* pBuffer = buffer) {
                uint returnedBytes;
                if (!Natives.DeviceIoControl(handle, IOCTL_DISK_GET_DRIVE_GEOMETRY_EX, IntPtr.Zero, 0,
                    pBuffer, (uint)buffer.Length,
                    // &geometry, (uint)Marshal.SizeOf(geometry),
                    out returnedBytes, IntPtr.Zero))
                {
                    int nativeError = Marshal.GetLastWin32Error();
                    throw new ApplicationException(string.Format("DeviceIoControl error 0x{0:X8}", nativeError));
                }
                DISK_GEOMETRY_EX* pGeometry = (DISK_GEOMETRY_EX*)pBuffer;
                this.BytesPerSector = pGeometry->Geometry.BytesPerSector;
                this.Cylinders = pGeometry->Geometry.Cylinders;
                this.DiskSize = pGeometry->DiskSize;
                this.MediaType = pGeometry->Geometry.MediaType;
                this.SectorsPerTrack = pGeometry->Geometry.SectorsPerTrack;
                this.TracksPerCylinder = pGeometry->Geometry.TracksPerCylinder;
                return;
            }
        }

        private ulong GetStreamOffset(uint absoluteSectorNumber)
        {
            return absoluteSectorNumber * this.BytesPerSector;
        }

        [Serializable()]
        private struct DISK_GEOMETRY_EX
        {
            internal DISK_GEOMETRY Geometry;
            internal ulong DiskSize;
            // GPT or MBR disk partition info
            // DISK_PARTITION_INFO
        }

        [Serializable()]
        private struct DISK_GEOMETRY
        {
            internal ulong Cylinders;
            internal MEDIA_TYPE MediaType;
            internal uint TracksPerCylinder;
            internal uint SectorsPerTrack;
            internal uint BytesPerSector;
        }

        private struct MBR_DISK_PARTITION_INFO
        {
            internal uint SizeOfPartitionInfo;
            internal PARTITION_STYLE PartitionStyle;
            internal uint Signature;
        }

        [Serializable()]
        private struct GPT_DISK_PARTITION_INFO
        {
            internal uint SizeOfPartitionInfo;
            internal PARTITION_STYLE PartitionStyle;
            internal Guid DiskId;
        }

        internal enum MEDIA_TYPE
        {
            Unknown = 0x00,
            F5_1Pt2_512 = 0x01,
            F3_1Pt44_512 = 0x02,
            F3_2Pt88_512 = 0x03,
            F3_20Pt8_512 = 0x04,
            F3_720_512 = 0x05,
            F5_360_512 = 0x06,
            F5_320_512 = 0x07,
            F5_320_1024 = 0x08,
            F5_180_512 = 0x09,
            F5_160_512 = 0x0a,
            RemovableMedia = 0x0b,
            FixedMedia = 0x0c,
            F3_120M_512 = 0x0d,
            F3_640_512 = 0x0e,
            F5_640_512 = 0x0f,
            F5_720_512 = 0x10,
            F3_1Pt2_512 = 0x11,
            F3_1Pt23_1024 = 0x12,
            F5_1Pt23_1024 = 0x13,
            F3_128Mb_512 = 0x14,
            F3_230Mb_512 = 0x15,
            F8_256_128 = 0x16,
            F3_200Mb_512 = 0x17,
            F3_240M_512 = 0x18,
            F3_32M_512 = 0x19
        }

        internal enum PARTITION_STYLE
        { 
            PARTITION_STYLE_MBR  = 0,
            PARTITION_STYLE_GPT  = 1,
            PARTITION_STYLE_RAW  = 2
        }

        private IntPtr _hStorage;
        private const int ConservativeExtendedGeometryStructureSize = 1024;
        private static uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX =
            Natives.CTL_CODE(Natives.FILE_DEVICE_TYPE.IOCTL_DISK_BASE, 0x0028, Natives.IOCTL_METHOD.METHOD_BUFFERED,
                Natives.IOCTL_ACCESS.FILE_ANY_ACCESS);
    }
}
