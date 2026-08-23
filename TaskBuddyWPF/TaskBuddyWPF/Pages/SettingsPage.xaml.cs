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

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version {version?.ToString(3) ?? "unknown"}";

            _pageInitialized = true;
        }

        private void ShowIdleProcess_Changed(object sender, RoutedEventArgs e)
        {
            if (!_pageInitialized) return; // same XAML-parse-order guard as ProcessesPage's column checkboxes
            AppSettings.ShowIdleProcess = ShowIdleProcessCheck.IsChecked == true;
        }
    }
}
