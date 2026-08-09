using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TaskBuddyWPF.Services;

namespace TaskBuddyWPF.Converters
{
    public class MemoryHeatBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ulong bytes = value is ulong u ? u : 0;
            double percent = SystemInfo.TotalPhysicalMemoryBytes > 0
                ? (double)bytes / SystemInfo.TotalPhysicalMemoryBytes * 100.0
                : 0;
            if (percent < 1.0) return Brushes.Transparent;
            double alpha = Math.Clamp((percent - 1.0) / 29.0, 0, 1) * 0.85 + 0.15;
            return new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 255, 110, 20));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
