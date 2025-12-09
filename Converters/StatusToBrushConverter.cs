using System;                      // ✅ REQUIRED (Type, NotImplementedException)
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DesignSheet
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = (value as string ?? "").ToLowerInvariant();

            return status switch
            {
                "quote" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3b82f6")),          // blue
                "assembly" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400e")),       // brown
                "balancing" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7c3aed")),      // purple
                "booked in" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ca3af")),      // grey
                "drive shop" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ec4899")),     // pink
                "on hold" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444")),        // red
                "completed" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#facc15")),      // yellow
                "paint shop" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fb923c")),     // orange
                "picking" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f97316")),        // orange
                "quoting" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#a855f7")),        // lavender-ish
                "picked up" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14b8a6")),      // teal
                "cancelled" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444")),      // red
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b7280"))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
