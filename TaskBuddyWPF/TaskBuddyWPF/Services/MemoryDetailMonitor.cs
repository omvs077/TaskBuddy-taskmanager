using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    // Reads per-tick memory stats from GetPerformanceInfo (documented psapi.dll)
    // and one-time hardware info from WMI Win32_PhysicalMemory.
    public class MemoryDetailMonitor
    {
        private MemoryDetailInfo? _cachedHardwareInfo;

        public MemoryDetailInfo GetSnapshot(ulong memUsedBytes, ulong memTotalBytes)
        {
            var info = new MemoryDetailInfo
            {
                InUseBytes = memUsedBytes,
                TotalBytes = memTotalBytes,
                AvailableBytes = memTotalBytes > memUsedBytes ? memTotalBytes - memUsedBytes : 0
            };

            if (NativeMethods.GetPerformanceInfo(out var perf, (uint)Marshal.SizeOf<NativeMethods.PERFORMANCE_INFORMATION>()))
            {
                ulong pageSize = perf.PageSize.ToUInt64();
                info.CommittedBytes = perf.CommitTotal.ToUInt64() * pageSize;
                info.CachedBytes = perf.SystemCache.ToUInt64() * pageSize;
                info.PagedPoolBytes = perf.KernelPaged.ToUInt64() * pageSize;
                info.NonPagedPoolBytes = perf.KernelNonPaged.ToUInt64() * pageSize;
            }

            // Hardware-reserved = reported total (from BIOS/firmware) minus
            // actual usable total. Estimated using GetPerformanceInfo's
            // PhysicalTotal (usable pages) vs SystemInfo.TotalPhysicalMemoryBytes.
            // Per-tick computation since TotalPhysicalMemoryBytes is cached.
            if (NativeMethods.GetPerformanceInfo(out var perf2, (uint)Marshal.SizeOf<NativeMethods.PERFORMANCE_INFORMATION>()))
            {
                ulong usableBytes = perf2.PhysicalTotal.ToUInt64() * perf2.PageSize.ToUInt64();
                info.HardwareReservedBytes = memTotalBytes > usableBytes ? memTotalBytes - usableBytes : 0;
            }

            try
            {
                // Compressed memory: WMI PerfOS counter, updated each tick.
                using var searcher = new ManagementObjectSearcher(
                    "SELECT CompressedBytes FROM Win32_PerfFormattedData_PerfOS_Memory");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["CompressedBytes"] is ulong cb) { info.CompressedBytes = cb; break; }
                    if (obj["CompressedBytes"] is long lcb && lcb >= 0) { info.CompressedBytes = (ulong)lcb; break; }
                }
            }
            catch { /* non-fatal — compressed bytes stays 0 */ }

            // Hardware info: cached after first resolution (doesn't change).
            var hw = GetHardwareInfo();
            info.SpeedMhz = hw.SpeedMhz;
            info.SlotsUsed = hw.SlotsUsed;
            info.FormFactor = hw.FormFactor;

            return info;
        }

        private MemoryDetailInfo GetHardwareInfo()
        {
            if (_cachedHardwareInfo != null) return _cachedHardwareInfo;
            var result = new MemoryDetailInfo();
            try
            {
                var formFactorMap = new Dictionary<ushort, string>
                {
                    { 0, "Unknown" }, { 8, "DIMM" }, { 12, "SO-DIMM" },
                    { 13, "RIMM" }, { 14, "DIMM" }, { 15, "FB-DIMM" }
                };

                using var searcher = new ManagementObjectSearcher(
                    "SELECT Speed, FormFactor FROM Win32_PhysicalMemory");
                int count = 0;
                uint speed = 0;
                ushort ff = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    count++;
                    if (obj["Speed"] is uint s && s > speed) speed = s;
                    if (obj["FormFactor"] is ushort f) ff = f;
                }
                result.SpeedMhz = speed;
                result.SlotsUsed = count;
                result.FormFactor = formFactorMap.TryGetValue(ff, out var ffStr) ? ffStr : "Unknown";

                using var arraySearcher = new ManagementObjectSearcher(
                    "SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
                foreach (ManagementObject arr in arraySearcher.Get())
                {
                    if (arr["MemoryDevices"] is ushort total)
                        result.TotalSlots = total;
                    break;
                }
            }
            catch { result.FormFactor = "Unknown"; }
            _cachedHardwareInfo = result;
            return result;
        }
    }
}

