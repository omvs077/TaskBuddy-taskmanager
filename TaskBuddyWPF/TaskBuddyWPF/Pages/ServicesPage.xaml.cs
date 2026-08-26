using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Pages
{
    public partial class ServicesPage : Page
    {
        private readonly ServiceEnumerator _enumerator = new();
        private readonly ObservableCollection<ServiceInfo> _services = new();
        private readonly DispatcherTimer _timer;
        private bool _isRefreshing;

        public ServicesPage()
        {
            InitializeComponent();
            ServiceGrid.ItemsSource = _services;
            SizeChanged += (s, e) => UpdatePinnedHeight();

            var view = CollectionViewSource.GetDefaultView(_services);
            view.Filter = FilterService;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TaskBuddyWPF.Services.AppSettings.RefreshIntervalSeconds * 3) };
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

        private void ApplyDiff(List<ServiceInfo> snapshot)
        {
            var incoming = new Dictionary<string, ServiceInfo>();
            foreach (var s in snapshot)
                incoming[s.ServiceName] = s;

            for (int i = _services.Count - 1; i >= 0; i--)
            {
                if (!incoming.ContainsKey(_services[i].ServiceName))
                    _services.RemoveAt(i);
            }

            var existing = new Dictionary<string, ServiceInfo>();
            foreach (var s in _services)
                existing[s.ServiceName] = s;

            foreach (var fresh in snapshot)
            {
                if (existing.TryGetValue(fresh.ServiceName, out var current))
                {
                    current.Pid = fresh.Pid;
                    current.IsRunning = fresh.IsRunning;
                }
                else
                {
                    _services.Add(fresh);
                }
            }
        }

        private void ServiceGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var row = ItemsControl.ContainerFromElement(ServiceGrid, (DependencyObject)e.OriginalSource) as DataGridRow;
            if (row != null) row.IsSelected = true;
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (ServiceGrid.SelectedItem is ServiceInfo selected)
                StartStopMenuItem.Header = selected.IsRunning ? "Stop" : "Start";
        }

        private async void StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (ServiceGrid.SelectedItem is not ServiceInfo selected)
                return;

            bool success = selected.IsRunning
                ? await Task.Run(() => _enumerator.StopServiceByName(selected.ServiceName))
                : await Task.Run(() => _enumerator.StartServiceByName(selected.ServiceName));

            if (!success)
            {
                string action = selected.IsRunning ? "stop" : "start";
                MessageBox.Show($"Unable to {action} '{selected.DisplayName}'. It may require elevated permissions.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await RefreshAsync();
        }

        private void OpenServices_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("services.msc") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open Services console:\n{ex.Message}",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool FilterService(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text)) return true;
            if (obj is not ServiceInfo s) return false;

            string query = SearchBox.Text.Trim();
            return s.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.ServiceName.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            CollectionViewSource.GetDefaultView(_services).Refresh();
        }

        // Same NavigationView ScrollViewer quirk as ProcessesPage/PerformancePage.
        private void Page_Loaded(object sender, RoutedEventArgs e) => UpdatePinnedHeight();

        private void UpdatePinnedHeight()
        {
            var scrollViewer = FindAncestorScrollViewer(this);
            if (scrollViewer == null) return;

            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            if (scrollViewer.ActualHeight > 0)
                RootGrid.Height = scrollViewer.ActualHeight;
        }

        private static ScrollViewer? FindAncestorScrollViewer(DependencyObject child)
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is ScrollViewer sv) return sv;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
