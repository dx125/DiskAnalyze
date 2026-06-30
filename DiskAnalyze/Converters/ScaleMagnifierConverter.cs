using System.Globalization;
using System.Windows.Data;

namespace DiskAnalyze.Converters
{
    public class ScaleMagnifierConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isHovered && isHovered)
            {
                if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double scaleValue))
                {
                    return scaleValue;
                }
                return 1.1; // Default 10% magnification
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
