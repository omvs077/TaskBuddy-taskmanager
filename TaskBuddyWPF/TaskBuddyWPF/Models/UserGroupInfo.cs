using System.ComponentModel;

namespace TaskBuddyWPF.Models
{
    public class UserGroupInfo : INotifyPropertyChanged
    {
        public string UserName { get; set; } = "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; Notify(nameof(IsSelected)); } }
        }

        public System.Collections.ObjectModel.ObservableCollection<TaskBuddyWPF.Models.ProcessInfo> Processes { get; } = new();

        private int _processCount;
        public int ProcessCount
        {
            get => _processCount;
            set { if (_processCount != value) { _processCount = value; Notify(nameof(ProcessCount)); } }
        }

        private double _totalCpuPercent;
        public double TotalCpuPercent
        {
            get => _totalCpuPercent;
            set { if (_totalCpuPercent != value) { _totalCpuPercent = value; Notify(nameof(TotalCpuPercent)); } }
        }

        private ulong _totalMemoryBytes;
        public ulong TotalMemoryBytes
        {
            get => _totalMemoryBytes;
            set { if (_totalMemoryBytes != value) { _totalMemoryBytes = value; Notify(nameof(TotalMemoryBytes)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
