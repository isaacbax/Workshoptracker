using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DesignSheet
{
    public class RetailOeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string v = (value as string ?? "").ToUpperInvariant();
            return v switch
            {
                "Y" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f97316")), // orange
                "N" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#facc15")), // yellow
                "Z" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444")), // red
                "A/L" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#93c5fd")), // light blue
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
