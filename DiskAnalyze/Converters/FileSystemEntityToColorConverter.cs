using DiskAnalyze.ViewModels;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DiskAnalyze.Converters
{
    public class FileSystemEntityToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush[] PredefinedBrushes = new[]
        {
            CreateFrozen(Colors.DodgerBlue),
            CreateFrozen(Colors.OrangeRed),
            CreateFrozen(Colors.MediumSeaGreen),
            CreateFrozen(Colors.Purple),
            CreateFrozen(Colors.Gold),
            CreateFrozen(Colors.IndianRed),
            CreateFrozen(Colors.SlateBlue),
            CreateFrozen(Colors.Gray) // For "Other" category
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FileSystemEntityViewModel entity)
            {
                // Use the last brush for "Other" category
                if (entity.IsOtherCategory)
                {
                    return PredefinedBrushes[PredefinedBrushes.Length - 1];
                }

                // Pick a color from the entity name; string.GetHashCode is randomized
                // per process, so use a stable hash to keep colors consistent across runs
                int index = (GetStableHash(entity.Name) & int.MaxValue) % (PredefinedBrushes.Length - 1);
                return PredefinedBrushes[index];
            }

            return PredefinedBrushes[0];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static int GetStableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                {
                    hash = hash * 31 + c;
                }

                return hash;
            }
        }

        private static SolidColorBrush CreateFrozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
