using System.Windows.Media;

using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal class BackgroundValueBrushConverter : ICssValueConverter<CssBackgroundValue, Brush?>
    {
        public Brush? Convert(CssBackgroundValue cssValue)
        {
            var color = cssValue.Color;
            var layers = cssValue.Layers;
            if (color is not null && layers is null)
            {
                return ConverterRegistry.Instance.Convert<Brush>(color);
            }
            if (layers is not null)
            {
                return ConverterRegistry.Instance.Convert<Brush>(layers);
            }
            return null;
        }
    }
}
