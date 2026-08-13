using System;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    public class DiskPerformanceMonitor : IDisposable
    {
        private IntPtr _query = IntPtr.Zero;
        private IntPtr _idleCounter = IntPtr.Zero;
        private IntPtr _readCounter = IntPtr.Zero;
        private IntPtr _writeCounter = IntPtr.Zero;
        private bool _initialized;
        private bool _disposed;

        private void EnsureInitialized()
        {
            if (_initialized || _disposed) return;

            if (NativeMethods.PdhOpenQuery(null, IntPtr.Zero, out _query) != 0)
                return;

            NativeMethods.PdhAddEnglishCounter(_query, @"\PhysicalDisk(_Total)\% Idle Time", IntPtr.Zero, out _idleCounter);
            NativeMethods.PdhAddEnglishCounter(_query, @"\PhysicalDisk(_Total)\Disk Read Bytes/sec", IntPtr.Zero, out _readCounter);
            NativeMethods.PdhAddEnglishCounter(_query, @"\PhysicalDisk(_Total)\Disk Write Bytes/sec", IntPtr.Zero, out _writeCounter);
            _initialized = true;
        }

        // % Idle Time and the byte-rate counters both require at least one prior
        // PdhCollectQueryData call before a formatted value is valid — the first
        // Sample() after startup will report zeros (CStatus != 0), same pattern
        // as SystemPerformanceMonitor's first-call CPU% behavior.
        public (double activeTimePercent, double readBytesPerSec, double writeBytesPerSec) Sample()
        {
            EnsureInitialized();
            if (!_initialized) return (0, 0, 0);

            if (NativeMethods.PdhCollectQueryData(_query) != 0)
                return (0, 0, 0);

            double idle = ReadValue(_idleCounter);
            double read = ReadValue(_readCounter);
            double write = ReadValue(_writeCounter);

            double activeTime = Math.Clamp(100.0 - idle, 0, 100);
            return (activeTime, read, write);
        }

        private double ReadValue(IntPtr counter)
        {
            if (counter == IntPtr.Zero) return 0;
            uint status = NativeMethods.PdhGetFormattedCounterValue(counter, NativeMethods.PDH_FMT_DOUBLE, IntPtr.Zero, out var value);
            return (status == 0 && value.CStatus == 0) ? value.doubleValue : 0;
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
