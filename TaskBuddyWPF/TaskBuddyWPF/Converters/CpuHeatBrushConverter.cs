using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TaskBuddyWPF.Converters
{
    public class CpuHeatBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = value is double d ? d : 0;
            if (percent < 3.0) return Brushes.Transparent;
            double alpha = Math.Clamp((percent - 3.0) / 97.0, 0, 1) * 0.85 + 0.15;
            return new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 255, 110, 20));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
