using System.Windows;

using AngleSharp.Css.Dom;
using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal class PeriodicValueThicknessConverter : ICssValueConverter<CssPeriodicValue<ICssValue>, Thickness>
    {
        public Thickness Convert(CssPeriodicValue<ICssValue> cssValue)
        {
            var r = ConverterRegistry.Instance;
            return new Thickness(r.Convert<double>(cssValue.Left), r.Convert<double>(cssValue.Top),
                                 r.Convert<double>(cssValue.Right), r.Convert<double>(cssValue.Bottom));
        }
    }
}
