using System.ComponentModel;

namespace TaskBuddyWPF.Models
{
    public class AppHistoryInfo : INotifyPropertyChanged
    {
        public string AppName { get; set; } = "";
        public string AppPath { get; set; } = "";
        public long CpuCycles { get; set; }
        public ulong NetworkBytes { get; set; }
        public System.DateTime LastActive { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
