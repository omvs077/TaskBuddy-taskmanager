using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    public class ServiceEnumerator
    {
        private const uint SERVICE_RUNNING = 4;
        private readonly Dictionary<string, string> _descriptionCache = new();

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
                            results.Add(new ServiceInfo
                            {
                                ServiceName = entry.lpServiceName ?? string.Empty,
                                DisplayName = entry.lpDisplayName ?? string.Empty,
                                Pid = entry.ServiceStatusProcess.dwProcessId,
                                IsRunning = entry.ServiceStatusProcess.dwCurrentState == SERVICE_RUNNING,
                                Description = ResolveDescription(scm, entry.lpServiceName ?? string.Empty)
                            });
                            current = IntPtr.Add(current, structSize);
                        }

                        more = false; // resumeHandle stays 0 when the whole DB fit in one call
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

        // Descriptions never change at runtime — cached forever by service name,
        // same reasoning as ProcessEnumerator's icon cache.
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
