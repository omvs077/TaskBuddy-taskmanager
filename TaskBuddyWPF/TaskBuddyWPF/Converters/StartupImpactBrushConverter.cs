using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TaskBuddyWPF.Models;

namespace TaskBuddyWPF.Converters
{
    public class StartupImpactBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var impact = value is StartupImpact i ? i : StartupImpact.NotMeasured;
            return impact switch
            {
                StartupImpact.High => new SolidColorBrush(Color.FromArgb(60, 232, 65, 65)),
                StartupImpact.Medium => new SolidColorBrush(Color.FromArgb(60, 232, 160, 30)),
                StartupImpact.Low => new SolidColorBrush(Color.FromArgb(45, 90, 200, 90)),
                _ => Brushes.Transparent // NotMeasured, None
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
