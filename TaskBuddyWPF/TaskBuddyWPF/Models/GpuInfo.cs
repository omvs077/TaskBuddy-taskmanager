using System.ComponentModel;

namespace TaskBuddyWPF.Models
{
    public class GpuInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public int GpuIndex { get; set; }
        public string AdapterName { get; set; } = string.Empty;

        private double _utilizationPercent;
        public double UtilizationPercent
        {
            get => _utilizationPercent;
            set { if (_utilizationPercent != value) { _utilizationPercent = value; Notify(nameof(UtilizationPercent)); } }
        }

        private ulong _dedicatedUsedBytes;
        public ulong DedicatedUsedBytes
        {
            get => _dedicatedUsedBytes;
            set { if (_dedicatedUsedBytes != value) { _dedicatedUsedBytes = value; Notify(nameof(DedicatedUsedBytes)); } }
        }

        private ulong _sharedUsedBytes;
        public ulong SharedUsedBytes
        {
            get => _sharedUsedBytes;
            set { if (_sharedUsedBytes != value) { _sharedUsedBytes = value; Notify(nameof(SharedUsedBytes)); } }
        }
    }
}
