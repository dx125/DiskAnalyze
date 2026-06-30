using DiskAnalyze.Core;
using System.Globalization;
using System.Windows.Data;

namespace DiskAnalyze.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is long bytes ? FileSizeFormatter.Format(bytes) : FileSizeFormatter.Format(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
