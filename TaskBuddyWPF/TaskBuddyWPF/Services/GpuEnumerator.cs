using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    // Reads GPU utilization and memory usage via the raw PDH "GPU Engine" and
    // "GPU Adapter Memory" counter sets — confirmed via research to be the
    // same source Task Manager itself uses (the alternative WMI class,
    // Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine, is
    // independently reported to read as always-0 on many systems).
    //
    // Each counter instance name encodes which physical adapter it belongs to
    // via a "phys_N" token (e.g. "pid_1234_luid_0x0_0xABCD_phys_0_eng_0_engtype_3D").
    // Adapter DISPLAY NAMES come from WMI (Win32_VideoController), paired with
    // phys_N by list order. This index-pairing is a known limitation for this
    // first pass — Windows generally enumerates adapters in the same order for
    // both, but this isn't guaranteed; a future pass could cross-reference via
    // DXGI adapter LUIDs instead if evidence shows misordering in practice.
    public class GpuEnumerator : IDisposable
    {
        private static readonly Regex PhysRegex = new(@"phys_(\d+)", RegexOptions.Compiled);

        private IntPtr _query = IntPtr.Zero;
        private IntPtr _engineCounter = IntPtr.Zero;
        private IntPtr _dedicatedCounter = IntPtr.Zero;
        private IntPtr _sharedCounter = IntPtr.Zero;
        private bool _initialized;
        private bool _disposed;
        private List<string>? _adapterNames;

        private void EnsureInitialized()
        {
            if (_initialized || _disposed) return;

            if (NativeMethods.PdhOpenQuery(null, IntPtr.Zero, out _query) != 0)
                return;

            NativeMethods.PdhAddEnglishCounter(_query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _engineCounter);
            NativeMethods.PdhAddEnglishCounter(_query, @"\GPU Adapter Memory(*)\Dedicated Usage", IntPtr.Zero, out _dedicatedCounter);
            NativeMethods.PdhAddEnglishCounter(_query, @"\GPU Adapter Memory(*)\Shared Usage", IntPtr.Zero, out _sharedCounter);
            _initialized = true;
        }

        private List<string> GetAdapterNames()
        {
            if (_adapterNames != null) return _adapterNames;
            _adapterNames = new List<string>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"] as string ?? "Unknown GPU";
                    _adapterNames.Add(name);
                }
            }
            catch
            {
                // non-fatal — falls back to "GPU N" labels in GetSnapshot
            }
            return _adapterNames;
        }

        // First call after startup returns an empty list (PDH counters need at
        // least one prior PdhCollectQueryData call before a formatted value is
        // valid), same pattern as DiskPerformanceMonitor.Sample().
        public List<GpuInfo> GetSnapshot()
        {
            EnsureInitialized();
            if (!_initialized) return new List<GpuInfo>();

            if (NativeMethods.PdhCollectQueryData(_query) != 0)
                return new List<GpuInfo>();

            var utilizationByPhys = new Dictionary<int, double>();
            var dedicatedByPhys = new Dictionary<int, ulong>();
            var sharedByPhys = new Dictionary<int, ulong>();

            foreach (var (instanceName, value) in ReadCounterArray(_engineCounter))
            {
                if (!TryGetPhys(instanceName, out int phys)) continue;
                if (!utilizationByPhys.TryGetValue(phys, out double existing) || value > existing)
                    utilizationByPhys[phys] = value; // busiest single engine, matching TM's per-adapter %
            }

            foreach (var (instanceName, value) in ReadCounterArray(_dedicatedCounter))
            {
                if (!TryGetPhys(instanceName, out int phys)) continue;
                dedicatedByPhys[phys] = dedicatedByPhys.GetValueOrDefault(phys) + (ulong)Math.Max(0, value);
            }

            foreach (var (instanceName, value) in ReadCounterArray(_sharedCounter))
            {
                if (!TryGetPhys(instanceName, out int phys)) continue;
                sharedByPhys[phys] = sharedByPhys.GetValueOrDefault(phys) + (ulong)Math.Max(0, value);
            }

            var adapterNames = GetAdapterNames();

            // Always report one entry per WMI-known adapter, not just ones with
            // live PDH counter instances right now — a currently-idle adapter
            // (no active engine workload) has no live instances at all and would
            // otherwise silently disappear, unlike real Task Manager which always
            // shows every detected GPU slot with a 0% default.
            var allPhysIndices = Enumerable.Range(0, Math.Max(adapterNames.Count,
                    utilizationByPhys.Keys.Union(dedicatedByPhys.Keys).Union(sharedByPhys.Keys).DefaultIfEmpty(-1).Max() + 1))
                .ToList();

            var result = new List<GpuInfo>();
            foreach (int phys in allPhysIndices)
            {
                result.Add(new GpuInfo
                {
                    GpuIndex = phys,
                    AdapterName = phys < adapterNames.Count ? adapterNames[phys] : $"GPU {phys}",
                    UtilizationPercent = utilizationByPhys.GetValueOrDefault(phys),
                    DedicatedUsedBytes = dedicatedByPhys.GetValueOrDefault(phys),
                    SharedUsedBytes = sharedByPhys.GetValueOrDefault(phys)
                });
            }
            return result;
        }

        private static bool TryGetPhys(string instanceName, out int phys)
        {
            var match = PhysRegex.Match(instanceName);
            if (match.Success && int.TryParse(match.Groups[1].Value, out phys)) return true;
            phys = -1;
            return false;
        }

        // PdhGetFormattedCounterArrayW two-call pattern: first call with a null
        // buffer to learn the required size (returns PDH_MORE_DATA), then a
        // second call with an allocated buffer of that size.
        private static List<(string instanceName, double value)> ReadCounterArray(IntPtr counter)
        {
            var results = new List<(string, double)>();
            if (counter == IntPtr.Zero) return results;

            int bufferSize = 0;
            int itemCount = 0;
            uint status = NativeMethods.PdhGetFormattedCounterArrayW(counter, NativeMethods.PDH_FMT_DOUBLE, ref bufferSize, out itemCount, IntPtr.Zero);
            if (status != NativeMethods.PDH_MORE_DATA || bufferSize <= 0) return results;

            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                status = NativeMethods.PdhGetFormattedCounterArrayW(counter, NativeMethods.PDH_FMT_DOUBLE, ref bufferSize, out itemCount, buffer);
                if (status != 0) return results;

                int itemSize = Marshal.SizeOf<NativeMethods.PDH_FMT_COUNTERVALUE_ITEM_W>();
                for (int i = 0; i < itemCount; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(buffer, i * itemSize);
                    var item = Marshal.PtrToStructure<NativeMethods.PDH_FMT_COUNTERVALUE_ITEM_W>(itemPtr);
                    if (item.FmtValue.CStatus != 0) continue;
                    string name = Marshal.PtrToStringUni(item.szName) ?? "";
                    results.Add((name, item.FmtValue.doubleValue));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return results;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_query != IntPtr.Zero)
            {
                NativeMethods.PdhCloseQuery(_query);
                _query = IntPtr.Zero;
            }
        }
    }
}

