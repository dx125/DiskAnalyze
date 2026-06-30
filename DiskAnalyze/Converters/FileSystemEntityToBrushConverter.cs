using DiskAnalyze.ViewModels;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DiskAnalyze.Converters
{
    public class FileSystemEntityToBrushConverter : IValueConverter
    {
        private readonly FileSystemEntityToColorConverter _colorConverter = new FileSystemEntityToColorConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FileSystemEntityViewModel entity)
            {
                SolidColorBrush baseBrush = (SolidColorBrush)_colorConverter.Convert(entity, targetType, parameter, culture);

                if (entity.IsHovered)
                {
                    // Make the color brighter for hover
                    Color baseColor = baseBrush.Color;
                    Color brightColor = Color.FromArgb(
                        baseColor.A,
                        (byte)Math.Min(255, baseColor.R + 40),
                        (byte)Math.Min(255, baseColor.G + 40),
                        (byte)Math.Min(255, baseColor.B + 40)
                    );
                    var hoverBrush = new SolidColorBrush(brightColor);
                    hoverBrush.Freeze();
                    return hoverBrush;
                }

                return baseBrush;
            }

            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
