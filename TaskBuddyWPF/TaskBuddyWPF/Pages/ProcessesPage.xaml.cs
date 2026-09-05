using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TaskBuddyWPF.Dialogs;
using TaskBuddyWPF.Native;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Pages
{
    public partial class ProcessesPage : Page
    {
        private readonly ProcessEnumerator _enumerator = new();
        private readonly ObservableCollection<ProcessInfo> _processes = new();
        private readonly DispatcherTimer _timer;
        private bool _isRefreshing;
        private bool _isApplyingLoadedLayout;
        private bool _pageInitialized;

        public ProcessesPage()
        {
            InitializeComponent();
            ProcessGrid.ItemsSource = _processes;
            SizeChanged += ProcessesPage_SizeChanged;

            var view = (ListCollectionView)CollectionViewSource.GetDefaultView(_processes);
            view.Filter = FilterProcess;
            view.SortDescriptions.Add(new SortDescription(nameof(ProcessInfo.WorkingSetBytes), ListSortDirection.Descending));
            view.IsLiveSorting = true;
            view.LiveSortingProperties.Add(nameof(ProcessInfo.WorkingSetBytes));
            view.LiveSortingProperties.Add(nameof(ProcessInfo.CpuPercent));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TaskBuddyWPF.Services.AppSettings.RefreshIntervalSeconds) };
            _timer.Tick += async (s, e) => await RefreshAsync();
            _timer.Start();

            LoadColumnLayout();
            _pageInitialized = true;
            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;

            try
            {
                var snapshot = await Task.Run(() => _enumerator.GetSnapshot());
                ApplyDiff(snapshot);
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void ApplyDiff(List<ProcessInfo> snapshot)
        {
            var incoming = new Dictionary<uint, ProcessInfo>();
            foreach (var p in snapshot)
                incoming[p.Pid] = p;

            for (int i = _processes.Count - 1; i >= 0; i--)
            {
                if (!incoming.ContainsKey(_processes[i].Pid))
                    _processes.RemoveAt(i);
            }

            var existing = new Dictionary<uint, ProcessInfo>();
            foreach (var p in _processes)
                existing[p.Pid] = p;

            foreach (var fresh in snapshot)
            {
                if (existing.TryGetValue(fresh.Pid, out var current))
                {
                    current.CpuPercent = fresh.CpuPercent;
                    current.CpuTime100ns = fresh.CpuTime100ns;
                    current.WorkingSetBytes = fresh.WorkingSetBytes;
                    current.ImagePath = fresh.ImagePath;
                    current.IsSuspended = fresh.IsSuspended;
                    current.IsEfficiencyMode = fresh.IsEfficiencyMode;
                    current.DiskBytesPerSec = fresh.DiskBytesPerSec;
                    current.Icon = fresh.Icon;
                }
                else
                {
                    _processes.Add(fresh);
                }
            }
        }

        private void ProcessGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var row = ItemsControl.ContainerFromElement(ProcessGrid, (DependencyObject)e.OriginalSource) as DataGridRow;
            if (row != null)
                row.IsSelected = true;
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;

            SuspendResumeMenuItem.Header = selected.IsSuspended ? "Resume" : "Suspend";
            EfficiencyModeMenuItem.IsChecked = selected.IsEfficiencyMode;
            GoToServiceMenuItem.IsEnabled = string.Equals(selected.ImageName, "svchost.exe", StringComparison.OrdinalIgnoreCase);

            uint? currentPriority = _enumerator.GetProcessPriority(selected.Pid);
            PriorityRealtimeItem.IsChecked = currentPriority == NativeMethods.REALTIME_PRIORITY_CLASS;
            PriorityHighItem.IsChecked = currentPriority == NativeMethods.HIGH_PRIORITY_CLASS;
            PriorityAboveNormalItem.IsChecked = currentPriority == NativeMethods.ABOVE_NORMAL_PRIORITY_CLASS;
            PriorityNormalItem.IsChecked = currentPriority == NativeMethods.NORMAL_PRIORITY_CLASS;
            PriorityBelowNormalItem.IsChecked = currentPriority == NativeMethods.BELOW_NORMAL_PRIORITY_CLASS;
            PriorityIdleItem.IsChecked = currentPriority == NativeMethods.IDLE_PRIORITY_CLASS;
        }

        private async void SuspendResume_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            await ProcessActions.ToggleSuspend(_enumerator, selected.Pid, selected.ImageName, selected.IsSuspended, RefreshAsync);
        }

        private async void EfficiencyMode_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            await ProcessActions.ToggleEfficiencyMode(_enumerator, selected.Pid, selected.ImageName, selected.IsEfficiencyMode, RefreshAsync);
        }

        private async void EndTask_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            await ProcessActions.EndTask(_enumerator, selected.Pid, selected.ImageName, RefreshAsync);
        }

        private async void EndProcessTree_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            await ProcessActions.EndProcessTree(_enumerator, selected.Pid, selected.ImageName, RefreshAsync);
        }

        private async void CreateDumpFile_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            await ProcessActions.CreateDumpFile(_enumerator, selected.Pid, selected.ImageName);
        }

        private void RunNewTask_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RunTaskDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(dialog.SelectedPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not start '{dialog.SelectedPath}':\n{ex.Message}",
                        "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            ProcessActions.OpenFileLocation(selected.ImagePath);
        }

        private void CopyPid_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            ProcessActions.CopyPid(selected.Pid);
        }

        private async void SetPriority_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            if (sender is not MenuItem { Tag: string tag }) return;

            uint priorityClass = tag switch
            {
                "Realtime" => NativeMethods.REALTIME_PRIORITY_CLASS,
                "High" => NativeMethods.HIGH_PRIORITY_CLASS,
                "AboveNormal" => NativeMethods.ABOVE_NORMAL_PRIORITY_CLASS,
                "BelowNormal" => NativeMethods.BELOW_NORMAL_PRIORITY_CLASS,
                "Idle" => NativeMethods.IDLE_PRIORITY_CLASS,
                _ => NativeMethods.NORMAL_PRIORITY_CLASS
            };

            await ProcessActions.SetPriority(_enumerator, selected.Pid, selected.ImageName, priorityClass, RefreshAsync);
        }

        private async void SetAffinity_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            await ProcessActions.SetAffinity(_enumerator, selected.Pid, selected.ImageName, Window.GetWindow(this));
        }

        private void SearchOnline_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            ProcessActions.SearchOnline(selected.ImageName);
        }

        private void Properties_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            ProcessActions.Properties(selected.ImagePath);
        }

        private void GoToDetails_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            ProcessActions.GoToDetails(selected.Pid, Window.GetWindow(this));
        }

        private void GoToService_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected) return;
            ProcessActions.GoToService(selected.Pid, Window.GetWindow(this));
        }

        private bool FilterProcess(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
                return true;

            if (obj is not ProcessInfo p)
                return false;

            string query = SearchBox.Text.Trim();
            return p.ImageName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || p.Pid.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            CollectionViewSource.GetDefaultView(_processes).Refresh();
        }

        // --- Column visibility (Milestone 6.1) ---

        private void ColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            ColumnsPopup.IsOpen = !ColumnsPopup.IsOpen;
        }

        private void ColumnCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!_pageInitialized || _isApplyingLoadedLayout) return; // guard against XAML's IsChecked="True" firing this during InitializeComponent, before named columns are assigned, and against re-saving while we're the ones setting checkbox state on load

            PidColumn.Visibility = ColPidCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            StatusColumn.Visibility = ColStatusCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            CpuColumn.Visibility = ColCpuCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            MemoryColumn.Visibility = ColMemoryCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            DiskColumn.Visibility = ColDiskCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            SaveColumnLayout();
        }

        private void LoadColumnLayout()
        {
            var saved = LayoutSettingsHelper.LoadColumnVisibility();
            if (saved.Count == 0) return; // no saved layout yet — keep all-visible defaults

            _isApplyingLoadedLayout = true;
            try
            {
                if (saved.TryGetValue("PID", out var pid)) { ColPidCheck.IsChecked = pid; PidColumn.Visibility = pid ? Visibility.Visible : Visibility.Collapsed; }
                if (saved.TryGetValue("Status", out var status)) { ColStatusCheck.IsChecked = status; StatusColumn.Visibility = status ? Visibility.Visible : Visibility.Collapsed; }
                if (saved.TryGetValue("CPU", out var cpu)) { ColCpuCheck.IsChecked = cpu; CpuColumn.Visibility = cpu ? Visibility.Visible : Visibility.Collapsed; }
                if (saved.TryGetValue("Memory", out var mem)) { ColMemoryCheck.IsChecked = mem; MemoryColumn.Visibility = mem ? Visibility.Visible : Visibility.Collapsed; }
                if (saved.TryGetValue("Disk", out var disk)) { ColDiskCheck.IsChecked = disk; DiskColumn.Visibility = disk ? Visibility.Visible : Visibility.Collapsed; }
            }
            finally
            {
                _isApplyingLoadedLayout = false;
            }
        }

        private void SaveColumnLayout()
        {
            LayoutSettingsHelper.SaveColumnVisibility(new Dictionary<string, bool>
            {
                ["PID"] = ColPidCheck.IsChecked == true,
                ["Status"] = ColStatusCheck.IsChecked == true,
                ["CPU"] = ColCpuCheck.IsChecked == true,
                ["Memory"] = ColMemoryCheck.IsChecked == true,
                ["Disk"] = ColDiskCheck.IsChecked == true
            });
        }

        // WPF-UI's NavigationView wraps its Frame content in its own ScrollViewer that
        // ignores the Page's row layout, letting the whole page scroll instead of just
        // the DataGrid. Fix: pin RootGrid's height to that ScrollViewer's actual viewport
        // size, so the ancestor ScrollViewer never has anything to scroll, and the
        // DataGrid's own internal scrolling (with fixed headers) takes over naturally.
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _enumerator.IncludeIdleProcess = TaskBuddyWPF.Services.AppSettings.ShowIdleProcess;
            UpdatePinnedHeight();
        }

        private void ProcessesPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePinnedHeight();
        }

        private void UpdatePinnedHeight()
        {
            var scrollViewer = FindAncestorScrollViewer(this);
            if (scrollViewer == null) return;

            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            if (scrollViewer != null && scrollViewer.ActualHeight > 0)
                RootGrid.Height = scrollViewer.ActualHeight;
        }

        private static ScrollViewer? FindAncestorScrollViewer(DependencyObject child)
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is ScrollViewer sv)
                    return sv;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
