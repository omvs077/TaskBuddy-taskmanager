using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    public class SystemPerformanceMonitor
    {
        private ulong _prevIdle, _prevKernel, _prevUser;
        private bool _hasPrev;

        // GetSystemTimes: kernelTime INCLUDES idle time (documented Win32 behavior).
        // total = kernel_delta + user_delta; busy = total - idle_delta; cpu% = busy/total.
        public double GetCpuPercent()
        {
            if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
                return 0;

            ulong idleVal = ToUInt64(idle);
            ulong kernelVal = ToUInt64(kernel);
            ulong userVal = ToUInt64(user);

            if (!_hasPrev)
            {
                _prevIdle = idleVal; _prevKernel = kernelVal; _prevUser = userVal;
                _hasPrev = true;
                return 0;
            }

            ulong sys = (kernelVal - _prevKernel) + (userVal - _prevUser);
            ulong idleDelta = idleVal - _prevIdle;

            _prevIdle = idleVal; _prevKernel = kernelVal; _prevUser = userVal;

            if (sys == 0) return 0;
            double busy = sys - idleDelta;
            return Math.Clamp(busy / (double)sys * 100.0, 0, 100);
        }

        public (ulong usedBytes, ulong totalBytes) GetMemoryUsage()
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (!NativeMethods.GlobalMemoryStatusEx(ref status))
                return (0, SystemInfo.TotalPhysicalMemoryBytes);

            ulong used = status.ullTotalPhys - status.ullAvailPhys;
            return (used, status.ullTotalPhys);
        }

        private static ulong ToUInt64(FILETIME ft) => ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }
}
