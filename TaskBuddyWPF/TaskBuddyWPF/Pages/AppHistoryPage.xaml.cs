using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TaskBuddyWPF.Models;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Pages
{
    public partial class AppHistoryPage : Page
    {
        private readonly ObservableCollection<AppHistoryInfo> _items = new();
        private ICollectionView _view = null!;
        private bool _isRefreshing;
        private bool _hasLoadedOnce;

        public AppHistoryPage()
        {
            InitializeComponent();
            HistoryGrid.ItemsSource = _items;
            _view = CollectionViewSource.GetDefaultView(_items);
            SizeChanged += (s, e) => UpdatePinnedHeight();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePinnedHeight();
            if (!_hasLoadedOnce)
            {
                _hasLoadedOnce = true;
                await RefreshAsync();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async Task RefreshAsync()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            StatusText.Visibility = Visibility.Collapsed;
            HistoryGrid.Visibility = Visibility.Visible;

            try
            {
                var history = await Task.Run(() => SrumReader.ReadHistory());
                _items.Clear();
                foreach (var h in history) _items.Add(h);

                if (_items.Count == 0)
                {
                    ShowStatus("No app history data found in the SRUM database yet.");
                }
            }
            catch (UnauthorizedAccessException)
            {
                ShowStatus("Reading app history requires running TaskBuddy as Administrator.");
            }
            catch (IOException ex)
            {
                ShowStatus($"Could not read the SRUM database (it may be locked): {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load app history: {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void ShowStatus(string message)
        {
            HistoryGrid.Visibility = Visibility.Collapsed;
            StatusText.Text = message;
            StatusText.Visibility = Visibility.Visible;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = SearchBox.Text?.Trim() ?? "";
            _view.Filter = text.Length == 0 ? null :
                (o => o is AppHistoryInfo a &&
                      a.AppName.Contains(text, StringComparison.OrdinalIgnoreCase));
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
