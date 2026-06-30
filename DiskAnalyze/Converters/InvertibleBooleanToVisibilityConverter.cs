using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DiskAnalyze.Converters
{
    // Unlike the built-in BooleanToVisibilityConverter, supports inversion via ConverterParameter=Invert
    public class InvertibleBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter != null && parameter.ToString() == "Invert";
            bool boolValue = value is bool val && val;

            return (boolValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter != null && parameter.ToString() == "Invert";
            bool visible = value is Visibility vis && vis == Visibility.Visible;

            return visible ^ invert;
        }
    }
}
