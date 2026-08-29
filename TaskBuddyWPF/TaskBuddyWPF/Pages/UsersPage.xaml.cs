using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Task = System.Threading.Tasks.Task;
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
    public partial class UsersPage : Page
    {
        private readonly ProcessEnumerator _enumerator = new();
        private readonly ObservableCollection<UserGroupInfo> _items = new();
        private readonly DispatcherTimer _timer;
        private ICollectionView _view = null!;
        private bool _isRefreshing;
        private string? _selectedUserName;

        // Owner lookups are relatively expensive (OpenProcessToken+LookupAccountSid per PID)
        // and an exe's owning user for a given PID never changes mid-life, so cache by PID
        // to avoid re-resolving every tick. Pruned alongside stale PIDs each refresh.
        private readonly Dictionary<uint, string> _ownerCache = new();

        public UsersPage()
        {
            InitializeComponent();
            _view = CollectionViewSource.GetDefaultView(_items);
            UsersList.ItemsSource = _view;
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
                var grouped = await Task.Run(() =>
                {
                    var snapshot = _enumerator.GetSnapshot();
                    var seenPids = new HashSet<uint>();
                    var byUser = new Dictionary<string, UserGroupInfo>(StringComparer.OrdinalIgnoreCase);
                    var procsByUser = new Dictionary<string, List<ProcessInfo>>(StringComparer.OrdinalIgnoreCase);

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
                            procsByUser[owner] = new List<ProcessInfo>();
                        }
                        group.ProcessCount++;
                        group.TotalCpuPercent += proc.CpuPercent;
                        group.TotalMemoryBytes += proc.WorkingSetBytes;
                        procsByUser[owner].Add(proc);
                    }

                    var stale = _ownerCache.Keys.Where(pid => !seenPids.Contains(pid)).ToList();
                    foreach (var pid in stale) _ownerCache.Remove(pid);

                    return byUser.Values
                        .OrderByDescending(u => u.TotalMemoryBytes)
                        .Select(u => (Group: u, Processes: procsByUser[u.UserName]))
                        .ToList();
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

        private void ApplyDiff(List<(UserGroupInfo Group, List<ProcessInfo> Processes)> fresh)
        {
            var freshMap = fresh.ToDictionary(f => f.Group.UserName, StringComparer.OrdinalIgnoreCase);

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (!freshMap.ContainsKey(_items[i].UserName)) _items.RemoveAt(i);
            }
            foreach (var (freshGroup, freshProcs) in fresh)
            {
                var existing = _items.FirstOrDefault(i => string.Equals(i.UserName, freshGroup.UserName, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    foreach (var p in freshProcs) freshGroup.Processes.Add(p);
                    _items.Add(freshGroup);
                }
                else
                {
                    existing.ProcessCount = freshGroup.ProcessCount;
                    existing.TotalCpuPercent = freshGroup.TotalCpuPercent;
                    existing.TotalMemoryBytes = freshGroup.TotalMemoryBytes;
                    ApplyProcessDiff(existing.Processes, freshProcs);
                }
            }

            // The detail panel is bound directly to a group's live Processes collection
            // (see UsersGrid_SelectionChanged), so diffing that collection in place above
            // is enough to keep the panel live — no separate refresh needed here.
        }

        private static void ApplyProcessDiff(ObservableCollection<ProcessInfo> existing, List<ProcessInfo> fresh)
        {
            var freshMap = fresh.ToDictionary(p => p.Pid);

            for (int i = existing.Count - 1; i >= 0; i--)
            {
                if (!freshMap.ContainsKey(existing[i].Pid)) existing.RemoveAt(i);
            }
            foreach (var p in fresh)
            {
                var match = existing.FirstOrDefault(e => e.Pid == p.Pid);
                if (match == null) existing.Add(p);
                else
                {
                    match.WorkingSetBytes = p.WorkingSetBytes;
                    match.CpuPercent = p.CpuPercent;
                    match.IsSuspended = p.IsSuspended;
                }
            }
        }

        private void UserCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not UserGroupInfo group) return;

            foreach (var u in _items) u.IsSelected = false;
            group.IsSelected = true;

            _selectedUserName = group.UserName;
            DetailHeader.Text = $"Processes — {group.UserName}";
            ProcessesGrid.ItemsSource = group.Processes;
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
