using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Native;
using System.Windows;

namespace TaskBuddyWPF.Services
{
    // Wraps ProcessEnumerator's snapshot (reusing its working CPU-delta/memory/icon
    // logic) and adds User/Architecture/Virtualization/Description, none of which
    // change during a process's lifetime, so all three are cached by PID and never
    // re-resolved on subsequent ticks — only new PIDs pay the resolution cost.
    public class DetailsEnumerator
    {
        private readonly ProcessEnumerator _processEnumerator = new();
        private readonly Dictionary<uint, string> _architectureCache = new();
        private readonly Dictionary<uint, bool> _virtualizationCache = new();
        private readonly Dictionary<uint, string> _descriptionCache = new();

        public List<ProcessDetailInfo> GetSnapshot()
        {
            var baseSnapshot = _processEnumerator.GetSnapshot();
            var seenPids = new HashSet<uint>();
            var result = new List<ProcessDetailInfo>();

            foreach (var proc in baseSnapshot)
            {
                seenPids.Add(proc.Pid);

                if (!_architectureCache.TryGetValue(proc.Pid, out var arch))
                {
                    arch = ResolveArchitecture(proc.Pid);
                    _architectureCache[proc.Pid] = arch;
                }

                if (!_virtualizationCache.TryGetValue(proc.Pid, out var virtualization))
                {
                    virtualization = ResolveVirtualization(proc.Pid);
                    _virtualizationCache[proc.Pid] = virtualization;
                }

                if (!_descriptionCache.TryGetValue(proc.Pid, out var description))
                {
                    description = ResolveDescription(proc.ImagePath);
                    _descriptionCache[proc.Pid] = description;
                }

                result.Add(new ProcessDetailInfo
                {
                    Pid = proc.Pid,
                    Name = proc.ImageName,
                    Icon = proc.Icon,
                    UserName = UserHelper.ResolveOwner(proc.Pid) ?? "SYSTEM / Protected (access denied)",
                    Architecture = arch,
                    IsVirtualized = virtualization,
                    Description = description,
                    Status = proc.StatusText,
                    CpuPercent = proc.CpuPercent,
                    MemoryBytes = proc.WorkingSetBytes,
                    ImagePath = proc.ImagePath,
                    IsSuspended = proc.IsSuspended,
                    IsEfficiencyMode = proc.IsEfficiencyMode
                });
            }

            PruneStale(_architectureCache, seenPids);
            PruneStale(_virtualizationCache, seenPids);
            PruneStale(_descriptionCache, seenPids);

            return result;
        }

        private static void PruneStale<T>(Dictionary<uint, T> dict, HashSet<uint> seenPids)
        {
            var stale = dict.Keys.Where(pid => !seenPids.Contains(pid)).ToList();
            foreach (var pid in stale) dict.Remove(pid);
        }

        // Native processes: pProcessMachine == IMAGE_FILE_MACHINE_UNKNOWN, so the
        // actual architecture comes from pNativeMachine. WOW64 processes: pProcessMachine
        // directly identifies the emulated architecture.
        private static string ResolveArchitecture(uint pid)
        {
            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero) return "Unknown";

            try
            {
                if (!NativeMethods.IsWow64Process2(hProcess, out ushort processMachine, out ushort nativeMachine))
                    return "Unknown";

                ushort effective = processMachine == NativeMethods.IMAGE_FILE_MACHINE_UNKNOWN ? nativeMachine : processMachine;
                return effective switch
                {
                    NativeMethods.IMAGE_FILE_MACHINE_AMD64 => "x64",
                    NativeMethods.IMAGE_FILE_MACHINE_I386 => "x86",
                    NativeMethods.IMAGE_FILE_MACHINE_ARM64 => "ARM64",
                    NativeMethods.IMAGE_FILE_MACHINE_ARM => "ARM",
                    _ => "Unknown"
                };
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        private static bool ResolveVirtualization(uint pid)
        {
            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero) return false;

            try
            {
                if (!NativeMethods.OpenProcessToken(hProcess, NativeMethods.TOKEN_QUERY, out IntPtr hToken))
                    return false;

                try
                {
                    IntPtr buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
                    try
                    {
                        if (!NativeMethods.GetTokenInformation(hToken, NativeMethods.TokenVirtualizationEnabled, buffer, 4, out _))
                            return false;
                        return System.Runtime.InteropServices.Marshal.ReadInt32(buffer) != 0;
                    }
                    finally
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
                    }
                }
                finally
                {
                    NativeMethods.CloseHandle(hToken);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        // Reads FileDescription from the exe's version resource via the stdlib
        // (System.Diagnostics.FileVersionInfo) rather than native version-info APIs —
        // matches the Anti-Overengineering Ladder's Stdlib rung.
        private static string ResolveDescription(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                return info.FileDescription ?? "";
            }
            catch
            {
                return ""; // missing file, no version resource, access denied — all non-fatal
            }
        }

        public bool TerminateProcess(uint pid) => _processEnumerator.TerminateProcess(pid);

        public Task ToggleSuspend(uint pid, string imageName, bool isSuspended, Func<Task> refresh) =>
            ProcessActions.ToggleSuspend(_processEnumerator, pid, imageName, isSuspended, refresh);

        public Task ToggleEfficiencyMode(uint pid, string imageName, bool isEfficiencyMode, Func<Task> refresh) =>
            ProcessActions.ToggleEfficiencyMode(_processEnumerator, pid, imageName, isEfficiencyMode, refresh);

        public Task EndTask(uint pid, string imageName, Func<Task> refresh) =>
            ProcessActions.EndTask(_processEnumerator, pid, imageName, refresh);

        public Task EndProcessTree(uint pid, string imageName, Func<Task> refresh) =>
            ProcessActions.EndProcessTree(_processEnumerator, pid, imageName, refresh);

        public Task CreateDumpFile(uint pid, string imageName) =>
            ProcessActions.CreateDumpFile(_processEnumerator, pid, imageName);

        public Task SetPriority(uint pid, string imageName, uint priorityClass, Func<Task> refresh) =>
            ProcessActions.SetPriority(_processEnumerator, pid, imageName, priorityClass, refresh);

        public Task SetAffinity(uint pid, string imageName, Window owner) =>
            ProcessActions.SetAffinity(_processEnumerator, pid, imageName, owner);
    }
}



