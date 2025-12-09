using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DesignSheet
{
    public class DueDateToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime date)
                return Brushes.White;

            var today = DateTime.Today;

            if (date.Date < today)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fecaca")); // red-ish
            if (date.Date == today)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fef3c7")); // yellow-ish
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dcfce7"));     // green-ish
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
