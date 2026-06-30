using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DiskAnalyze.Converters
{
    /// <summary>
    /// Maps a boolean to a <see cref="GridLength"/>: the <see cref="ConverterParameter"/> width
    /// (pixels, default 220) when true, and zero when false. Lets a grid column collapse entirely
    /// so the remaining columns reclaim its space.
    /// </summary>
    public class BooleanToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool val && val;

            if (!boolValue)
                return new GridLength(0);

            double width = 220;
            if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                width = parsed;
            }

            return new GridLength(width);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
