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
    }
}
