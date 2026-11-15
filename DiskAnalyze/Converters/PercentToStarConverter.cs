using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DiskAnalyze.Converters
{
    /// <summary>
    /// Maps a percentage (0-100) to a star <see cref="GridLength"/>, so a bar fills that share of the
    /// available width at any window size. <c>ConverterParameter=Remainder</c> yields the complementary
    /// share (100 - percent) for the empty part of the track.
    /// </summary>
    public class PercentToStarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = value is double d && !double.IsNaN(d) ? Math.Clamp(d, 0, 100) : 0;
            bool remainder = parameter is string text && text == "Remainder";

            return new GridLength(remainder ? 100 - percent : percent, GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
