using System.ComponentModel;
using System.Windows.Media;

namespace TaskBuddyWPF.Models
{
    public class ProcessDetailInfo : INotifyPropertyChanged
    {
        public uint Pid { get; set; }
        public string Name { get; set; } = "";
        public ImageSource? Icon { get; set; }
        public string UserName { get; set; } = "";
        public string Architecture { get; set; } = "";
        public bool IsVirtualized { get; set; }
        public string Description { get; set; } = "";

        private string _status = "Running";
        public string Status
        {
            get => _status;
            set { if (_status != value) { _status = value; Notify(nameof(Status)); } }
        }

        private double _cpuPercent;
        public double CpuPercent
        {
            get => _cpuPercent;
            set { if (_cpuPercent != value) { _cpuPercent = value; Notify(nameof(CpuPercent)); } }
        }

        private ulong _memoryBytes;
        public ulong MemoryBytes
        {
            get => _memoryBytes;
            set { if (_memoryBytes != value) { _memoryBytes = value; Notify(nameof(MemoryBytes)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
