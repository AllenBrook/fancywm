using System.Windows;

using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    public class BorderRadiusCornerRadiusConverter : ICssValueConverter<CssBorderRadiusValue, CornerRadius>
    {
        public CornerRadius Convert(CssBorderRadiusValue cssValue)
        {
            var r = ConverterRegistry.Instance;
            var value = cssValue.Horizontal;
            return new CornerRadius(
                r.Convert<double>(value.Top), r.Convert<double>(value.Right),
                r.Convert<double>(value.Bottom), r.Convert<double>(value.Left));
        }
    }
}
