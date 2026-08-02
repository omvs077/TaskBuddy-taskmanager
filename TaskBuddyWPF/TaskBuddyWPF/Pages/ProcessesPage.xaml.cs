using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
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

        public ProcessesPage()
        {
            InitializeComponent();
            ProcessGrid.ItemsSource = _processes;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => Refresh();
            _timer.Start();

            Refresh();
        }

        private void Refresh()
        {
            var snapshot = _enumerator.GetSnapshot();
            _processes.Clear();
            foreach (var p in snapshot)
                _processes.Add(p);
        }
    }
}
