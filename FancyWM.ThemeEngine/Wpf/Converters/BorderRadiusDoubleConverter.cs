using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    public class BorderRadiusDoubleConverter : ICssValueConverter<CssBorderRadiusValue, double>
    {
        public double Convert(CssBorderRadiusValue cssValue)
        {
            return ConverterRegistry.Instance.Convert<double>(cssValue.Horizontal.Top);
        }
    }
}
