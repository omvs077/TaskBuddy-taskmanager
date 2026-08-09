using System;
using System.Globalization;
using System.Windows.Data;

namespace TaskBuddyWPF.Converters
{
    public class BytesPerSecToMBpsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double bytesPerSec)
                return (bytesPerSec / 1024.0 / 1024.0).ToString("F1");
            return "0.0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
