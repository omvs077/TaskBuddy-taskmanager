using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Pages
{
    public partial class GpuPage : Page, IDisposable
    {
        private readonly GpuEnumerator _enumerator = new();
        private readonly DispatcherTimer _timer;

        public GpuPage()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += async (s, e) => await SampleAsync();
            _timer.Start();

            _ = SampleAsync();
        }

        private async Task SampleAsync()
        {
            var snapshot = await Task.Run(() => _enumerator.GetSnapshot());
            GpuList.ItemsSource = snapshot;
        }

        public void Dispose() => _enumerator.Dispose();
    }
}
