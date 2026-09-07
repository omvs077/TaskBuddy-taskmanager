using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Pages
{
    public partial class DetailsPage : Page
    {
        private readonly DetailsEnumerator _enumerator = new();
        private readonly ObservableCollection<ProcessDetailInfo> _items = new();
        private readonly DispatcherTimer _timer;
        private ICollectionView _view = null!;
        private bool _isRefreshing;

        public DetailsPage()
        {
            InitializeComponent();
            DetailsGrid.ItemsSource = _items;
            _view = CollectionViewSource.GetDefaultView(_items);
            SizeChanged += (s, e) => UpdatePinnedHeight();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TaskBuddyWPF.Services.AppSettings.RefreshIntervalSeconds) };
            _timer.Tick += async (s, e) => await RefreshAsync();
            _timer.Start();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePinnedHeight();
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            try
            {
                var fresh = await Task.Run(() => _enumerator.GetSnapshot());
                ApplyDiff(fresh);

                if (NavigationTarget.RequestedPid.HasValue)
                {
                    SelectAndScrollToPid(NavigationTarget.RequestedPid.Value);
                    NavigationTarget.RequestedPid = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to enumerate process details: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { _isRefreshing = false; }
        }

        private void ApplyDiff(List<ProcessDetailInfo> fresh)
        {
            var freshMap = fresh.ToDictionary(f => f.Pid);

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (!freshMap.ContainsKey(_items[i].Pid)) _items.RemoveAt(i);
            }
            foreach (var f in fresh)
            {
                var existing = _items.FirstOrDefault(i => i.Pid == f.Pid);
                if (existing == null) _items.Add(f);
                else
                {
                    existing.Status = f.Status;
                    existing.CpuPercent = f.CpuPercent;
                    existing.MemoryBytes = f.MemoryBytes;
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = SearchBox.Text?.Trim() ?? "";
            _view.Filter = text.Length == 0 ? null :
                (o => o is ProcessDetailInfo p &&
                      p.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        private void DetailsGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && dep is not DataGridRow) dep = VisualTreeHelper.GetParent(dep);
            if (dep is DataGridRow row) row.IsSelected = true;
        }


        private void SelectAndScrollToPid(uint pid)
        {
            var match = _items.FirstOrDefault(i => i.Pid == pid);
            if (match == null) return;

            DetailsGrid.SelectedItem = match;
            DetailsGrid.ScrollIntoView(match);
        }

        private void UpdatePinnedHeight()
        {
            var sv = FindAncestorScrollViewer(this);
            if (sv == null) return;
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            RootGrid.Height = sv.ActualHeight;
        }

        private static ScrollViewer? FindAncestorScrollViewer(DependencyObject d)
        {
            var parent = VisualTreeHelper.GetParent(d);
            while (parent != null && parent is not ScrollViewer) parent = VisualTreeHelper.GetParent(parent);
            return parent as ScrollViewer;
        }
        private void DetailsContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            D_SuspendResumeMenuItem.Header = selected.IsSuspended ? "Resume" : "Suspend";
            D_EfficiencyModeMenuItem.IsChecked = selected.IsEfficiencyMode;
        }

        private async void D_SuspendResume_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            await _enumerator.ToggleSuspend(selected.Pid, selected.Name, selected.IsSuspended, RefreshAsync);
        }

        private async void D_EfficiencyMode_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            await _enumerator.ToggleEfficiencyMode(selected.Pid, selected.Name, selected.IsEfficiencyMode, RefreshAsync);
        }

        private async void D_EndTask_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            await _enumerator.EndTask(selected.Pid, selected.Name, RefreshAsync);
        }

        private async void D_EndProcessTree_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            await _enumerator.EndProcessTree(selected.Pid, selected.Name, RefreshAsync);
        }

        private async void D_CreateDumpFile_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            await _enumerator.CreateDumpFile(selected.Pid, selected.Name);
        }

        private void D_OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            ProcessActions.OpenFileLocation(selected.ImagePath);
        }

        private void D_CopyPid_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            ProcessActions.CopyPid(selected.Pid);
        }

        private async void D_SetPriority_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            if (sender is not MenuItem { Tag: string tag }) return;
            uint priorityClass = tag switch
            {
                "Realtime" => 0x00000100u,
                "High" => 0x00000080u,
                "AboveNormal" => 0x00008000u,
                "Normal" => 0x00000020u,
                "BelowNormal" => 0x00004000u,
                "Idle" => 0x00000040u,
                _ => 0x00000020u
            };
            await _enumerator.SetPriority(selected.Pid, selected.Name, priorityClass, RefreshAsync);
        }

        private async void D_SetAffinity_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            await _enumerator.SetAffinity(selected.Pid, selected.Name, Window.GetWindow(this));
        }

        private void D_SearchOnline_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            ProcessActions.SearchOnline(selected.Name);
        }

        private void D_Properties_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            ProcessActions.Properties(selected.ImagePath);
        }

        private void D_GoToService_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            ProcessActions.GoToService(selected.Pid, Window.GetWindow(this));
        }
    }
}
