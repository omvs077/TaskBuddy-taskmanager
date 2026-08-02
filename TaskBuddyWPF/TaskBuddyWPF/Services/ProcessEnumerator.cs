using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    public class ProcessEnumerator
    {
        private readonly Dictionary<uint, (long cpuTime, DateTime timestamp)> _cpuCache = new();
        private readonly Dictionary<uint, string> _pathCache = new();
        private readonly int _coreCount = Environment.ProcessorCount;

        public List<ProcessInfo> GetSnapshot()
        {
            int bufferSize = 64 * 1024;
            IntPtr buffer = IntPtr.Zero;
            var results = new List<ProcessInfo>();
            var seenPids = new HashSet<uint>();

            try
            {
                uint status;
                int returnLength;
                do
                {
                    if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                    buffer = Marshal.AllocHGlobal(bufferSize);
                    status = NativeMethods.NtQuerySystemInformation(
                        NativeMethods.SystemProcessInformation, buffer, bufferSize, out returnLength);

                    if (status == NativeMethods.STATUS_INFO_LENGTH_MISMATCH)
                        bufferSize *= 2;
                } while (status == NativeMethods.STATUS_INFO_LENGTH_MISMATCH);

                if (status != 0)
                    throw new InvalidOperationException($"NtQuerySystemInformation failed: 0x{status:X8}");

                IntPtr current = buffer;
                var now = DateTime.UtcNow;

                while (true)
                {
                    var entry = Marshal.PtrToStructure<SYSTEM_PROCESS_INFORMATION>(current);
                    uint pid = (uint)entry.UniqueProcessId.ToInt64();
                    uint ppid = (uint)entry.InheritedFromUniqueProcessId.ToInt64();
                    seenPids.Add(pid);

                    string imageName = entry.ImageName.Buffer != IntPtr.Zero
                        ? Marshal.PtrToStringUni(entry.ImageName.Buffer, entry.ImageName.Length / 2)
                        : "System Idle Process";

                    long cpuTime = entry.UserTime + entry.KernelTime;
                    double cpuPercent = 0.0;

                    if (_cpuCache.TryGetValue(pid, out var prev))
                    {
                        long deltaCpu = cpuTime - prev.cpuTime;
                        double elapsedTicks = (now - prev.timestamp).Ticks;
                        if (elapsedTicks > 0)
                            cpuPercent = (deltaCpu / elapsedTicks) / _coreCount * 100.0;
                    }
                    _cpuCache[pid] = (cpuTime, now);

                    if (pid == 0)
                    {
                        if (entry.NextEntryOffset == 0) break;
                        current = IntPtr.Add(current, (int)entry.NextEntryOffset);
                        continue;
                    }

                    string imagePath = ResolveImagePath(pid);

                    results.Add(new ProcessInfo
                    {
                        Pid = pid,
                        ParentPid = ppid,
                        WorkingSetBytes = (ulong)entry.WorkingSetSize.ToUInt64(),
                        CpuTime100ns = cpuTime,
                        CpuPercent = Math.Max(0, cpuPercent),
                        ImageName = imageName ?? string.Empty,
                        ImagePath = imagePath
                    });

                    if (entry.NextEntryOffset == 0) break;
                    current = IntPtr.Add(current, (int)entry.NextEntryOffset);
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }

            PruneStale(seenPids);
            return results;
        }

        private string ResolveImagePath(uint pid)
        {
            if (_pathCache.TryGetValue(pid, out var cached))
                return cached;

            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero)
                return string.Empty; // do NOT cache failures — retry next cycle

            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (NativeMethods.QueryFullProcessImageNameW(hProcess, 0, sb, ref size))
                {
                    string path = sb.ToString();
                    _pathCache[pid] = path; // only cache on success
                    return path;
                }
                return string.Empty;
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        public bool TerminateProcess(uint pid)
        {
            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_TERMINATE, false, pid);
            if (hProcess == IntPtr.Zero)
                return false;

            try
            {
                return NativeMethods.TerminateProcess(hProcess, 1);
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        private void PruneStale(HashSet<uint> seenPids)
        {
            PruneDict(_cpuCache, seenPids);
            PruneDict(_pathCache, seenPids);
        }

        private static void PruneDict<T>(Dictionary<uint, T> dict, HashSet<uint> seenPids)
        {
            List<uint>? stale = null;
            foreach (var key in dict.Keys)
            {
                if (!seenPids.Contains(key))
                    (stale ??= new List<uint>()).Add(key);
            }
            if (stale != null)
                foreach (var key in stale)
                    dict.Remove(key);
        }
    }
}
