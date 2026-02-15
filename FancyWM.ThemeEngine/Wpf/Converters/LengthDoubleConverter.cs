using AngleSharp.Css.Dom;
using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal class LengthDoubleConverter : ICssValueConverter<Length, double>
    {
        public double Convert(Length cssValue)
        {
            return cssValue.AsPx(null, AngleSharp.Css.RenderMode.Undefined);
        }
    }
}
