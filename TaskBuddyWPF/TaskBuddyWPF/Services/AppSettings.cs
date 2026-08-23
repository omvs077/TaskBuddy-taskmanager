using System;
using System.Collections.Generic;
using System.IO;

namespace TaskBuddyWPF.Services
{
    // Global app settings, separate file from layout.ini (column visibility) to keep
    // concerns isolated — settings.ini for behavior toggles, layout.ini for UI layout.
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

                    if (parts[0].Trim().Equals("ShowIdleProcess", StringComparison.OrdinalIgnoreCase)
                        && bool.TryParse(parts[1].Trim(), out var val))
                    {
                        _showIdleProcess = val;
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
                    $"ShowIdleProcess={_showIdleProcess}"
                });
            }
            catch
            {
                // Non-fatal: failing to persist a preference shouldn't crash the app.
            }
        }
    }
}
