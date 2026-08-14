using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaskBuddyWPF.Native;

namespace TaskBuddyWPF.Services
{
    // Shared icon-extraction helper, mirrors ProcessEnumerator's ResolveIcon pattern.
    // Cache keyed by path — an exe's icon never changes, so never pruned.
    public static class IconHelper
    {
        private static readonly Dictionary<string, ImageSource> _iconCache = new();
        private static ImageSource? _defaultIcon;

        public static ImageSource? DefaultIcon
        {
            get
            {
                if (_defaultIcon == null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri("pack://application:,,,/DefaultProcessIcon.png");
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    _defaultIcon = bitmap;
                }
                return _defaultIcon;
            }
        }

        // path may be null/empty (e.g. Task Scheduler entries with no filesystem path) —
        // returns DefaultIcon in that case rather than throwing.
        public static ImageSource? ResolveIcon(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return DefaultIcon;
            if (_iconCache.TryGetValue(path, out var cached)) return cached;

            var shfi = new SHFILEINFO();
            IntPtr result = NativeMethods.SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_SMALLICON);
            if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return DefaultIcon;

            try
            {
                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmapSource.Freeze();
                _iconCache[path] = bitmapSource;
                return bitmapSource;
            }
            catch
            {
                return DefaultIcon; // protected/system paths can throw on extraction — non-fatal
            }
            finally
            {
                NativeMethods.DestroyIcon(shfi.hIcon);
            }
        }

        // Startup-entry Commands are raw registry/shortcut strings, not clean paths —
        // may be quoted ("C:\...\App.exe"), unquoted (C:\...\App.exe), or have trailing
        // arguments ("C:\...\App.exe" --flag). Extracts just the executable path.
        public static string? ExtractExePath(string? command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            command = command.Trim();

            if (command.StartsWith("\""))
            {
                var end = command.IndexOf('"', 1);
                return end > 1 ? command.Substring(1, end - 1) : null;
            }

            var exeIdx = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0) return command.Substring(0, exeIdx + 4);

            return command; // fallback: use as-is (e.g. .lnk paths, no args expected)
        }
    }
}
