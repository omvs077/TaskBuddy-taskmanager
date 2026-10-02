using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace TaskBuddyWPF.Services
{
    // Singleton: only one kernel ETW trace session ("NT Kernel Logger") can
    // exist system-wide, so this must not be instantiated per-page like
    // GpuEnumerator/DiskPerformanceMonitor. ETW delivers discrete per-packet
    // send/receive events (not a queryable cumulative total like
    // GetProcessIoCounters), so bytes are accumulated here via thread-safe
    // counters and drained by the poller via GetAndResetDeltas().
    public sealed class NetworkTraceMonitor : IDisposable
    {
        private static readonly Lazy<NetworkTraceMonitor> _instance = new(() => new NetworkTraceMonitor());
        public static NetworkTraceMonitor Instance => _instance.Value;

        private class Counter { public long BytesSent; public long BytesReceived; }
        private readonly ConcurrentDictionary<int, Counter> _countersByPid = new();
        private TraceEventSession? _session;
        private readonly object _startLock = new();
        private bool _started;

        private NetworkTraceMonitor() { }

        // Safe to call repeatedly / from multiple pages — starts the session
        // only once. If it fails (not elevated, or another tool already owns
        // the kernel session), the Network column will just show no data
        // rather than throwing into the UI.
        public void EnsureStarted()
        {
            if (_started) return;
            lock (_startLock)
            {
                if (_started) return;
                try
                {
                    _session = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
                    _session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

                    _session.Source.Kernel.TcpIpSend += data => Record(data.ProcessID, data.size, isSend: true);
                    _session.Source.Kernel.TcpIpRecv += data => Record(data.ProcessID, data.size, isSend: false);

                    var thread = new Thread(() =>
                    {
                        try { _session.Source.Process(); } catch { /* session stopped or errored — non-fatal */ }
                    })
                    { IsBackground = true, Name = "NetworkTraceMonitor" };
                    thread.Start();
                    _started = true;
                }
                catch
                {
                    _session = null;
                }
            }
        }

        private void Record(int pid, int size, bool isSend)
        {
            if (pid <= 0 || size <= 0) return;
            var counter = _countersByPid.GetOrAdd(pid, _ => new Counter());
            if (isSend) Interlocked.Add(ref counter.BytesSent, size);
            else Interlocked.Add(ref counter.BytesReceived, size);
        }

        // Atomically reads and clears every PID's accumulated bytes since the
        // last call, so the poller can divide by elapsed time for a rate.
        public Dictionary<int, (long sent, long received)> GetAndResetDeltas()
        {
            var result = new Dictionary<int, (long, long)>();
            foreach (var kvp in _countersByPid)
            {
                long sent = Interlocked.Exchange(ref kvp.Value.BytesSent, 0);
                long received = Interlocked.Exchange(ref kvp.Value.BytesReceived, 0);
                if (sent > 0 || received > 0)
                    result[kvp.Key] = (sent, received);
            }
            return result;
        }

        public void Dispose()
        {
            try { _session?.Stop(); } catch { }
            try { _session?.Dispose(); } catch { }
        }
    }
}
