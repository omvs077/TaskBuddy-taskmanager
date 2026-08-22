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

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
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

        private async void EndTask_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsGrid.SelectedItem is not ProcessDetailInfo selected) return;
            bool success = await Task.Run(() => _enumerator.TerminateProcess(selected.Pid));
            if (!success)
            {
                MessageBox.Show($"Failed to terminate process {selected.Name} (PID {selected.Pid}). " +
                    "It may require elevated permissions.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
    }
}
