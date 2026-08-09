using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace TaskBuddyWPF.Dialogs
{
    public partial class RunTaskDialog : FluentWindow
    {
        public string? SelectedPath { get; private set; }

        public RunTaskDialog()
        {
            InitializeComponent();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Programs and files (*.exe;*.*)|*.exe;*.*" };
            if (dialog.ShowDialog() == true)
                PathBox.Text = dialog.FileName;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            SelectedPath = PathBox.Text.Trim();
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
