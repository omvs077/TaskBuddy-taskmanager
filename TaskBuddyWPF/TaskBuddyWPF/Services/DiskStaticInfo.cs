using System;
using System.IO;
using System.Management;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    public static class DiskStaticInfo
    {
        private static ulong? _capacityBytes;
        public static ulong CapacityBytes
        {
            get
            {
                if (_capacityBytes == null)
                {
                    _capacityBytes = 0;
                    try
                    {
                        using var searcher = new ManagementObjectSearcher("SELECT Size FROM Win32_DiskDrive WHERE Index=0");
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            _capacityBytes = Convert.ToUInt64(mo["Size"]);
                            break;
                        }
                    }
                    catch { }
                }
                return _capacityBytes.Value;
            }
        }

        private static ulong? _formattedBytes;
        public static ulong FormattedBytes
        {
            get
            {
                if (_formattedBytes == null)
                {
                    _formattedBytes = 0;
                    try
                    {
                        string sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                        string letter = sysDrive.TrimEnd('\\');
                        using var searcher = new ManagementObjectSearcher($"SELECT Size FROM Win32_LogicalDisk WHERE DeviceID='{letter}'");
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            _formattedBytes = Convert.ToUInt64(mo["Size"]);
                            break;
                        }
                    }
                    catch { }
                }
                return _formattedBytes.Value;
            }
        }

        private static bool? _isSystemDisk;
        public static bool IsSystemDisk
        {
            get
            {
                if (_isSystemDisk == null)
                {
                    _isSystemDisk = true; // single-disk assumption; Index=0 is always queried as system disk
                }
                return _isSystemDisk.Value;
            }
        }

        private static bool? _hasPageFile;
        public static bool HasPageFile
        {
            get
            {
                if (_hasPageFile == null)
                {
                    _hasPageFile = false;
                    try
                    {
                        using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PageFileUsage");
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            _hasPageFile = true;
                            break;
                        }
                    }
                    catch { }
                }
                return _hasPageFile.Value;
            }
        }

        private static string? _healthStatus;
        public static string HealthStatus
        {
            get
            {
                if (_healthStatus == null)
                {
                    _healthStatus = "Unknown";
                    try
                    {
                        var scope = new ManagementScope(@"root\Microsoft\Windows\Storage");
                        var query = new ObjectQuery("SELECT HealthStatus FROM MSFT_PhysicalDisk");
                        using var searcher = new ManagementObjectSearcher(scope, query);
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            uint hs = Convert.ToUInt32(mo["HealthStatus"]);
                            _healthStatus = hs switch
                            {
                                0 => "Healthy",
                                1 => "Warning",
                                2 => "Unhealthy",
                                _ => "Unknown"
                            };
                            break;
                        }
                    }
                    catch { }
                }
                return _healthStatus;
            }
        }

        private static string? _diskType;
        public static string DiskType
        {
            get
            {
                if (_diskType == null)
                {
                    _diskType = "Unknown";
                    try
                    {
                        using var handle = NativeMethods.CreateFile(@"\\.\PhysicalDrive0", NativeMethods.GENERIC_READ,
                            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
                        if (!handle.IsInvalid)
                        {
                            var query = new NativeMethods.STORAGE_PROPERTY_QUERY
                            {
                                PropertyId = NativeMethods.StorageDeviceSeekPenaltyProperty,
                                QueryType = NativeMethods.PropertyStandardQuery,
                                AdditionalParameters = new byte[1]
                            };
                            var descriptor = new NativeMethods.DEVICE_SEEK_PENALTY_DESCRIPTOR();
                            uint bytesReturned;
                            uint querySize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(query);
                            uint descSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(descriptor);
                            bool ok = NativeMethods.DeviceIoControl(handle, NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                                ref query, querySize, ref descriptor, descSize, out bytesReturned, IntPtr.Zero);
                            if (ok)
                                _diskType = descriptor.IncursSeekPenalty ? "HDD" : "SSD";
                        }
                    }
                    catch { }
                }
                return _diskType;
            }
        }
    }
}

