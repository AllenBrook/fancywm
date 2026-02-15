using System.Windows;

using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal class LengthThicknessConverter : ICssValueConverter<Length, Thickness>
    {
        public Thickness Convert(Length cssValue)
        {
            var r = ConverterRegistry.Instance;
            return new Thickness(r.Convert<double>(cssValue));
        }
    }
}
