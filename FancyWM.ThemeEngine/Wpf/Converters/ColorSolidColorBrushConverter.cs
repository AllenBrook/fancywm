using System.Windows.Media;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal class ColorSolidColorBrushConverter : ICssValueConverter<AngleSharp.Css.Values.Color, SolidColorBrush>
    {
        public SolidColorBrush Convert(AngleSharp.Css.Values.Color color)
        {
            var brush = new SolidColorBrush(ConverterRegistry.Instance.Convert<Color>(color));
            return brush;
        }
    }
}
