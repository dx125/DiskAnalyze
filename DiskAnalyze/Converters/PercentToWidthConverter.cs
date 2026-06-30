using System.Globalization;
using System.Windows.Data;

namespace DiskAnalyze.Converters
{
    /// <summary>
    /// Maps a percentage (0-100) to a pixel width. <see cref="Offset"/> allows reusing the same
    /// converter to position elements relative to the resulting width (e.g. an icon at the bar's end).
    /// </summary>
    public class PercentToWidthConverter : IValueConverter
    {
        public double MaxWidth { get; set; } = 500;

        public double Offset { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent)
            {
                return Math.Max(0, percent / 100 * MaxWidth + Offset);
            }

            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
