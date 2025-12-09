using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DesignSheet
{
    public class PriorityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string priority = (value as string ?? "").ToUpperInvariant();
            return priority switch
            {
                "Y" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22c55e")), // green
                "N" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444")), // red
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
