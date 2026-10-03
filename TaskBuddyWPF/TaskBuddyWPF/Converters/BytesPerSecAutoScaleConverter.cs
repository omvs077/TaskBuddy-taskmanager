using System;
using System.Globalization;
using System.Windows.Data;

namespace TaskBuddyWPF.Converters
{
    // Auto-scales to B/s, KB/s, or MB/s depending on magnitude, so small
    // real values (e.g. background sync traffic) remain visible instead of
    // rounding to "0.0" the way a fixed MB/s-only display would.
    public class BytesPerSecAutoScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double bytesPerSec) return "0 B/s";
            if (bytesPerSec < 1024)
                return $"{bytesPerSec:F0} B/s";
            if (bytesPerSec < 1024 * 1024)
                return $"{bytesPerSec / 1024.0:F1} KB/s";
            return $"{bytesPerSec / 1024.0 / 1024.0:F2} MB/s";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
