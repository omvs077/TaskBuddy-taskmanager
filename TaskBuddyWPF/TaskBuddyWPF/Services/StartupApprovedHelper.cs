using System;
using Microsoft.Win32;

namespace TaskBuddyWPF.Services
{
    public static class StartupApprovedHelper
    {
        private const string RunApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string StartupFolderApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        public static bool GetRunEnabled(bool isHkcu, string valueName)
        {
            var hive = isHkcu ? Registry.CurrentUser : Registry.LocalMachine;
            return GetEnabled(hive, RunApprovedPath, valueName);
        }

        public static void SetRunEnabled(bool isHkcu, string valueName, bool enabled)
        {
            var hive = isHkcu ? Registry.CurrentUser : Registry.LocalMachine;
            SetEnabled(hive, RunApprovedPath, valueName, enabled);
        }

        public static bool GetStartupFolderEnabled(bool isHkcu, string valueName)
        {
            var hive = isHkcu ? Registry.CurrentUser : Registry.LocalMachine;
            return GetEnabled(hive, StartupFolderApprovedPath, valueName);
        }

        public static void SetStartupFolderEnabled(bool isHkcu, string valueName, bool enabled)
        {
            var hive = isHkcu ? Registry.CurrentUser : Registry.LocalMachine;
            SetEnabled(hive, StartupFolderApprovedPath, valueName, enabled);
        }

        private static bool GetEnabled(RegistryKey hive, string subKeyPath, string valueName)
        {
            using var key = hive.OpenSubKey(subKeyPath);
            var raw = key?.GetValue(valueName) as byte[];
            if (raw == null || raw.Length == 0) return true;

            byte state = raw[0];
            return state == 0x02 || state == 0x06;
        }

        private static void SetEnabled(RegistryKey hive, string subKeyPath, string valueName, bool enabled)
        {
            using var key = hive.CreateSubKey(subKeyPath, writable: true)
                ?? throw new InvalidOperationException($"Could not open or create '{subKeyPath}'.");

            var bytes = new byte[12];
            if (enabled)
            {
                bytes[0] = 0x02;
            }
            else
            {
                bytes[0] = 0x03;
                long fileTime = DateTime.UtcNow.ToFileTimeUtc();
                BitConverter.GetBytes(fileTime).CopyTo(bytes, 4);
            }

            key.SetValue(valueName, bytes, RegistryValueKind.Binary);
        }
    }
}
