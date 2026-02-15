using System.Windows.Media;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    public class PrimitiveValueFontFamilyConverter : ICssValueConverter<AngleSharp.Css.Values.ICssPrimitiveValue, FontFamily>
    {
        public FontFamily Convert(AngleSharp.Css.Values.ICssPrimitiveValue cssValue)
        {
            return new FontFamily(cssValue.CssText.Trim('"'));
        }
    }
}
