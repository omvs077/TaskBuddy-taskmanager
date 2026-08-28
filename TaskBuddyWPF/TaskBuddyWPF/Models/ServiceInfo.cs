using System.ComponentModel;
using System.Windows.Media;

namespace TaskBuddyWPF.Models
{
    public class ServiceInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public uint Pid { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public ImageSource? Icon { get; set; }

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set { if (_isRunning != value) { _isRunning = value; Notify(nameof(IsRunning)); Notify(nameof(StatusText)); } }
        }

        public string StatusText => IsRunning ? "Running" : "Stopped";
        public string PidText => Pid > 0 ? Pid.ToString() : "";
    }
}

