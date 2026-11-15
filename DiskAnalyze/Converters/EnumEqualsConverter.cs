using System.Globalization;
using System.Windows.Data;

namespace DiskAnalyze.Converters
{
    /// <summary>
    /// Binds a group of radio buttons to one enum property: <c>IsChecked="{Binding Theme,
    /// Converter={StaticResource EnumEqualsConverter}, ConverterParameter=Dark}"</c>.
    /// Checking a button writes its value back; unchecking writes nothing.
    /// </summary>
    public class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value != null && parameter is string name && string.Equals(value.ToString(), name, StringComparison.Ordinal);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true && parameter is string name ? Enum.Parse(targetType, name) : Binding.DoNothing;
    }
}
