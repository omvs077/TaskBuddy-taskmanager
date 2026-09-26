using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Pages
{
    public enum PerformanceResource { Cpu, Memory, Disk, Wifi, Gpu0, Gpu1, Battery }

    public partial class PerformancePage : Page
    {
        private const int MaxSamples = 120; // 60s of history at 500ms sampling

        private readonly SystemPerformanceMonitor _sysMonitor = new();
        private readonly DiskPerformanceMonitor _diskMonitor = new();
        private readonly WifiEnumerator _wifiEnumerator = new();
        private readonly GpuEnumerator _gpuEnumerator = new();
        private readonly BatteryEnumerator _batteryEnumerator = new();
        private readonly TaskBuddyWPF.Services.MemoryDetailMonitor _memDetailMonitor = new();
        private readonly DispatcherTimer _timer;
        private int _processCountCache;
        private DateTime _processCountLastUpdated = DateTime.MinValue;
        private bool _isSampling;

        private readonly Queue<double> _cpuHistory = new();
        private readonly Queue<double> _memHistory = new();
        private readonly Queue<double> _diskActiveHistory = new();
        private readonly Queue<double> _wifiReceiveHistory = new();
        private readonly Queue<double> _wifiSendHistory = new();
        private readonly Queue<double> _batteryHistory = new();
        private readonly Queue<double> _gpu0History = new();
        private readonly Queue<double> _gpu1History = new();

        private ulong _lastMemUsed, _lastMemTotal;
        private double _lastDiskActive, _lastDiskRead, _lastDiskWrite, _lastDiskResponse;
        private TaskBuddyWPF.Models.WifiInfo? _lastWifi;
        private TaskBuddyWPF.Models.GpuInfo? _lastGpu0;
        private TaskBuddyWPF.Models.GpuInfo? _lastGpu1;
        private TaskBuddyWPF.Models.BatteryInfo? _lastBattery;
        private TaskBuddyWPF.Models.MemoryDetailInfo? _lastMemDetail;

        private PerformanceResource _selected = PerformanceResource.Cpu;

        public PerformancePage()
        {
            InitializeComponent();
            SizeChanged += (s, e) => UpdatePinnedHeight();

            CpuMiniGraph.SetHeaderVisible(false);
            MemoryMiniGraph.SetHeaderVisible(false);
            DiskMiniGraph.SetHeaderVisible(false);
            WifiMiniGraph.SetHeaderVisible(false);
            Gpu0MiniGraph.SetHeaderVisible(false);
            Gpu1MiniGraph.SetHeaderVisible(false);
            DetailGraph.SetHeaderVisible(false);
            CpuMiniGraph.AccentColor = System.Windows.Media.Color.FromRgb(90, 170, 255);   // blue
            MemoryMiniGraph.AccentColor = System.Windows.Media.Color.FromRgb(170, 120, 255); // purple
            DiskMiniGraph.AccentColor = System.Windows.Media.Color.FromRgb(90, 220, 140);   // green
            WifiMiniGraph.AccentColor = System.Windows.Media.Color.FromRgb(90, 170, 255);  // blue (Receive)
            Gpu0MiniGraph.AccentColor = System.Windows.Media.Color.FromRgb(255, 165, 0);   // orange
            Gpu1MiniGraph.AccentColor = System.Windows.Media.Color.FromRgb(90, 220, 140);  // green

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) }; // dedicated rate for smooth graphs, independent of Settings refresh speed
            _timer.Tick += async (s, e) => await SampleAsync();
            _timer.Start();

            _ = SampleAsync();
            HighlightSelected();
        }

        private async Task SampleAsync()
        {
            if (_isSampling) return;
            _isSampling = true;

            try
            {
                var (cpu, memUsed, memTotal, diskActive, diskRead, diskWrite, diskResponse, wifi, gpus, battery) = await Task.Run(() =>
                {
                    double c = _sysMonitor.GetCpuPercent();
                    var (used, total) = _sysMonitor.GetMemoryUsage();
                    var (active, read, write, response) = _diskMonitor.Sample();
                    var w = _wifiEnumerator.GetSnapshot();
                    var g = _gpuEnumerator.GetSnapshot();
                    var batt = _batteryEnumerator.GetSnapshot();
                    return (c, used, total, active, read, write, response, w, g, batt);
                });

                _lastMemUsed = memUsed;
                _lastMemTotal = memTotal;
                _lastDiskActive = diskActive;
                _lastDiskRead = diskRead;
                _lastDiskWrite = diskWrite;
                _lastDiskResponse = diskResponse;
                _lastWifi = wifi;
                _lastGpu0 = gpus.Count > 0 ? gpus[0] : null;
                _lastGpu1 = gpus.Count > 1 ? gpus[1] : null;
                _lastBattery = battery;
                _lastMemDetail = _memDetailMonitor.GetSnapshot(memUsed, memTotal);

                Enqueue(_cpuHistory, cpu);
                Enqueue(_memHistory, memTotal > 0 ? memUsed / (double)memTotal * 100.0 : 0);
                Enqueue(_diskActiveHistory, diskActive);
                Enqueue(_wifiReceiveHistory, wifi.ReceiveKbps);
                Enqueue(_wifiSendHistory, wifi.SendKbps);
                if (_lastGpu0 != null) Enqueue(_gpu0History, _lastGpu0.UtilizationPercent);
                if (_lastGpu1 != null) Enqueue(_gpu1History, _lastGpu1.UtilizationPercent);

                double memGb = memUsed / 1024.0 / 1024.0 / 1024.0;
                double totalGb = memTotal / 1024.0 / 1024.0 / 1024.0;

                CpuMiniGraph.SetData(_cpuHistory.ToArray(), 100);
                CpuMiniValue.Text = $"{cpu:F0}%";

                MemoryMiniGraph.SetData(_memHistory.ToArray(), 100);
                MemoryMiniValue.Text = $"{memGb:F1}/{totalGb:F1} GB";

                DiskMiniGraph.SetData(_diskActiveHistory.ToArray(), 100);
                DiskMiniValue.Text = $"{diskActive:F0}%";

                double wifiMaxScale = Math.Max(100, Math.Max(Max(_wifiReceiveHistory), Max(_wifiSendHistory)) * 1.2);
                WifiMiniGraph.SetData(_wifiReceiveHistory.ToArray(), wifiMaxScale);
                WifiMiniGraph.SetSecondSeries(_wifiSendHistory.ToArray(), Colors.Orange);
                WifiMiniValue.Text = wifi.IsConnected ? $"{wifi.ReceiveKbps:F0} Kbps" : "Not connected";

                Gpu0MiniGraph.SetData(_gpu0History.ToArray(), 100);
                Gpu0MiniValue.Text = _lastGpu0 != null ? $"{_lastGpu0.UtilizationPercent:F0}%" : "—";

                Gpu1MiniGraph.SetData(_gpu1History.ToArray(), 100);
                Gpu1MiniValue.Text = _lastGpu1 != null ? $"{_lastGpu1.UtilizationPercent:F0}%" : "—";

                if (battery.IsPresent) Enqueue(_batteryHistory, battery.ChargePercent);
                BatteryMiniGraph.SetData(_batteryHistory.ToArray(), 100);
                BatteryMiniValue.Text = battery.IsPresent ? $"{battery.ChargePercent}%" : "No battery";

                RefreshDetail();
            }
            finally
            {
                _isSampling = false;
            }
        }

        private static void Enqueue(Queue<double> q, double value)
        {
            q.Enqueue(value);
            while (q.Count > MaxSamples) q.Dequeue();
        }

        private void RefreshDetail()
        {
            // DetailGraph is one shared instance across every sidebar category —
            // clear any leftover dashed second series (e.g. from Wi-Fi's Send
            // line) before each switch, so it doesn't bleed into Disk/Memory/GPU
            // views that never set one themselves. Only the Wi-Fi case below
            // re-populates it.
            DetailGraph.SetSecondSeries(null, Colors.Transparent);
            CpuInfoPanel.Visibility = Visibility.Collapsed;
            MemInfoPanel.Visibility = Visibility.Collapsed;
            DiskInfoPanel.Visibility = Visibility.Collapsed;
            BatteryInfoPanel.Visibility = Visibility.Collapsed;

            switch (_selected)
            {
                case PerformanceResource.Cpu:
                    DetailTitle.Text = "CPU";
                    DetailSubtitle.Text = SystemInfo.ProcessorName;
                    DetailGraph.AccentColor = System.Windows.Media.Color.FromRgb(90, 170, 255);
                    DetailGraph.SetData(_cpuHistory.ToArray(), 100);
                    Stat1Label.Text = "Utilization";
                    Stat1Value.Text = _cpuHistory.Count > 0 ? $"{Last(_cpuHistory):F0}%" : "0%";
                    Stat2Label.Text = "Processes";
                    Stat2Value.Text = GetThrottledProcessCount().ToString();
                    Stat3Label.Text = "Cores";
                    Stat3Value.Text = $"{SystemInfo.PhysicalCoreCount} / {SystemInfo.LogicalCoreCount}";
                    Stat4Label.Text = "Uptime";
                    Stat4Value.Text = SystemInfo.UptimeString;

                    CpuInfoPanel.Visibility = Visibility.Visible;
                    CpuBaseSpeedValue.Text = $"{SystemInfo.BaseSpeedGhz:F2} GHz";
                    CpuSocketsValue.Text = SystemInfo.SocketCount.ToString();
                    CpuCoresValue.Text = SystemInfo.PhysicalCoreCount.ToString();
                    CpuLogicalValue.Text = SystemInfo.LogicalCoreCount.ToString();
                    CpuVirtualizationValue.Text = SystemInfo.VirtualizationEnabled ? "Enabled" : "Disabled";
                    var cacheSizes = SystemInfo.CacheSizes;
                    CpuL1Value.Text = FormatCacheSize(cacheSizes.L1Bytes);
                    CpuL2Value.Text = FormatCacheSize(cacheSizes.L2Bytes);
                    CpuL3Value.Text = FormatCacheSize(cacheSizes.L3Bytes);
                    break;

                case PerformanceResource.Memory:
                    DetailTitle.Text = "Memory";
                    DetailSubtitle.Text = "";
                    DetailGraph.AccentColor = System.Windows.Media.Color.FromRgb(170, 120, 255);
                    DetailGraph.SetData(_memHistory.ToArray(), 100);
                    double memGb = _lastMemUsed / 1024.0 / 1024.0 / 1024.0;
                    double totalGb = _lastMemTotal / 1024.0 / 1024.0 / 1024.0;
                    double pct = _lastMemTotal > 0 ? _lastMemUsed / (double)_lastMemTotal * 100.0 : 0;
                    Stat1Label.Text = "In use";
                    Stat1Value.Text = $"{memGb:F1} GB ({pct:F0}%)";
                    Stat2Label.Text = "Total";
                    Stat2Value.Text = $"{totalGb:F1} GB";
                    Stat3Label.Text = ""; Stat3Value.Text = "";
                    Stat4Label.Text = ""; Stat4Value.Text = "";

                    if (_lastMemDetail != null)
                    {
                        MemInfoPanel.Visibility = Visibility.Visible;
                        double compressedGb = _lastMemDetail.CompressedBytes / 1024.0 / 1024.0 / 1024.0;
                        MemInUseValue.Text = compressedGb > 0.01
                            ? $"{memGb:F1} GB ({compressedGb:F1} GB compressed)"
                            : $"{memGb:F1} GB";
                        MemAvailableValue.Text = $"{_lastMemDetail.AvailableBytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
                        MemCommittedValue.Text = $"{_lastMemDetail.CommittedBytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
                        MemCachedValue.Text = $"{_lastMemDetail.CachedBytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
                        MemPagedPoolValue.Text = FormatCacheSize(_lastMemDetail.PagedPoolBytes);
                        MemNonPagedPoolValue.Text = FormatCacheSize(_lastMemDetail.NonPagedPoolBytes);
                        MemSpeedValue.Text = _lastMemDetail.SpeedMhz > 0 ? $"{_lastMemDetail.SpeedMhz} MHz" : "—";
                        MemSlotsValue.Text = _lastMemDetail.SlotsUsed > 0 ? $"{_lastMemDetail.SlotsUsed} / {_lastMemDetail.TotalSlots}" : "—";
                        MemFormFactorValue.Text = _lastMemDetail.FormFactor;
                        MemHardwareReservedValue.Text = FormatCacheSize(_lastMemDetail.HardwareReservedBytes);
                    }
                    break;

                case PerformanceResource.Disk:
                    DetailTitle.Text = "Disk";
                    DetailSubtitle.Text = "";
                    DetailGraph.AccentColor = System.Windows.Media.Color.FromRgb(90, 220, 140);
                    DetailGraph.SetData(_diskActiveHistory.ToArray(), 100);
                    Stat1Label.Text = "Active time";
                    Stat1Value.Text = $"{_lastDiskActive:F0}%";
                    Stat2Label.Text = "Read speed";
                    Stat2Value.Text = FormatBytesPerSec(_lastDiskRead);
                    Stat3Label.Text = "Write speed";
                    Stat3Value.Text = FormatBytesPerSec(_lastDiskWrite);
                    Stat4Label.Text = "Response time"; Stat4Value.Text = $"{_lastDiskResponse:F1} ms";

                    DiskInfoPanel.Visibility = Visibility.Visible;
                    DiskCapacityValue.Text = $"{DiskStaticInfo.CapacityBytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
                    DiskFormattedValue.Text = $"{DiskStaticInfo.FormattedBytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
                    DiskSystemValue.Text = DiskStaticInfo.IsSystemDisk ? "Yes" : "No";
                    DiskPageFileValue.Text = DiskStaticInfo.HasPageFile ? "Yes" : "No";
                    DiskTypeValue.Text = DiskStaticInfo.DiskType;
                    DiskHealthValue.Text = DiskStaticInfo.HealthStatus;
                    break;

                case PerformanceResource.Wifi:
                    var wifi = _lastWifi;
                    DetailTitle.Text = "Wi-Fi";
                    DetailSubtitle.Text = wifi != null && wifi.IsConnected
                        ? $"{wifi.AdapterName} — {wifi.SSID}"
                        : "Not connected";
                    DetailGraph.AccentColor = System.Windows.Media.Color.FromRgb(90, 170, 255);
                    double wifiScale = Math.Max(100, Math.Max(Max(_wifiReceiveHistory), Max(_wifiSendHistory)) * 1.2);
                    DetailGraph.SetData(_wifiReceiveHistory.ToArray(), wifiScale);
                    DetailGraph.SetSecondSeries(_wifiSendHistory.ToArray(), Colors.Orange);
                    Stat1Label.Text = "Connection type";
                    Stat1Value.Text = wifi?.ConnectionType ?? "—";
                    Stat2Label.Text = "IPv4 address";
                    Stat2Value.Text = string.IsNullOrEmpty(wifi?.IPv4Address) ? "—" : wifi!.IPv4Address;
                    Stat3Label.Text = "IPv6 address";
                    Stat3Value.Text = string.IsNullOrEmpty(wifi?.IPv6Address) ? "—" : wifi!.IPv6Address;
                    Stat4Label.Text = "Signal strength";
                    Stat4Value.Text = wifi != null && wifi.IsConnected ? $"{wifi.SignalQuality}%" : "—";
                    break;

                case PerformanceResource.Gpu0:
                    RefreshGpuDetail(_lastGpu0, _gpu0History, System.Windows.Media.Color.FromRgb(255, 165, 0));
                    break;

                case PerformanceResource.Gpu1:
                    RefreshGpuDetail(_lastGpu1, _gpu1History, System.Windows.Media.Color.FromRgb(90, 220, 140));
                    break;

                case PerformanceResource.Battery:
                    DetailTitle.Text = "Battery";
                    DetailSubtitle.Text = _lastBattery?.IsPresent == true ? _lastBattery.StatusText : "Not detected";
                    DetailGraph.AccentColor = System.Windows.Media.Color.FromRgb(90, 220, 140);
                    DetailGraph.SetData(_batteryHistory.ToArray(), 100);
                    Stat1Label.Text = "Charge";
                    Stat1Value.Text = _lastBattery?.IsPresent == true ? $"{_lastBattery.ChargePercent}%" : "—";
                    Stat2Label.Text = ""; Stat2Value.Text = "";
                    Stat3Label.Text = ""; Stat3Value.Text = "";
                    Stat4Label.Text = ""; Stat4Value.Text = "";

                    BatteryInfoPanel.Visibility = Visibility.Visible;
                    BatteryStatusValue.Text = _lastBattery?.IsPresent == true ? _lastBattery.StatusText : "—";
                    BatteryHealthValue.Text = BatteryStaticInfo.DesignedCapacity > 0 ? $"{BatteryStaticInfo.HealthPercent}%" : "—";
                    BatteryDesignCapacityValue.Text = BatteryStaticInfo.DesignedCapacity > 0 ? $"{BatteryStaticInfo.DesignedCapacity} mWh" : "—";
                    BatteryFullCapacityValue.Text = BatteryStaticInfo.FullChargedCapacity > 0 ? $"{BatteryStaticInfo.FullChargedCapacity} mWh" : "—";
                    break;
            }
        }

        private void RefreshGpuDetail(TaskBuddyWPF.Models.GpuInfo? gpu, Queue<double> history, System.Windows.Media.Color accent)
        {
            DetailTitle.Text = gpu != null ? $"GPU {gpu.GpuIndex}" : "GPU";
            DetailSubtitle.Text = gpu?.AdapterName ?? "Not detected";
            DetailGraph.AccentColor = accent;
            DetailGraph.SetData(history.ToArray(), 100);
            Stat1Label.Text = "Utilization";
            Stat1Value.Text = gpu != null ? $"{gpu.UtilizationPercent:F0}%" : "0%";
            Stat2Label.Text = "Dedicated GPU memory";
            Stat2Value.Text = gpu != null ? $"{gpu.DedicatedUsedBytes / 1024.0 / 1024.0:F0} MB" : "—";
            Stat3Label.Text = "Shared GPU memory";
            Stat3Value.Text = gpu != null ? $"{gpu.SharedUsedBytes / 1024.0 / 1024.0:F0} MB" : "—";
            Stat4Label.Text = ""; Stat4Value.Text = "";
        }

        private static double Max(Queue<double> q)
        {
            double max = 0;
            foreach (var v in q) if (v > max) max = v;
            return max;
        }

        private int GetThrottledProcessCount()
        {
            if ((DateTime.UtcNow - _processCountLastUpdated).TotalSeconds >= 1)
            {
                _processCountCache = System.Diagnostics.Process.GetProcesses().Length;
                _processCountLastUpdated = DateTime.UtcNow;
            }
            return _processCountCache;
        }

        private static double Last(Queue<double> q)
        {
            double last = 0;
            foreach (var v in q) last = v;
            return last;
        }

        private static string FormatCacheSize(ulong bytes)
        {
            if (bytes == 0) return "—";
            if (bytes >= 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0:F1} MB";
            return $"{bytes / 1024.0:F0} KB";
        }

        private static string FormatBytesPerSec(double bytesPerSec)
        {
            if (bytesPerSec >= 1024 * 1024)
                return $"{bytesPerSec / 1024.0 / 1024.0:F1} MB/s";
            if (bytesPerSec >= 1024)
                return $"{bytesPerSec / 1024.0:F1} KB/s";
            return $"{bytesPerSec:F0} B/s";
        }

        private void CpuCard_Click(object sender, MouseButtonEventArgs e) { _selected = PerformanceResource.Cpu; HighlightSelected(); RefreshDetail(); }
        private void MemoryCard_Click(object sender, MouseButtonEventArgs e) { _selected = PerformanceResource.Memory; HighlightSelected(); RefreshDetail(); }
        private void DiskCard_Click(object sender, MouseButtonEventArgs e) { _selected = PerformanceResource.Disk; HighlightSelected(); RefreshDetail(); }
        private void WifiCard_Click(object sender, MouseButtonEventArgs e) { _selected = PerformanceResource.Wifi; HighlightSelected(); RefreshDetail(); }
        private void Gpu0Card_Click(object sender, MouseButtonEventArgs e) { _selected = PerformanceResource.Gpu0; HighlightSelected(); RefreshDetail(); }
        private void Gpu1Card_Click(object sender, MouseButtonEventArgs e) { _selected = PerformanceResource.Gpu1; HighlightSelected(); RefreshDetail(); }
        private void BatteryCard_Click(object sender, MouseButtonEventArgs e) { _selected = PerformanceResource.Battery; HighlightSelected(); RefreshDetail(); }

        private void HighlightSelected()
        {
            var selectedBrush = new SolidColorBrush(Color.FromArgb(40, 90, 170, 255));
            var normalBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            CpuCard.Background = _selected == PerformanceResource.Cpu ? selectedBrush : normalBrush;
            MemoryCard.Background = _selected == PerformanceResource.Memory ? selectedBrush : normalBrush;
            DiskCard.Background = _selected == PerformanceResource.Disk ? selectedBrush : normalBrush;
            WifiCard.Background = _selected == PerformanceResource.Wifi ? selectedBrush : normalBrush;
            Gpu0Card.Background = _selected == PerformanceResource.Gpu0 ? selectedBrush : normalBrush;
            Gpu1Card.Background = _selected == PerformanceResource.Gpu1 ? selectedBrush : normalBrush;
        }

        // Same NavigationView ScrollViewer quirk as ProcessesPage — see that file's
        // comment for the full explanation. Reused pattern, not rediscovered.
        private void Page_Loaded(object sender, RoutedEventArgs e) => UpdatePinnedHeight();

        private void UpdatePinnedHeight()
        {
            var scrollViewer = FindAncestorScrollViewer(this);
            if (scrollViewer == null) return;

            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            if (scrollViewer.ActualHeight > 0)
                RootGrid.Height = scrollViewer.ActualHeight;
        }

        private static ScrollViewer? FindAncestorScrollViewer(DependencyObject child)
        {
            DependencyObject? parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is ScrollViewer sv) return sv;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}




























