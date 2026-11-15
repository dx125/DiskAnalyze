using DiskAnalyze.Services;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DiskAnalyze.Converters
{
    /// <summary>
    /// Fill of a diagram segment, or with ConverterParameter "Label" the text colour for a label drawn on
    /// that fill. Bindings: [0] ColorIndex (int), [1] IsOtherCategory (bool), [2] IsHovered (bool, optional),
    /// [3] <see cref="ThemeManager.ActualTheme"/> (optional). Being a MultiBinding, it re-evaluates when
    /// IsHovered or the theme changes, which a plain <c>{Binding}</c> to the entity object never does.
    /// Colours come from the active theme's Color.Series0…7 / Color.Other tokens.
    /// </summary>
    public class EntityBrushConverter : IMultiValueConverter
    {
        private const int SlotCount = 8;
        private const int OtherSlot = -1;

        private readonly Dictionary<(AppTheme Theme, int Slot, bool Label, bool Hovered), Brush> _cache = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            int index = values.Length > 0 && values[0] is int i ? i : 0;
            bool isOther = values.Length > 1 && values[1] is bool other && other;
            bool isHovered = values.Length > 2 && values[2] is bool hovered && hovered;
            bool isLabel = parameter as string == "Label";

            // Bars list every item, so ranks past the palette wrap around; the pie never gets that far
            // (AppSettings.MaxPieSlices).
            int slot = isOther ? OtherSlot : (index % SlotCount + SlotCount) % SlotCount;
            var theme = ThemeManager.Instance.ActualTheme;
            bool hoverFill = isHovered && !isLabel;
            var key = (theme, slot, isLabel, hoverFill);

            if (!_cache.TryGetValue(key, out var brush))
            {
                brush = CreateBrush(theme, slot, isLabel, hoverFill);
                _cache[key] = brush;
            }

            return brush;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static Brush CreateBrush(AppTheme theme, int slot, bool isLabel, bool isHovered)
        {
            string key = (isLabel, slot) switch
            {
                (true, OtherSlot) => "Color.OtherLabel",
                (true, _) => $"Color.SeriesLabel{slot}",
                (false, OtherSlot) => "Color.Other",
                _ => $"Color.Series{slot}"
            };

            var color = System.Windows.Application.Current?.TryFindResource(key) is Color c ? c : Colors.Gray;

            // Hover moves the fill away from the background: lighter on dark, darker on light.
            if (isHovered)
            {
                color = theme == AppTheme.Dark ? Mix(color, Colors.White, 0.22) : Mix(color, Colors.Black, 0.15);
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color Mix(Color color, Color target, double amount) => Color.FromRgb(
            (byte)Math.Round(color.R + (target.R - color.R) * amount),
            (byte)Math.Round(color.G + (target.G - color.G) * amount),
            (byte)Math.Round(color.B + (target.B - color.B) * amount));
    }
}
