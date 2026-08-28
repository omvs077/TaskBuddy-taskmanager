using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    public class ServiceEnumerator
    {
        private const uint SERVICE_RUNNING = 4;
        private readonly Dictionary<string, string> _descriptionCache = new();
        private readonly Dictionary<string, string> _groupCache = new();

        // Icons cached by hosting PID — same reasoning as ProcessEnumerator's icon
        // cache (an exe's icon never changes). Not pruned: a stopped/reused PID
        // showing a stale icon briefly is a minor cosmetic edge case, not worth the
        // extra bookkeeping for a cache this small (one entry per unique host process).
        private readonly Dictionary<uint, ImageSource?> _iconCache = new();

        public List<ServiceInfo> GetSnapshot()
        {
            var results = new List<ServiceInfo>();
            IntPtr scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ENUMERATE_SERVICE);
            if (scm == IntPtr.Zero)
                return results;

            try
            {
                int bufferSize = 64 * 1024;
                IntPtr buffer = IntPtr.Zero;

                try
                {
                    uint resumeHandle = 0;
                    bool more;
                    do
                    {
                        if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                        buffer = Marshal.AllocHGlobal(bufferSize);

                        bool success = NativeMethods.EnumServicesStatusEx(
                            scm, NativeMethods.SC_ENUM_PROCESS_INFO, NativeMethods.SERVICE_WIN32,
                            NativeMethods.SERVICE_STATE_ALL, buffer, (uint)bufferSize,
                            out uint bytesNeeded, out uint servicesReturned, ref resumeHandle, null);

                        if (!success)
                        {
                            int err = Marshal.GetLastWin32Error();
                            if (err == NativeMethods.ERROR_INSUFFICIENT_BUFFER)
                            {
                                bufferSize = (int)bytesNeeded;
                                more = true;
                                continue;
                            }
                            more = false;
                            break;
                        }

                        int structSize = Marshal.SizeOf<ENUM_SERVICE_STATUS_PROCESS>();
                        IntPtr current = buffer;
                        for (int i = 0; i < servicesReturned; i++)
                        {
                            var entry = Marshal.PtrToStructure<ENUM_SERVICE_STATUS_PROCESS>(current);
                            uint pid = entry.ServiceStatusProcess.dwProcessId;
                            results.Add(new ServiceInfo
                            {
                                ServiceName = entry.lpServiceName ?? string.Empty,
                                DisplayName = entry.lpDisplayName ?? string.Empty,
                                Pid = pid,
                                IsRunning = entry.ServiceStatusProcess.dwCurrentState == SERVICE_RUNNING,
                                Description = ResolveDescription(scm, entry.lpServiceName ?? string.Empty),
                                Group = ResolveGroup(scm, entry.lpServiceName ?? string.Empty),
                                Icon = ResolveServiceIcon(pid)
                            });
                            current = IntPtr.Add(current, structSize);
                        }

                        more = false;
                    } while (more);
                }
                finally
                {
                    if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(scm);
            }

            return results;
        }

        private ImageSource? ResolveServiceIcon(uint pid)
        {
            if (pid == 0) return IconHelper.DefaultIcon; // stopped/no host process

            if (_iconCache.TryGetValue(pid, out var cached)) return cached;

            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero)
            {
                var fallback = IconHelper.DefaultIcon;
                _iconCache[pid] = fallback;
                return fallback;
            }

            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (!NativeMethods.QueryFullProcessImageNameW(hProcess, 0, sb, ref size))
                {
                    var fallback = IconHelper.DefaultIcon;
                    _iconCache[pid] = fallback;
                    return fallback;
                }

                var icon = IconHelper.ResolveIcon(sb.ToString());
                _iconCache[pid] = icon;
                return icon;
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        private string ResolveDescription(IntPtr scm, string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName)) return string.Empty;
            if (_descriptionCache.TryGetValue(serviceName, out var cached)) return cached;

            IntPtr hService = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_QUERY_CONFIG);
            if (hService == IntPtr.Zero) return string.Empty;

            try
            {
                uint bufSize = 8192;
                IntPtr buffer = Marshal.AllocHGlobal((int)bufSize);
                try
                {
                    if (NativeMethods.QueryServiceConfig2(hService, NativeMethods.SERVICE_CONFIG_DESCRIPTION, buffer, bufSize, out _))
                    {
                        var desc = Marshal.PtrToStructure<SERVICE_DESCRIPTION>(buffer);
                        string result = desc.lpDescription ?? string.Empty;
                        _descriptionCache[serviceName] = result;
                        return result;
                    }
                    return string.Empty;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(hService);
            }
        }

        private string ResolveGroup(IntPtr scm, string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName)) return string.Empty;
            if (_groupCache.TryGetValue(serviceName, out var cached)) return cached;

            IntPtr hService = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_QUERY_CONFIG);
            if (hService == IntPtr.Zero) return string.Empty;

            try
            {
                uint bufSize = 8192;
                IntPtr buffer = Marshal.AllocHGlobal((int)bufSize);
                try
                {
                    if (NativeMethods.QueryServiceConfig(hService, buffer, bufSize, out _))
                    {
                        var cfg = Marshal.PtrToStructure<QUERY_SERVICE_CONFIGW>(buffer);
                        string result = cfg.lpLoadOrderGroup ?? string.Empty;
                        _groupCache[serviceName] = result;
                        return result;
                    }
                    return string.Empty;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(hService);
            }
        }

        public bool StartServiceByName(string serviceName)
        {
            IntPtr scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ENUMERATE_SERVICE);
            if (scm == IntPtr.Zero) return false;

            try
            {
                IntPtr hService = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_START);
                if (hService == IntPtr.Zero) return false;

                try
                {
                    return NativeMethods.StartService(hService, 0, null);
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(hService);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(scm);
            }
        }

        public bool StopServiceByName(string serviceName)
        {
            IntPtr scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ENUMERATE_SERVICE);
            if (scm == IntPtr.Zero) return false;

            try
            {
                IntPtr hService = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_STOP);
                if (hService == IntPtr.Zero) return false;

                try
                {
                    var status = new SERVICE_STATUS();
                    return NativeMethods.ControlService(hService, NativeMethods.SERVICE_CONTROL_STOP, ref status);
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(hService);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(scm);
            }
        }
    }
}
