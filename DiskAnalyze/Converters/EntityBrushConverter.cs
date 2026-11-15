using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DiskAnalyze.Converters
{
    /// <summary>
    /// Fill for a diagram segment. Bindings: [0] ColorIndex (int), [1] IsOtherCategory (bool),
    /// [2] optional IsHovered (bool). Being a MultiBinding, it re-evaluates when IsHovered changes,
    /// which a plain <c>{Binding}</c> to the entity object never does.
    /// </summary>
    public class EntityBrushConverter : IMultiValueConverter
    {
        // Indexed by size rank. The pie shows ranks 0-4, which are always distinct;
        // the tones are light enough for the black labels drawn on top.
        private static readonly Color[] Palette =
        {
            Color.FromRgb(0x5B, 0x9B, 0xD5), // blue
            Color.FromRgb(0xF2, 0x8E, 0x2B), // orange
            Color.FromRgb(0x6C, 0xBF, 0x5A), // green
            Color.FromRgb(0xE1, 0x57, 0x59), // red
            Color.FromRgb(0xB0, 0x7A, 0xA1), // purple
            Color.FromRgb(0xED, 0xC9, 0x48), // yellow
            Color.FromRgb(0x76, 0xB7, 0xB2), // teal
            Color.FromRgb(0xFF, 0x9D, 0xA7)  // pink
        };

        private static readonly Color OtherColor = Color.FromRgb(0xB0, 0xB0, 0xB0);

        private static readonly SolidColorBrush[] NormalBrushes = Palette.Select(CreateFrozen).ToArray();
        private static readonly SolidColorBrush[] HoveredBrushes = Palette.Select(c => CreateFrozen(Brighten(c))).ToArray();
        private static readonly SolidColorBrush OtherBrush = CreateFrozen(OtherColor);
        private static readonly SolidColorBrush OtherHoveredBrush = CreateFrozen(Brighten(OtherColor));

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            int index = values.Length > 0 && values[0] is int i ? i : 0;
            bool isOther = values.Length > 1 && values[1] is bool other && other;
            bool isHovered = values.Length > 2 && values[2] is bool hovered && hovered;

            if (isOther)
            {
                return isHovered ? OtherHoveredBrush : OtherBrush;
            }

            int slot = (index % Palette.Length + Palette.Length) % Palette.Length;
            return isHovered ? HoveredBrushes[slot] : NormalBrushes[slot];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static Color Brighten(Color color) => Color.FromRgb(
            (byte)Math.Min(255, color.R + 40),
            (byte)Math.Min(255, color.G + 40),
            (byte)Math.Min(255, color.B + 40));

        private static SolidColorBrush CreateFrozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
