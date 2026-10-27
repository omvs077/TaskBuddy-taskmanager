using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskBuddyWPF.Converters
{
    // Converts a 0-100 percent value to a Star GridLength so a two-column Grid
    // (filled / remainder) can render a simple percentage bar without needing
    // to know the container's actual pixel width. Pass ConverterParameter
    // "invert" for the remainder column.
    public class PercentToStarWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = value is double d ? Math.Clamp(d, 0, 100) : 0;
            bool invert = (parameter as string) == "invert";
            double star = invert ? (100 - percent) : percent;
            return new GridLength(Math.Max(0.01, star), GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
