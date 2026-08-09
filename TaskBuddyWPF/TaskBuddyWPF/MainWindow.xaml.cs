using System.Windows;
using TaskBuddyWPF.Pages;
using Wpf.Ui.Controls;

namespace TaskBuddyWPF
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void RootNav_Loaded(object sender, RoutedEventArgs e)
        {
            RootNav.Navigate(typeof(ProcessesPage));
        }
    }
}
