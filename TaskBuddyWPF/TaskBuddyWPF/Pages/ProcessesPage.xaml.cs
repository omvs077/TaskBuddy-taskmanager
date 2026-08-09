using System;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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

        public ProcessesPage()
        {
            InitializeComponent();
            SizeChanged += ProcessesPage_SizeChanged;
            ProcessGrid.ItemsSource = _processes;

            var view = (ListCollectionView)CollectionViewSource.GetDefaultView(_processes);
            view.Filter = FilterProcess;
            view.SortDescriptions.Add(new SortDescription(nameof(ProcessInfo.WorkingSetBytes), ListSortDirection.Descending));
            view.IsLiveSorting = true;
            view.LiveSortingProperties.Add(nameof(ProcessInfo.WorkingSetBytes));
            view.LiveSortingProperties.Add(nameof(ProcessInfo.CpuPercent));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += async (s, e) => await RefreshAsync();
            _timer.Start();

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
            if (ProcessGrid.SelectedItem is ProcessInfo selected)
                SuspendResumeMenuItem.Header = selected.IsSuspended ? "Resume" : "Suspend";
        }

        private async void SuspendResume_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected)
                return;

            bool success = selected.IsSuspended
                ? await Task.Run(() => _enumerator.ResumeProcess(selected.Pid))
                : await Task.Run(() => _enumerator.SuspendProcess(selected.Pid));

            if (!success)
            {
                string action = selected.IsSuspended ? "resume" : "suspend";
                MessageBox.Show($"Unable to {action} '{selected.ImageName}' (PID {selected.Pid}). It may require elevated permissions.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await RefreshAsync();
        }

        private async void EndTask_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is not ProcessInfo selected)
                return;

            bool success = await Task.Run(() => _enumerator.TerminateProcess(selected.Pid));
            if (!success)
            {
                MessageBox.Show($"Unable to end task '{selected.ImageName}' (PID {selected.Pid}). It may require elevated permissions.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await RefreshAsync();
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

        // WPF-UI's NavigationView wraps its Frame content in its own ScrollViewer that
        // ignores the Page's row layout, letting the whole page scroll instead of just
        // the DataGrid. Fix: pin RootGrid's height to that ScrollViewer's actual viewport
        // size, so the ancestor ScrollViewer never has anything to scroll, and the
        // DataGrid's own internal scrolling (with fixed headers) takes over naturally.
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePinnedHeight();
        }

        private void ProcessesPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePinnedHeight();
        }

        private void UpdatePinnedHeight()
        {
            var scrollViewer = FindAncestorScrollViewer(this);
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
