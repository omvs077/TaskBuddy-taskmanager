using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using TaskBuddyWPF.Dialogs;
using TaskBuddyWPF.Native;
using TaskBuddyWPF.Pages;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Services
{
    // Shared logic behind the process context menu, reused identically across the
    // Processes tab, the Users tab's per-user process list, and the Details tab.
    // Takes raw fields rather than a concrete model type (ProcessInfo vs.
    // ProcessDetailInfo differ) so both models' pages can call the same code.
    // UI wiring (menu items, checkmarks, confirmation copy) stays per-page since
    // that's unavoidable boilerplate in plain WPF; this class is the one place the
    // actual behavior lives.
    public static class ProcessActions
    {
        public static async Task EndTask(ProcessEnumerator enumerator, uint pid, string imageName, Func<Task> refresh)
        {
            bool success = await Task.Run(() => enumerator.TerminateProcess(pid));
            if (!success)
            {
                MessageBox.Show($"Unable to end task '{imageName}' (PID {pid}). It may require elevated permissions.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            await refresh();
        }

        public static async Task EndProcessTree(ProcessEnumerator enumerator, uint pid, string imageName, Func<Task> refresh)
        {
            var confirm = MessageBox.Show(
                $"This will end '{imageName}' (PID {pid}) and every process it started. Continue?",
                "TaskBuddy", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            var (succeeded, failed) = await Task.Run(() => enumerator.EndProcessTree(pid));
            if (failed > 0)
            {
                MessageBox.Show($"Ended {succeeded} process(es); {failed} could not be ended (may require elevated permissions).",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            await refresh();
        }

        public static async Task ToggleSuspend(ProcessEnumerator enumerator, uint pid, string imageName, bool isSuspended, Func<Task> refresh)
        {
            bool success = isSuspended
                ? await Task.Run(() => enumerator.ResumeProcess(pid))
                : await Task.Run(() => enumerator.SuspendProcess(pid));

            if (!success)
            {
                string action = isSuspended ? "resume" : "suspend";
                MessageBox.Show($"Unable to {action} '{imageName}' (PID {pid}). It may require elevated permissions.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            await refresh();
        }

        public static async Task ToggleEfficiencyMode(ProcessEnumerator enumerator, uint pid, string imageName, bool isEfficiencyMode, Func<Task> refresh)
        {
            bool success = isEfficiencyMode
                ? await Task.Run(() => enumerator.DisableEfficiencyMode(pid))
                : await Task.Run(() => enumerator.EnableEfficiencyMode(pid));

            if (!success)
            {
                string action = isEfficiencyMode ? "disable" : "enable";
                MessageBox.Show($"Unable to {action} efficiency mode for '{imageName}' (PID {pid}). It may require elevated permissions.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            await refresh();
        }

        public static async Task SetPriority(ProcessEnumerator enumerator, uint pid, string imageName, uint priorityClass, Func<Task> refresh)
        {
            bool success = await Task.Run(() => enumerator.SetProcessPriority(pid, priorityClass));
            if (!success)
            {
                MessageBox.Show($"Unable to change priority for '{imageName}' (PID {pid}). It may require elevated permissions.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            await refresh();
        }

        public static async Task SetAffinity(ProcessEnumerator enumerator, uint pid, string imageName, Window owner)
        {
            ulong? currentMask = await Task.Run(() => enumerator.GetProcessAffinity(pid));
            if (currentMask == null)
            {
                MessageBox.Show($"Unable to read processor affinity for '{imageName}'. It may require elevated permissions.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SetAffinityDialog(imageName, currentMask.Value, SystemInfo.LogicalCoreCount) { Owner = owner };
            if (dialog.ShowDialog() == true)
            {
                bool success = await Task.Run(() => enumerator.SetProcessAffinity(pid, dialog.SelectedAffinityMask));
                if (!success)
                {
                    MessageBox.Show($"Unable to set processor affinity for '{imageName}'. It may require elevated permissions.",
                        "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        public static async Task CreateDumpFile(ProcessEnumerator enumerator, uint pid, string imageName)
        {
            string baseName = Path.GetFileNameWithoutExtension(imageName);
            string filePath = Path.Combine(Path.GetTempPath(), $"{baseName}.DMP");

            bool success = await Task.Run(() => enumerator.CreateDumpFile(pid, filePath));
            if (success)
            {
                MessageBox.Show($"The file has been successfully created.\n\nPath: {filePath}",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Unable to create a dump file for '{imageName}'. It may require elevated permissions.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public static void OpenFileLocation(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                MessageBox.Show("File location is not available for this process.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{imagePath}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file location:\n{ex.Message}",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public static void CopyPid(uint pid) => Clipboard.SetText(pid.ToString());

        public static void SearchOnline(string imageName)
        {
            try
            {
                string query = Uri.EscapeDataString(imageName);
                Process.Start(new ProcessStartInfo($"https://www.bing.com/search?q={query}") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser:\n{ex.Message}",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public static void Properties(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                MessageBox.Show("Properties are not available for this process.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sei = new SHELLEXECUTEINFO
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<SHELLEXECUTEINFO>(),
                lpVerb = "properties",
                lpFile = imagePath,
                fMask = NativeMethods.SEE_MASK_INVOKEIDLIST,
                nShow = 1
            };
            if (!NativeMethods.ShellExecuteEx(ref sei))
            {
                MessageBox.Show("Could not open properties for this process.",
                    "TaskBuddy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public static void GoToDetails(uint pid, Window window)
        {
            NavigationTarget.RequestedPid = pid;
            FindMainWindow(window)?.RootNav.Navigate(typeof(DetailsPage));
        }

        public static void GoToService(uint pid, Window window)
        {
            NavigationTarget.RequestedPid = pid;
            FindMainWindow(window)?.RootNav.Navigate(typeof(ServicesPage));
        }

        private static MainWindow? FindMainWindow(Window fallback) =>
            Application.Current.MainWindow as MainWindow ?? fallback as MainWindow;
    }
}
