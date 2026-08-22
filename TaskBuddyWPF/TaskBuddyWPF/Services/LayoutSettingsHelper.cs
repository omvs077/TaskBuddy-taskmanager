using System;
using System.Collections.Generic;
using System.IO;

namespace TaskBuddyWPF.Services
{
    // Minimal INI-style key=value persistence for column visibility layout state.
    // Deliberately not using a library (per Anti-Overengineering Ladder — Stdlib rung):
    // 5 boolean flags don't warrant an INI parsing dependency.
    public static class LayoutSettingsHelper
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TaskBuddy", "layout.ini");

        public static Dictionary<string, bool> LoadColumnVisibility()
        {
            var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(SettingsPath)) return result;

            try
            {
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;
                    var parts = trimmed.Split('=', 2);
                    if (parts.Length != 2) continue;
                    if (bool.TryParse(parts[1].Trim(), out var value))
                        result[parts[0].Trim()] = value;
                }
            }
            catch
            {
                // Corrupt/unreadable settings file — fall back to defaults (all visible)
                // rather than crash the app over a cosmetic preference file.
            }
            return result;
        }

        public static void SaveColumnVisibility(Dictionary<string, bool> columns)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                Directory.CreateDirectory(dir);
                var lines = new List<string> { "# TaskBuddy column layout — auto-generated" };
                foreach (var kv in columns)
                    lines.Add($"{kv.Key}={kv.Value}");
                File.WriteAllLines(SettingsPath, lines);
            }
            catch
            {
                // Non-fatal: failing to persist a UI preference shouldn't crash the app.
            }
        }
    }
}
