using System;
using System.Runtime.InteropServices;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    public static class SystemInfo
    {
        private static ulong? _totalPhysicalMemoryBytes;

        public static ulong TotalPhysicalMemoryBytes
        {
            get
            {
                if (_totalPhysicalMemoryBytes == null)
                {
                    var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                    _totalPhysicalMemoryBytes = NativeMethods.GlobalMemoryStatusEx(ref status) ? status.ullTotalPhys : 1UL;
                }
                return _totalPhysicalMemoryBytes.Value;
            }
        }

        private static string? _processorName;
        public static string ProcessorName
        {
            get
            {
                if (_processorName == null)
                {
                    try
                    {
                        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                        _processorName = (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "Unknown Processor";
                    }
                    catch
                    {
                        _processorName = "Unknown Processor";
                    }
                }
                return _processorName;
            }
        }

        public static int LogicalCoreCount => Environment.ProcessorCount;

        private static int? _physicalCoreCount;
        public static int PhysicalCoreCount
        {
            get
            {
                if (_physicalCoreCount == null)
                {
                    _physicalCoreCount = CountPhysicalCores();
                }
                return _physicalCoreCount.Value;
            }
        }

        private static int CountPhysicalCores()
        {
            uint len = 0;
            NativeMethods.GetLogicalProcessorInformationEx(NativeMethods.RelationProcessorCore, IntPtr.Zero, ref len);
            if (len == 0) return Environment.ProcessorCount;

            IntPtr buffer = Marshal.AllocHGlobal((int)len);
            try
            {
                if (!NativeMethods.GetLogicalProcessorInformationEx(NativeMethods.RelationProcessorCore, buffer, ref len))
                    return Environment.ProcessorCount;

                int count = 0;
                IntPtr current = buffer;
                uint bytesRead = 0;
                while (bytesRead < len)
                {
                    var header = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX_HEADER>(current);
                    if (header.Relationship == NativeMethods.RelationProcessorCore) count++;
                    current = IntPtr.Add(current, header.Size);
                    bytesRead += (uint)header.Size;
                }
                return count > 0 ? count : Environment.ProcessorCount;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static string UptimeString
        {
            get
            {
                var ts = TimeSpan.FromMilliseconds(NativeMethods.GetTickCount64());
                return $"{(int)ts.TotalDays}d:{ts.Hours:D2}h:{ts.Minutes:D2}m:{ts.Seconds:D2}s";
            }
        }

        private static int? _socketCount;
        public static int SocketCount
        {
            get
            {
                _socketCount ??= CountRelationEntries(NativeMethods.RelationProcessorPackage);
                return _socketCount.Value;
            }
        }

        // "~MHz" under the CentralProcessor registry key — the same static,
        // BIOS-reported nominal speed Task Manager itself displays as
        // "Base speed" (not a live/current frequency reading).
        private static double? _baseSpeedGhz;
        public static double BaseSpeedGhz
        {
            get
            {
                if (_baseSpeedGhz == null)
                {
                    try
                    {
                        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                        var mhz = key?.GetValue("~MHz");
                        _baseSpeedGhz = mhz != null ? Convert.ToInt32(mhz) / 1000.0 : 0;
                    }
                    catch
                    {
                        _baseSpeedGhz = 0;
                    }
                }
                return _baseSpeedGhz.Value;
            }
        }

        private static bool? _virtualizationEnabled;
        public static bool VirtualizationEnabled
        {
            get
            {
                if (_virtualizationEnabled == null)
                {
                    try
                    {
                        using var searcher = new System.Management.ManagementObjectSearcher(
                            "SELECT VirtualizationFirmwareEnabled FROM Win32_Processor");
                        _virtualizationEnabled = false;
                        foreach (System.Management.ManagementObject obj in searcher.Get())
                        {
                            if (obj["VirtualizationFirmwareEnabled"] is bool v && v)
                            {
                                _virtualizationEnabled = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        _virtualizationEnabled = false;
                    }
                }
                return _virtualizationEnabled.Value;
            }
        }

        private static (ulong l1, ulong l2, ulong l3)? _cacheSizes;
        public static (ulong L1Bytes, ulong L2Bytes, ulong L3Bytes) CacheSizes
        {
            get
            {
                _cacheSizes ??= ComputeCacheSizes();
                return _cacheSizes.Value;
            }
        }

        private static int CountRelationEntries(int relationshipType)
        {
            uint len = 0;
            NativeMethods.GetLogicalProcessorInformationEx(relationshipType, IntPtr.Zero, ref len);
            if (len == 0) return 1;

            IntPtr buffer = Marshal.AllocHGlobal((int)len);
            try
            {
                if (!NativeMethods.GetLogicalProcessorInformationEx(relationshipType, buffer, ref len))
                    return 1;

                int count = 0;
                IntPtr current = buffer;
                uint bytesRead = 0;
                while (bytesRead < len)
                {
                    var header = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX_HEADER>(current);
                    if (header.Relationship == relationshipType) count++;
                    current = IntPtr.Add(current, header.Size);
                    bytesRead += (uint)header.Size;
                }
                return count > 0 ? count : 1;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // Sums CacheSize across every distinct cache instance Windows reports at
        // each level — this matches Task Manager's displayed cache totals (each
        // physically distinct cache the OS enumerates is summed once, not
        // duplicated per logical processor sharing it).
        private static (ulong, ulong, ulong) ComputeCacheSizes()
        {
            uint len = 0;
            NativeMethods.GetLogicalProcessorInformationEx(NativeMethods.RelationCache, IntPtr.Zero, ref len);
            if (len == 0) return (0, 0, 0);

            IntPtr buffer = Marshal.AllocHGlobal((int)len);
            try
            {
                if (!NativeMethods.GetLogicalProcessorInformationEx(NativeMethods.RelationCache, buffer, ref len))
                    return (0, 0, 0);

                ulong l1 = 0, l2 = 0, l3 = 0;
                IntPtr current = buffer;
                uint bytesRead = 0;
                while (bytesRead < len)
                {
                    var header = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX_HEADER>(current);
                    if (header.Relationship == NativeMethods.RelationCache)
                    {
                        var cache = Marshal.PtrToStructure<CACHE_RELATIONSHIP_MINIMAL>(IntPtr.Add(current, 8));
                        switch (cache.Level)
                        {
                            case 1: l1 += cache.CacheSize; break;
                            case 2: l2 += cache.CacheSize; break;
                            case 3: l3 += cache.CacheSize; break;
                        }
                    }
                    current = IntPtr.Add(current, header.Size);
                    bytesRead += (uint)header.Size;
                }
                return (l1, l2, l3);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}



