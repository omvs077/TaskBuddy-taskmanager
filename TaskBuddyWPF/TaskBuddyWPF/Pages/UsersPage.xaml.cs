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
    public partial class UsersPage : Page
    {
        private readonly ProcessEnumerator _enumerator = new();
        private readonly ObservableCollection<UserGroupInfo> _items = new();
        private readonly DispatcherTimer _timer;
        private ICollectionView _view = null!;
        private bool _isRefreshing;

        // Owner lookups are relatively expensive (OpenProcessToken+LookupAccountSid per PID)
        // and an exe's owning user for a given PID never changes mid-life, so cache by PID
        // to avoid re-resolving every tick. Pruned alongside stale PIDs each refresh.
        private readonly Dictionary<uint, string> _ownerCache = new();

        public UsersPage()
        {
            InitializeComponent();
            UsersGrid.ItemsSource = _items;
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
                var grouped = await Task.Run(() =>
                {
                    var snapshot = _enumerator.GetSnapshot();
                    var seenPids = new HashSet<uint>();
                    var byUser = new Dictionary<string, UserGroupInfo>(StringComparer.OrdinalIgnoreCase);

                    foreach (var proc in snapshot)
                    {
                        seenPids.Add(proc.Pid);

                        if (!_ownerCache.TryGetValue(proc.Pid, out var owner))
                        {
                            owner = UserHelper.ResolveOwner(proc.Pid) ?? "SYSTEM / Protected (access denied)";
                            _ownerCache[proc.Pid] = owner;
                        }

                        if (!byUser.TryGetValue(owner, out var group))
                        {
                            group = new UserGroupInfo { UserName = owner };
                            byUser[owner] = group;
                        }
                        group.ProcessCount++;
                        group.TotalCpuPercent += proc.CpuPercent;
                        group.TotalMemoryBytes += proc.WorkingSetBytes;
                    }

                    var stale = _ownerCache.Keys.Where(pid => !seenPids.Contains(pid)).ToList();
                    foreach (var pid in stale) _ownerCache.Remove(pid);

                    return byUser.Values.OrderByDescending(u => u.TotalMemoryBytes).ToList();
                });

                ApplyDiff(grouped);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to enumerate users: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { _isRefreshing = false; }
        }

        private void ApplyDiff(List<UserGroupInfo> fresh)
        {
            var freshMap = fresh.ToDictionary(f => f.UserName, StringComparer.OrdinalIgnoreCase);

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (!freshMap.ContainsKey(_items[i].UserName)) _items.RemoveAt(i);
            }
            foreach (var f in fresh)
            {
                var existing = _items.FirstOrDefault(i => string.Equals(i.UserName, f.UserName, StringComparison.OrdinalIgnoreCase));
                if (existing == null) _items.Add(f);
                else
                {
                    existing.ProcessCount = f.ProcessCount;
                    existing.TotalCpuPercent = f.TotalCpuPercent;
                    existing.TotalMemoryBytes = f.TotalMemoryBytes;
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = SearchBox.Text?.Trim() ?? "";
            _view.Filter = text.Length == 0 ? null :
                (o => o is UserGroupInfo u &&
                      u.UserName.Contains(text, StringComparison.OrdinalIgnoreCase));
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
