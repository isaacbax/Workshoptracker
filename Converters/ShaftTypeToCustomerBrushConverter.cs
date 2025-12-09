using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DesignSheet
{
    public class ShaftTypeToCustomerBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string v = (value as string ?? "").ToLowerInvariant();
            if (v == "industrial")
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f5f5dc")); // sand
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
