using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    public class ProcessEnumerator
    {
        private readonly Dictionary<uint, (long cpuTime, DateTime timestamp)> _cpuCache = new();
        private readonly Dictionary<uint, string> _pathCache = new();
        private readonly Dictionary<uint, (ulong totalBytes, DateTime timestamp)> _ioCache = new();
        private readonly Dictionary<string, ImageSource> _iconCache = new();
        private readonly HashSet<uint> _suspendedByUs = new();
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

                    var (imagePath, diskBytesPerSec, icon) = ResolveProcessDetails(pid, now);

                    results.Add(new ProcessInfo
                    {
                        Pid = pid,
                        ParentPid = ppid,
                        WorkingSetBytes = (ulong)entry.WorkingSetSize.ToUInt64(),
                        CpuTime100ns = cpuTime,
                        CpuPercent = Math.Max(0, cpuPercent),
                        ImageName = imageName ?? string.Empty,
                        ImagePath = imagePath,
                        IsSuspended = _suspendedByUs.Contains(pid),
                        DiskBytesPerSec = diskBytesPerSec,
                        Icon = icon
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

        // Opens one handle per process (PROCESS_QUERY_LIMITED_INFORMATION is sufficient
        // for path resolution + I/O counters, avoiding a second OpenProcess call).
        private (string path, double diskBytesPerSec, ImageSource? icon) ResolveProcessDetails(uint pid, DateTime now)
        {
            string path = _pathCache.TryGetValue(pid, out var cachedPath) ? cachedPath : string.Empty;
            double diskBytesPerSec = 0.0;

            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero)
                return (path, diskBytesPerSec, string.IsNullOrEmpty(path) ? DefaultIcon : ResolveIcon(path));

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    var sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (NativeMethods.QueryFullProcessImageNameW(hProcess, 0, sb, ref size))
                    {
                        path = sb.ToString();
                        _pathCache[pid] = path; // only cache on success
                    }
                }

                ImageSource? icon = string.IsNullOrEmpty(path) ? DefaultIcon : ResolveIcon(path);

                if (NativeMethods.GetProcessIoCounters(hProcess, out var io))
                {
                    ulong totalBytes = io.ReadTransferCount + io.WriteTransferCount;
                    if (_ioCache.TryGetValue(pid, out var prevIo))
                    {
                        double elapsedSeconds = (now - prevIo.timestamp).TotalSeconds;
                        if (elapsedSeconds > 0 && totalBytes >= prevIo.totalBytes)
                            diskBytesPerSec = (totalBytes - prevIo.totalBytes) / elapsedSeconds;
                    }
                    _ioCache[pid] = (totalBytes, now);
                }

                return (path, diskBytesPerSec, icon);
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        // Icon cache keyed by path (not PID) — an exe's icon never changes, so this
        // cache is never pruned; naturally bounded by the number of distinct exe paths seen.
        // Runs on a background thread (see ProcessesPage.RefreshAsync); BitmapSource.Freeze()
        // makes the result safe to hand to the UI thread for binding.
        // Simple generic fallback (gray rounded square) for processes whose icon can't
        // be extracted — drawn in code, no bundled asset needed. Computed once, reused.
        private static ImageSource? _defaultIcon;
        private static ImageSource? DefaultIcon
        {
            get
            {
                if (_defaultIcon == null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri("pack://application:,,,/DefaultProcessIcon.png");
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    _defaultIcon = bitmap;
                }
                return _defaultIcon;
            }
        }

        private ImageSource? ResolveIcon(string path)
        {
            if (_iconCache.TryGetValue(path, out var cached))
                return cached;

            var shfi = new SHFILEINFO();
            IntPtr result = NativeMethods.SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_SMALLICON);
            if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return DefaultIcon;

            try
            {
                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmapSource.Freeze();
                _iconCache[path] = bitmapSource;
                return bitmapSource;
            }
            catch
            {
                return DefaultIcon; // some paths (protected/system) can throw on icon extraction — non-fatal
            }
            finally
            {
                NativeMethods.DestroyIcon(shfi.hIcon);
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

        public bool SuspendProcess(uint pid)
        {
            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_SUSPEND_RESUME, false, pid);
            if (hProcess == IntPtr.Zero)
                return false;

            try
            {
                uint status = NativeMethods.NtSuspendProcess(hProcess);
                if (status == 0)
                {
                    _suspendedByUs.Add(pid);
                    return true;
                }
                return false;
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        public bool ResumeProcess(uint pid)
        {
            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_SUSPEND_RESUME, false, pid);
            if (hProcess == IntPtr.Zero)
                return false;

            try
            {
                uint status = NativeMethods.NtResumeProcess(hProcess);
                if (status == 0)
                {
                    _suspendedByUs.Remove(pid);
                    return true;
                }
                return false;
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
            PruneDict(_ioCache, seenPids);
            _suspendedByUs.RemoveWhere(pid => !seenPids.Contains(pid));
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
