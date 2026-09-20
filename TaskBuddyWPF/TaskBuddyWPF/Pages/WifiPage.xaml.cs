using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Pages
{
    public partial class WifiPage : Page, IDisposable
    {
        private const int MaxSamples = 120; // 60s of history at 500ms sampling, matches Performance tab
        private readonly WifiEnumerator _enumerator = new();
        private readonly DispatcherTimer _timer;
        private readonly Queue<double> _receiveHistory = new();
        private readonly Queue<double> _sendHistory = new();

        public WifiPage()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += async (s, e) => await SampleAsync();
            _timer.Start();

            _ = SampleAsync();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) { }

        private async Task SampleAsync()
        {
            var info = await Task.Run(() => _enumerator.GetSnapshot());

            if (!info.IsConnected)
            {
                ConnectedPanel.Visibility = Visibility.Collapsed;
                DisconnectedPanel.Visibility = Visibility.Visible;
                DetailSubtitle.Text = "";
                return;
            }

            ConnectedPanel.Visibility = Visibility.Visible;
            DisconnectedPanel.Visibility = Visibility.Collapsed;

            Enqueue(_receiveHistory, info.ReceiveKbps);
            Enqueue(_sendHistory, info.SendKbps);

            double maxScale = Math.Max(100, Math.Max(Max(_receiveHistory), Max(_sendHistory)) * 1.2);
            DetailGraph.AccentColor = Color.FromRgb(90, 170, 255);
            DetailGraph.SetData(_receiveHistory.ToArray(), maxScale);
            DetailGraph.SetSecondSeries(_sendHistory.ToArray(), Colors.Orange);

            DetailSubtitle.Text = $"{info.AdapterName} — Receiving {info.ReceiveKbps:F0} Kbps, Sending {info.SendKbps:F0} Kbps";
            AdapterNameValue.Text = string.IsNullOrEmpty(info.AdapterName) ? "—" : info.AdapterName;
            SsidValue.Text = string.IsNullOrEmpty(info.SSID) ? "—" : info.SSID;
            ConnectionTypeValue.Text = info.ConnectionType;
            Ipv4Value.Text = string.IsNullOrEmpty(info.IPv4Address) ? "—" : info.IPv4Address;
            Ipv6Value.Text = string.IsNullOrEmpty(info.IPv6Address) ? "—" : info.IPv6Address;
            SignalValue.Text = $"{info.SignalQuality}%";
            DrawSignalBars(info.SignalQuality);
        }

        private void DrawSignalBars(int quality)
        {
            SignalBars.Children.Clear();
            int activeBars = quality <= 0 ? 0 : (int)Math.Ceiling(quality / 25.0);
            for (int i = 0; i < 4; i++)
            {
                var bar = new Rectangle
                {
                    Width = 4,
                    Height = 6 + i * 4,
                    Margin = new Thickness(1, 0, 0, 0),
                    Fill = i < activeBars
                        ? new SolidColorBrush(Color.FromRgb(90, 170, 255))
                        : new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                SignalBars.Children.Add(bar);
            }
        }

        private static void Enqueue(Queue<double> q, double value)
        {
            q.Enqueue(value);
            while (q.Count > MaxSamples) q.Dequeue();
        }

        private static double Max(Queue<double> q)
        {
            double max = 0;
            foreach (var v in q) if (v > max) max = v;
            return max;
        }

        public void Dispose() => _enumerator.Dispose();
    }
}
