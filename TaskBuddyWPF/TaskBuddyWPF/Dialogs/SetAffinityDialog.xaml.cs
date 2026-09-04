using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace TaskBuddyWPF.Dialogs
{
    public partial class SetAffinityDialog : FluentWindow
    {
        public ulong SelectedAffinityMask { get; private set; }

        private readonly System.Collections.Generic.List<CheckBox> _coreChecks = new();

        public SetAffinityDialog(string processName, ulong currentMask, int coreCount)
        {
            InitializeComponent();
            HeaderText.Text = $"Which processors are allowed to run '{processName}'?";

            for (int i = 0; i < coreCount; i++)
            {
                bool isChecked = (currentMask & (1UL << i)) != 0;
                var check = new CheckBox
                {
                    Content = $"CPU {i}",
                    IsChecked = isChecked,
                    Margin = new Thickness(0, 2, 0, 2),
                    Tag = i
                };
                check.Checked += (s, e) => UpdateSelectAllState();
                check.Unchecked += (s, e) => UpdateSelectAllState();
                _coreChecks.Add(check);
            }

            CoreList.ItemsSource = _coreChecks;
            UpdateSelectAllState();
        }

        private void UpdateSelectAllState()
        {
            bool allChecked = _coreChecks.TrueForAll(c => c.IsChecked == true);
            bool noneChecked = _coreChecks.TrueForAll(c => c.IsChecked == false);
            SelectAllCheck.IsChecked = allChecked ? true : (noneChecked ? false : (bool?)null);
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool target = SelectAllCheck.IsChecked != false;
            foreach (var c in _coreChecks) c.IsChecked = target;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ulong mask = 0;
            foreach (var c in _coreChecks)
            {
                if (c.IsChecked == true && c.Tag is int i)
                    mask |= 1UL << i;
            }

            if (mask == 0)
            {
                System.Windows.MessageBox.Show("At least one processor must be selected.",
                    "TaskBuddy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            SelectedAffinityMask = mask;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
