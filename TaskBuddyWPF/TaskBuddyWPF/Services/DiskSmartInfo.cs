using System;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    // Reads the NVMe SMART/Health Information log page (Log ID 0x02) via
    // IOCTL_STORAGE_QUERY_PROPERTY / StorageDeviceProtocolSpecificProperty.
    // Buffer layout (offsets confirmed against the NVMe spec's Health Info
    // Log structure, cross-checked against open-source kernel headers):
    //   Input (48 bytes): STORAGE_PROPERTY_QUERY (8B) + STORAGE_PROTOCOL_SPECIFIC_DATA (40B)
    //   Output (560 bytes): STORAGE_PROTOCOL_DATA_DESCRIPTOR header (48B) + 512B log page
    //   Log page payload (relative to byte 48 of the output buffer):
    //     +1..2  Temperature (u16, Kelvin)      +3  Available Spare %
    //     +5     Percentage Used %              +32 Data Units Read (128-bit)
    //     +48    Data Units Written (128-bit)   +128 Power On Hours (128-bit)
    //   128-bit fields: only the low 8 bytes are read (ulong) — sufficient
    //   headroom for any realistic drive lifetime.
    public static class DiskSmartInfo
    {
        private static Models.DiskSmartInfo? _cached;

        public static Models.DiskSmartInfo Get()
        {
            if (_cached != null) return _cached;
            var result = new Models.DiskSmartInfo();
            try
            {
                using var handle = NativeMethods.CreateFile(@"\\.\PhysicalDrive0", NativeMethods.GENERIC_READ,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);

                if (!handle.IsInvalid)
                {
                    byte[] input = new byte[48];
                    BitConverter.GetBytes(NativeMethods.StorageDeviceProtocolSpecificProperty).CopyTo(input, 0);
                    BitConverter.GetBytes(NativeMethods.PropertyStandardQuery).CopyTo(input, 4);
                    BitConverter.GetBytes(NativeMethods.ProtocolTypeNvme).CopyTo(input, 8);
                    BitConverter.GetBytes(NativeMethods.NVMeDataTypeLogPage).CopyTo(input, 12);
                    BitConverter.GetBytes(NativeMethods.NVME_LOG_PAGE_HEALTH_INFO).CopyTo(input, 16);
                    BitConverter.GetBytes(0u).CopyTo(input, 20);
                    BitConverter.GetBytes(40u).CopyTo(input, 24);
                    BitConverter.GetBytes(512u).CopyTo(input, 28);

                    byte[] output = new byte[560];
                    bool ok = NativeMethods.DeviceIoControl(handle, NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                        input, (uint)input.Length, output, (uint)output.Length, out _, IntPtr.Zero);

                    if (ok)
                    {
                        const int p = 48; // payload start
                        int tempKelvin = BitConverter.ToUInt16(output, p + 1);
                        result.TemperatureCelsius = tempKelvin > 0 ? tempKelvin - 273 : 0;
                        result.AvailableSparePercent = output[p + 3];
                        result.PercentageUsed = output[p + 5];
                        ulong dataUnitsRead = BitConverter.ToUInt64(output, p + 32);
                        ulong dataUnitsWritten = BitConverter.ToUInt64(output, p + 48);
                        result.DataUnitsReadBytes = dataUnitsRead * 512000UL;
                        result.DataUnitsWrittenBytes = dataUnitsWritten * 512000UL;
                        result.PowerOnHours = BitConverter.ToUInt64(output, p + 128);
                        result.IsAvailable = true;
                    }
                }
            }
            catch { /* non-fatal — non-NVMe drive, permissions, or older driver */ }
            _cached = result;
            return result;
        }
    }
}
