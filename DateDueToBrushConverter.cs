using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DesignSheet
{
    public class DateDueToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Brushes.White;

            DateTime date;

            if (value is DateTime dt)
            {
                date = dt;
            }
            else
            {
                if (!DateTime.TryParseExact(
                        value.ToString(),
                        "dd/MM/yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out date))
                {
                    return Brushes.White;
                }
            }

            var today = DateTime.Today;

            if (date.Date < today)
                return Brushes.Red;          // overdue
            if (date.Date == today)
                return Brushes.Yellow;       // due today
            return Brushes.LightGreen;       // future
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
