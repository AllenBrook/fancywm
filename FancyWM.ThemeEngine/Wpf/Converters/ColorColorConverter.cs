using System.Windows.Media;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal class ColorColorConverter : ICssValueConverter<AngleSharp.Css.Values.Color, Color>
    {
        public Color Convert(AngleSharp.Css.Values.Color color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}
