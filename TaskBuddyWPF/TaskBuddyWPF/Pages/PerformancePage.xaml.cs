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
    public enum PerformanceResource { Cpu, Memory, Disk }

    public partial class PerformancePage : Page
    {
        private const int MaxSamples = 60;

        private readonly SystemPerformanceMonitor _sysMonitor = new();
        private readonly DiskPerformanceMonitor _diskMonitor = new();
        private readonly DispatcherTimer _timer;
        private bool _isSampling;

        private readonly Queue<double> _cpuHistory = new();
        private readonly Queue<double> _memHistory = new();
        private readonly Queue<double> _diskActiveHistory = new();

        private ulong _lastMemUsed, _lastMemTotal;
        private double _lastDiskActive, _lastDiskRead, _lastDiskWrite;

        private PerformanceResource _selected = PerformanceResource.Cpu;

        public PerformancePage()
        {
            InitializeComponent();
            SizeChanged += (s, e) => UpdatePinnedHeight();

            CpuMiniGraph.SetHeaderVisible(false);
            MemoryMiniGraph.SetHeaderVisible(false);
            DiskMiniGraph.SetHeaderVisible(false);
            DetailGraph.SetHeaderVisible(false);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TaskBuddyWPF.Services.AppSettings.RefreshIntervalSeconds) };
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
                var (cpu, memUsed, memTotal, diskActive, diskRead, diskWrite) = await Task.Run(() =>
                {
                    double c = _sysMonitor.GetCpuPercent();
                    var (used, total) = _sysMonitor.GetMemoryUsage();
                    var (active, read, write) = _diskMonitor.Sample();
                    return (c, used, total, active, read, write);
                });

                _lastMemUsed = memUsed;
                _lastMemTotal = memTotal;
                _lastDiskActive = diskActive;
                _lastDiskRead = diskRead;
                _lastDiskWrite = diskWrite;

                Enqueue(_cpuHistory, cpu);
                Enqueue(_memHistory, memTotal > 0 ? memUsed / (double)memTotal * 100.0 : 0);
                Enqueue(_diskActiveHistory, diskActive);

                double memGb = memUsed / 1024.0 / 1024.0 / 1024.0;
                double totalGb = memTotal / 1024.0 / 1024.0 / 1024.0;

                CpuMiniGraph.SetData(_cpuHistory.ToArray(), 100);
                CpuMiniValue.Text = $"{cpu:F0}%";

                MemoryMiniGraph.SetData(_memHistory.ToArray(), 100);
                MemoryMiniValue.Text = $"{memGb:F1}/{totalGb:F1} GB";

                DiskMiniGraph.SetData(_diskActiveHistory.ToArray(), 100);
                DiskMiniValue.Text = $"{diskActive:F0}%";

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
            switch (_selected)
            {
                case PerformanceResource.Cpu:
                    DetailTitle.Text = "CPU";
                    DetailGraph.SetData(_cpuHistory.ToArray(), 100);
                    Stat1Label.Text = "Utilization";
                    Stat1Value.Text = _cpuHistory.Count > 0 ? $"{Last(_cpuHistory):F0}%" : "0%";
                    Stat2Label.Text = "Processes";
                    Stat2Value.Text = System.Diagnostics.Process.GetProcesses().Length.ToString();
                    Stat3Label.Text = ""; Stat3Value.Text = "";
                    Stat4Label.Text = ""; Stat4Value.Text = "";
                    break;

                case PerformanceResource.Memory:
                    DetailTitle.Text = "Memory";
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
                    break;

                case PerformanceResource.Disk:
                    DetailTitle.Text = "Disk";
                    DetailGraph.SetData(_diskActiveHistory.ToArray(), 100);
                    Stat1Label.Text = "Active time";
                    Stat1Value.Text = $"{_lastDiskActive:F0}%";
                    Stat2Label.Text = "Read speed";
                    Stat2Value.Text = FormatBytesPerSec(_lastDiskRead);
                    Stat3Label.Text = "Write speed";
                    Stat3Value.Text = FormatBytesPerSec(_lastDiskWrite);
                    Stat4Label.Text = ""; Stat4Value.Text = "";
                    break;
            }
        }

        private static double Last(Queue<double> q)
        {
            double last = 0;
            foreach (var v in q) last = v;
            return last;
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

        private void HighlightSelected()
        {
            var selectedBrush = new SolidColorBrush(Color.FromArgb(40, 90, 170, 255));
            var normalBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            CpuCard.Background = _selected == PerformanceResource.Cpu ? selectedBrush : normalBrush;
            MemoryCard.Background = _selected == PerformanceResource.Memory ? selectedBrush : normalBrush;
            DiskCard.Background = _selected == PerformanceResource.Disk ? selectedBrush : normalBrush;
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
