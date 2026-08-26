using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Pages
{
    public partial class SettingsPage : Page
    {
        private bool _pageInitialized;

        public SettingsPage()
        {
            InitializeComponent();

            ShowIdleProcessCheck.IsChecked = AppSettings.ShowIdleProcess;

            var match = RefreshSpeedCombo.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Tag == AppSettings.RefreshIntervalSeconds.ToString());
            RefreshSpeedCombo.SelectedItem = match ?? RefreshSpeedCombo.Items[0];

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version {version?.ToString(3) ?? "unknown"}";

            _pageInitialized = true;
        }

        private void ShowIdleProcess_Changed(object sender, RoutedEventArgs e)
        {
            if (!_pageInitialized) return;
            AppSettings.ShowIdleProcess = ShowIdleProcessCheck.IsChecked == true;
        }

        private void RefreshSpeed_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_pageInitialized) return;
            if (RefreshSpeedCombo.SelectedItem is ComboBoxItem item && int.TryParse((string)item.Tag, out var seconds))
            {
                AppSettings.RefreshIntervalSeconds = seconds;
            }
        }
    }
}
