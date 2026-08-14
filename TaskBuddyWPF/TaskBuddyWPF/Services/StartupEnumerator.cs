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

            ReadRunKey(Registry.CurrentUser, list);
            ReadRunKey(Registry.LocalMachine, list);
            ReadStartupFolder(Environment.SpecialFolder.Startup, list);
            ReadStartupFolder(Environment.SpecialFolder.CommonStartup, list);
            ReadTaskScheduler(list);

            return list;
        }

        private static void ReadRunKey(RegistryKey hive, List<StartupAppInfo> list)
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
                    IsEnabled = true,
                    Impact = StartupImpact.NotMeasured,
                    Icon = IconHelper.ResolveIcon(IconHelper.ExtractExePath(cmd))
                });
            }
        }

        private static void ReadStartupFolder(Environment.SpecialFolder folder, List<StartupAppInfo> list)
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
                    IsEnabled = true,
                    Impact = StartupImpact.NotMeasured,
                    // .lnk shortcuts: SHGetFileInfo resolves shell icons for .lnk directly
                    // (shows the target's icon), so no shortcut-target resolution needed.
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

                // Task actions can reference a real exe (ExecAction.Path) — use it for an
                // icon when available; otherwise fall back to the generic default rather
                // than guessing from the task name.
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
