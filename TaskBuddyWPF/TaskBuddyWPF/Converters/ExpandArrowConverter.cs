using System;
using System.Globalization;
using System.Windows.Data;

namespace TaskBuddyWPF.Converters
{
    public class ExpandArrowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool expanded && expanded ? "\u25BC" : "\u25B6"; // ▼ expanded / ▶ collapsed

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
