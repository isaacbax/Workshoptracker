using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WorkshopTracker.Converters
{
    /// <summary>
    /// Converts a DateTime (DateDue) to a Brush:
    /// - Before today  => Red
    /// - Today         => Yellow
    /// - After today   => LightGreen
    /// - Null/invalid  => Transparent
    /// </summary>
    public class DateDueToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == System.Windows.DependencyProperty.UnsetValue)
                return Brushes.Transparent;

            DateTime date;

            if (value is DateTime dt)
            {
                date = dt.Date;
            }
            else if (!DateTime.TryParse(value.ToString(), out date))
            {
                return Brushes.Transparent;
            }

            var today = DateTime.Today;

            if (date < today)
                return Brushes.Red;

            if (date == today)
                return Brushes.Yellow;

            return Brushes.LightGreen;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way converter – not needed in reverse
            return Binding.DoNothing;
        }
    }
}
