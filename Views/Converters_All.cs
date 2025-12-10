using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DesignSheet.Models;

namespace DesignSheet.Views
{
    // RETAIL (Y/N) -> background
    public sealed class RetailBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var v = (value as string)?.Trim().ToUpperInvariant();
            return v == "Y"
                ? new SolidColorBrush(Color.FromRgb(198, 239, 206))  // light green
                : Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // OE (Y/N/A/L) -> background
    public sealed class OeBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var v = (value as string)?.Trim().ToUpperInvariant();

            return v switch
            {
                "Y" => new SolidColorBrush(Color.FromRgb(189, 215, 238)),  // blue-ish
                "N" => Brushes.Transparent,
                "A/L" => new SolidColorBrush(Color.FromRgb(255, 242, 204)),  // light yellow
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // DAY DUE (mon–fri) -> background
    public sealed class DayDueBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var v = (value as string)?.Trim().ToLowerInvariant();

            return v switch
            {
                "mon" => new SolidColorBrush(Color.FromRgb(221, 235, 247)),
                "tues" => new SolidColorBrush(Color.FromRgb(221, 235, 247)),
                "wed" => new SolidColorBrush(Color.FromRgb(221, 235, 247)),
                "thur" => new SolidColorBrush(Color.FromRgb(221, 235, 247)),
                "fri" => new SolidColorBrush(Color.FromRgb(221, 235, 247)),
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // DATE DUE -> red / yellow / green
    public sealed class DueDateToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            if (string.IsNullOrWhiteSpace(text))
                return Brushes.Transparent;

            if (!DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
                return Brushes.Transparent;

            var today = DateTime.Today;

            if (date.Date < today)
                return new SolidColorBrush(Color.FromRgb(255, 199, 206)); // red-ish
            if (date.Date == today)
                return new SolidColorBrush(Color.FromRgb(255, 235, 156)); // yellow-ish

            return new SolidColorBrush(Color.FromRgb(198, 239, 206));     // green-ish
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // STATUS -> background
    public sealed class StatusBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var v = (value as string)?.Trim().ToLowerInvariant() ?? "";

            return v switch
            {
                "quote" => new SolidColorBrush(Color.FromRgb(221, 235, 247)),
                "picking" => new SolidColorBrush(Color.FromRgb(252, 228, 214)),
                "assembly" => new SolidColorBrush(Color.FromRgb(255, 242, 204)),
                "balancing" => new SolidColorBrush(Color.FromRgb(226, 239, 218)),
                "booked in" => new SolidColorBrush(Color.FromRgb(226, 239, 218)),
                "paint shop" => new SolidColorBrush(Color.FromRgb(234, 153, 153)),
                "completed" => new SolidColorBrush(Color.FromRgb(198, 239, 206)),
                "picked up" => new SolidColorBrush(Color.FromRgb(189, 215, 238)),
                "cancelled" => new SolidColorBrush(Color.FromRgb(191, 191, 191)),
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // SHAFT -> background
    public sealed class ShaftBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var v = (value as string)?.Trim().ToLowerInvariant();
            return v switch
            {
                "domestic" => new SolidColorBrush(Color.FromRgb(221, 235, 247)),
                "industrial" => new SolidColorBrush(Color.FromRgb(237, 201, 175)),
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // PRIORITY (Y) -> whole row pink
    public sealed class PriorityRowBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkRowView view && !view.IsSeparator && view.Row != null)
            {
                var v = (view.Row.PRIORITY ?? "").Trim().ToUpperInvariant();
                if (v == "Y")
                {
                    return new SolidColorBrush(Color.FromRgb(255, 192, 203)); // pink
                }
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // Separator row enabled/disabled
    public sealed class SeparatorEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSeparator)
                return !isSeparator;

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // CUSTOMER cell sand when SHAFT = industrial
    public sealed class CustomerShaftBrushConverter : IValueConverter
    {
        private static readonly Brush SandBrush =
            new SolidColorBrush(Color.FromRgb(237, 201, 175)); // sand colour

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var shaft = (value as string)?.Trim() ?? string.Empty;

            if (shaft.Equals("industrial", StringComparison.OrdinalIgnoreCase))
                return SandBrush;

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
