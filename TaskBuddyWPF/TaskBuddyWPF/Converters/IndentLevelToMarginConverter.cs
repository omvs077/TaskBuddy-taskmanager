using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskBuddyWPF.Converters
{
    public class IndentLevelToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int level = value is int i ? i : 0;
            return new Thickness(level * 20, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
