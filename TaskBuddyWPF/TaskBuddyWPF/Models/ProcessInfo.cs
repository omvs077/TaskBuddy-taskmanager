using System.ComponentModel;
using System.Windows.Media;

namespace TaskBuddyWPF.Models
{
    public class ProcessInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public uint Pid { get; set; }
        public uint ParentPid { get; set; }
        public string ImageName { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string CommandLine { get; set; } = string.Empty;
        public ImageSource? Icon { get; set; }

        private bool _hasVisibleWindow;
        public bool HasVisibleWindow
        {
            get => _hasVisibleWindow;
            set { if (_hasVisibleWindow != value) { _hasVisibleWindow = value; Notify(nameof(HasVisibleWindow)); } }
        }

        private bool _isCritical;
        public bool IsCritical
        {
            get => _isCritical;
            set { if (_isCritical != value) { _isCritical = value; Notify(nameof(IsCritical)); } }
        }

        // Must raise PropertyChanged for WPF's live-grouping (IsLiveGrouping) to
        // re-bucket this item into a different CollectionViewGroup incrementally,
        // instead of requiring a full CollectionView.Refresh() that destroys and
        // recreates GroupItem containers (and resets any Expander's collapsed state).
        private string _category = "Background process";
        public string Category
        {
            get => _category;
            set { if (_category != value) { _category = value; Notify(nameof(Category)); } }
        }

        private uint _groupPid;
        public uint GroupPid
        {
            get => _groupPid;
            set { if (_groupPid != value) { _groupPid = value; Notify(nameof(GroupPid)); } }
        }

        private int _indentLevel;
        public int IndentLevel
        {
            get => _indentLevel;
            set { if (_indentLevel != value) { _indentLevel = value; Notify(nameof(IndentLevel)); } }
        }

        private ulong _workingSetBytes;
        public ulong WorkingSetBytes
        {
            get => _workingSetBytes;
            set { if (_workingSetBytes != value) { _workingSetBytes = value; Notify(nameof(WorkingSetBytes)); } }
        }

        private long _cpuTime100ns;
        public long CpuTime100ns
        {
            get => _cpuTime100ns;
            set { if (_cpuTime100ns != value) { _cpuTime100ns = value; Notify(nameof(CpuTime100ns)); } }
        }

        private double _cpuPercent;
        public double CpuPercent
        {
            get => _cpuPercent;
            set { if (_cpuPercent != value) { _cpuPercent = value; Notify(nameof(CpuPercent)); } }
        }

        private bool _isSuspended;
        public bool IsSuspended
        {
            get => _isSuspended;
            set { if (_isSuspended != value) { _isSuspended = value; Notify(nameof(IsSuspended)); Notify(nameof(StatusText)); } }
        }

        public string StatusText => IsSuspended ? "Suspended" : "Running";

        // Reflects only what TaskBuddy itself has toggled (tracked locally in
        // ProcessEnumerator, same pattern as IsSuspended) rather than a live OS
        // query — GetProcessInformation for ProcessPowerThrottling is documented
        // as unreliable on some Windows builds even right after a successful Set.
        // Efficiency Mode enabled by real Task Manager or powercfg will not show here.
        private bool _isEfficiencyMode;
        public bool IsEfficiencyMode
        {
            get => _isEfficiencyMode;
            set { if (_isEfficiencyMode != value) { _isEfficiencyMode = value; Notify(nameof(IsEfficiencyMode)); } }
        }

        private double _diskBytesPerSec;
        public double DiskBytesPerSec
        {
            get => _diskBytesPerSec;
            set { if (_diskBytesPerSec != value) { _diskBytesPerSec = value; Notify(nameof(DiskBytesPerSec)); } }
        }
    }
}
