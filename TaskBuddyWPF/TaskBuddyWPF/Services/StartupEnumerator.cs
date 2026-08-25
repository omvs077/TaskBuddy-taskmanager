using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using TaskBuddyWPF.Models;

namespace TaskBuddyWPF.Services
{
    public static class StartupEnumerator
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static List<StartupAppInfo> Enumerate()
        {
            var list = new List<StartupAppInfo>();

            ReadRunKey(Registry.CurrentUser, true, list);
            ReadRunKey(Registry.LocalMachine, false, list);
            ReadStartupFolder(Environment.SpecialFolder.Startup, true, list);
            ReadStartupFolder(Environment.SpecialFolder.CommonStartup, false, list);
            ReadTaskScheduler(list);

            return list;
        }

        private static void ReadRunKey(RegistryKey hive, bool isHkcu, List<StartupAppInfo> list)
        {
            using var key = hive.OpenSubKey(RunKeyPath);
            if (key == null) return;
            foreach (var name in key.GetValueNames())
            {
                var cmd = key.GetValue(name)?.ToString() ?? "";
                list.Add(new StartupAppInfo
                {
                    Name = name,
                    Command = cmd,
                    Source = StartupSource.RegistryRun,
                    IsHkcu = isHkcu,
                    IsEnabled = StartupApprovedHelper.GetRunEnabled(isHkcu, name),
                    Impact = StartupImpact.NotMeasured,
                    Icon = IconHelper.ResolveIcon(IconHelper.ExtractExePath(cmd))
                });
            }
        }

        private static void ReadStartupFolder(Environment.SpecialFolder folder, bool isHkcu, List<StartupAppInfo> list)
        {
            var path = Environment.GetFolderPath(folder);
            if (!Directory.Exists(path)) return;
            foreach (var file in Directory.GetFiles(path))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                if ((File.GetAttributes(file) & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                var name = Path.GetFileNameWithoutExtension(file);
                list.Add(new StartupAppInfo
                {
                    Name = name,
                    Command = file,
                    Source = StartupSource.StartupFolder,
                    IsHkcu = isHkcu,
                    // StartupApproved\StartupFolder is keyed by the shortcut's full
                    // filename (with extension), not the display name.
                    IsEnabled = StartupApprovedHelper.GetStartupFolderEnabled(isHkcu, fileName),
                    Impact = StartupImpact.NotMeasured,
                    Icon = IconHelper.ResolveIcon(file)
                });
            }
        }

        private static void ReadTaskScheduler(List<StartupAppInfo> list)
        {
            using var ts = new TaskService();
            foreach (var task in ts.AllTasks)
            {
                bool isLogonTrigger = false;
                foreach (var trig in task.Definition.Triggers)
                {
                    if (trig.TriggerType == TaskTriggerType.Logon || trig.TriggerType == TaskTriggerType.Boot)
                    {
                        isLogonTrigger = true;
                        break;
                    }
                }
                if (!isLogonTrigger) continue;

                string? exePath = null;
                foreach (var action in task.Definition.Actions)
                {
                    if (action is Microsoft.Win32.TaskScheduler.ExecAction exec)
                    {
                        exePath = exec.Path;
                        break;
                    }
                }

                list.Add(new StartupAppInfo
                {
                    Name = task.Name,
                    Command = task.Path,
                    Source = StartupSource.TaskScheduler,
                    IsEnabled = task.Enabled,
                    Impact = StartupImpact.NotMeasured,
                    Icon = IconHelper.ResolveIcon(exePath)
                });
            }
        }
    }
}
