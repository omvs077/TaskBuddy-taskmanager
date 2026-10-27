using System.ComponentModel;
using System.Windows.Media;

namespace TaskBuddyWPF.Models
{
    public enum StartupImpact { NotMeasured, None, Low, Medium, High }
    public enum StartupSource { RegistryRun, StartupFolder, TaskScheduler }

    public class StartupAppInfo : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string Command { get; set; } = "";
        public StartupSource Source { get; set; }
        public StartupImpact Impact { get; set; } = StartupImpact.NotMeasured;
        public ImageSource? Icon { get; set; }

        // Only meaningful for Source == RegistryRun: which hive (HKCU vs HKLM) the
        // Run-key entry lives in, needed to write the matching StartupApproved\Run
        // key. HKLM entries typically need elevation to toggle; HKCU entries don't.
        public bool IsHkcu { get; set; } = true;

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
