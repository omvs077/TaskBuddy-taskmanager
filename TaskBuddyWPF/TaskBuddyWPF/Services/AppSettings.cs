using System;
using System.Collections.Generic;
using System.IO;

namespace TaskBuddyWPF.Services
{
    public static class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TaskBuddy", "settings.ini");

        private static bool _showIdleProcess;
        public static bool ShowIdleProcess
        {
            get => _showIdleProcess;
            set { _showIdleProcess = value; Save(); }
        }

        // Base refresh cadence in seconds for most tabs (Fast=1, Normal=2, Slow=5,
        // matching the spirit of Task Manager's own Update Speed setting). The
        // Services tab deliberately runs at 3x this value, unchanged behavior from
        // before this setting existed — service enumeration is heavier than process
        // enumeration and was intentionally throttled.
        private static int _refreshIntervalSeconds = 1;
        public static int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set { _refreshIntervalSeconds = value; Save(); }
        }

        static AppSettings()
        {
            Load();
        }

        private static void Load()
        {
            if (!File.Exists(SettingsPath)) return;
            try
            {
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;
                    var parts = trimmed.Split('=', 2);
                    if (parts.Length != 2) continue;

                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    if (key.Equals("ShowIdleProcess", StringComparison.OrdinalIgnoreCase)
                        && bool.TryParse(value, out var showIdle))
                    {
                        _showIdleProcess = showIdle;
                    }
                    else if (key.Equals("RefreshIntervalSeconds", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(value, out var interval) && interval > 0)
                    {
                        _refreshIntervalSeconds = interval;
                    }
                }
            }
            catch
            {
                // Corrupt/unreadable settings file — fall back to defaults rather than crash.
            }
        }

        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllLines(SettingsPath, new List<string>
                {
                    "# TaskBuddy application settings — auto-generated",
                    $"ShowIdleProcess={_showIdleProcess}",
                    $"RefreshIntervalSeconds={_refreshIntervalSeconds}"
                });
            }
            catch
            {
                // Non-fatal: failing to persist a preference shouldn't crash the app.
            }
        }
    }
}
