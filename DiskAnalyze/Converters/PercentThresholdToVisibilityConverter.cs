using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DiskAnalyze.Converters
{
    /// <summary>
    /// Visible when a percentage reaches the threshold given as ConverterParameter (default 4).
    /// Hides labels and markers on segments too small to fit them.
    /// </summary>
    public class PercentThresholdToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double threshold = 4;
            if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                threshold = parsed;
            }

            return value is double percent && percent >= threshold ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
