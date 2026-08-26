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
            var bootTrace = StartupImpactReader.ReadLatestBootTrace();

            ReadRunKey(Registry.CurrentUser, true, list, bootTrace);
            ReadRunKey(Registry.LocalMachine, false, list, bootTrace);
            ReadStartupFolder(Environment.SpecialFolder.Startup, true, list, bootTrace);
            ReadStartupFolder(Environment.SpecialFolder.CommonStartup, false, list, bootTrace);
            ReadTaskScheduler(list, bootTrace);

            return list;
        }

        // Matches by bare exe filename since the boot-trace XML only records process
        // names, not full paths. Returns NotMeasured (matching Task Manager's own
        // fallback) if the entry hasn't been observed in the latest boot trace yet.
        private static StartupImpact LookupImpact(
            Dictionary<string, (long cpuMicroseconds, long diskBytes)> bootTrace, string? exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath)) return StartupImpact.NotMeasured;
            var fileName = Path.GetFileName(exePath);
            if (!bootTrace.TryGetValue(fileName, out var usage)) return StartupImpact.NotMeasured;
            return StartupImpactReader.ClassifyImpact(usage.cpuMicroseconds, usage.diskBytes);
        }

        private static void ReadRunKey(RegistryKey hive, bool isHkcu, List<StartupAppInfo> list,
            Dictionary<string, (long, long)> bootTrace)
        {
            using var key = hive.OpenSubKey(RunKeyPath);
            if (key == null) return;
            foreach (var name in key.GetValueNames())
            {
                var cmd = key.GetValue(name)?.ToString() ?? "";
                var exePath = IconHelper.ExtractExePath(cmd);
                list.Add(new StartupAppInfo
                {
                    Name = name,
                    Command = cmd,
                    Source = StartupSource.RegistryRun,
                    IsHkcu = isHkcu,
                    IsEnabled = StartupApprovedHelper.GetRunEnabled(isHkcu, name),
                    Impact = LookupImpact(bootTrace, exePath),
                    Icon = IconHelper.ResolveIcon(exePath)
                });
            }
        }

        private static void ReadStartupFolder(Environment.SpecialFolder folder, bool isHkcu, List<StartupAppInfo> list,
            Dictionary<string, (long, long)> bootTrace)
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
                    IsEnabled = StartupApprovedHelper.GetStartupFolderEnabled(isHkcu, fileName),
                    // .lnk shortcuts won't match the boot-trace by name directly (the
                    // trace records the launched exe, not the shortcut) — this will
                    // mostly show NotMeasured for folder items, an honest limitation.
                    Impact = LookupImpact(bootTrace, file),
                    Icon = IconHelper.ResolveIcon(file)
                });
            }
        }

        private static void ReadTaskScheduler(List<StartupAppInfo> list, Dictionary<string, (long, long)> bootTrace)
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
                    Impact = LookupImpact(bootTrace, exePath),
                    Icon = IconHelper.ResolveIcon(exePath)
                });
            }
        }
    }
}
