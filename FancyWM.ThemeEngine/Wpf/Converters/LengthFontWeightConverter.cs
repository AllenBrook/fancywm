using System.Windows;

using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    public class LengthFontWeightConverter : ICssValueConverter<Length, FontWeight>
    {
        public FontWeight Convert(Length cssValue)
        {
            return FontWeight.FromOpenTypeWeight((int)cssValue.Value);
        }
    }
}
