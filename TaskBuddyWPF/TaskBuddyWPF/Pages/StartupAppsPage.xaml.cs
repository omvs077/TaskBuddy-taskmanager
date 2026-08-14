using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Pages
{
    public partial class StartupAppsPage : Page
    {
        private readonly ObservableCollection<StartupAppInfo> _items = new();
        private ICollectionView _view = null!;
        private bool _isRefreshing;
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public StartupAppsPage()
        {
            InitializeComponent();
            StartupGrid.ItemsSource = _items;
            _view = CollectionViewSource.GetDefaultView(_items);
            SizeChanged += (s, e) => UpdatePinnedHeight();
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
                var fresh = await Task.Run(() => StartupEnumerator.Enumerate());
                ApplyDiff(fresh);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to enumerate startup apps: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { _isRefreshing = false; }
        }

        // Key is Source+Command (full path/task path) rather than Source+Name,
        // since two entries (e.g. desktop.ini in two different Startup folders,
        // or two differently-located apps with the same display name) can share
        // a Name but never share a full Command path.
        private static string KeyOf(StartupAppInfo i) => i.Source + "|" + i.Command;

        private void ApplyDiff(List<StartupAppInfo> fresh)
        {
            var freshMap = new Dictionary<string, StartupAppInfo>();
            foreach (var f in fresh) freshMap[KeyOf(f)] = f;

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (!freshMap.ContainsKey(KeyOf(_items[i]))) _items.RemoveAt(i);
            }
            foreach (var f in fresh)
            {
                var key = KeyOf(f);
                var existing = _items.FirstOrDefault(i => KeyOf(i) == key);
                if (existing == null) _items.Add(f);
                else
                {
                    existing.Impact = f.Impact;
                    existing.IsEnabled = f.IsEnabled;
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = SearchBox.Text?.Trim() ?? "";
            _view.Filter = text.Length == 0 ? null :
                (o => o is StartupAppInfo s &&
                      s.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        private void StartupGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && dep is not DataGridRow) dep = VisualTreeHelper.GetParent(dep);
            if (dep is DataGridRow row) row.IsSelected = true;
        }

        private void Enable_Click(object sender, RoutedEventArgs e) => SetEnabled(true);
        private void Disable_Click(object sender, RoutedEventArgs e) => SetEnabled(false);

        private void SetEnabled(bool enabled)
        {
            if (StartupGrid.SelectedItem is not StartupAppInfo item) return;
            try
            {
                switch (item.Source)
                {
                    case StartupSource.RegistryRun:
                        MessageBox.Show("Registry Run-key enable/disable requires writing the " +
                            "StartupApproved binary flag format Windows itself uses. Not yet " +
                            "implemented — flagging for a follow-up decision rather than doing " +
                            "a partial/incorrect toggle.", "Not implemented",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    case StartupSource.TaskScheduler:
                        using (var ts = new TaskService())
                        {
                            var task = ts.GetTask(item.Command);
                            if (task != null) task.Enabled = enabled;
                        }
                        break;
                    case StartupSource.StartupFolder:
                        MessageBox.Show("Startup-folder items are enabled/disabled by moving the " +
                            "shortcut file; not yet implemented.", "Not implemented",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                }
                item.IsEnabled = enabled;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to change startup state: {ex.Message}", "Error",
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
