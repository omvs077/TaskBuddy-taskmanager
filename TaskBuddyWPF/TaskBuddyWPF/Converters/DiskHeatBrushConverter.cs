using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TaskBuddyWPF.Converters
{
    // NOTE: disk throughput has no natural 0-100% ceiling per process, unlike CPU/RAM.
    // Uses a fixed approximate ceiling (20 MB/s = full intensity) as a visual heuristic.
    public class DiskHeatBrushConverter : IValueConverter
    {
        private const double CeilingBytesPerSec = 20.0 * 1024 * 1024;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double bytesPerSec = value is double d ? d : 0;
            if (bytesPerSec < 0.05 * 1024 * 1024) return Brushes.Transparent;
            double alpha = Math.Clamp(bytesPerSec / CeilingBytesPerSec, 0, 1) * 0.85 + 0.15;
            return new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 255, 110, 20));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
