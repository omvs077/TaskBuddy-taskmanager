using System.ComponentModel;
using System.Windows.Media;

namespace TaskBuddyWPF.Models
{
    public enum StartupImpact { NotMeasured, None, Low, High }
    public enum StartupSource { RegistryRun, StartupFolder, TaskScheduler }

    public class StartupAppInfo : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string Command { get; set; } = "";
        public StartupSource Source { get; set; }
        public StartupImpact Impact { get; set; } = StartupImpact.NotMeasured;
        public ImageSource? Icon { get; set; }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
