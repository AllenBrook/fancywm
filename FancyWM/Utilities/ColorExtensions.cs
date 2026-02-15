using System.Windows.Media;

namespace FancyWM.Utilities
{
    internal static class ColorExtensions
    {
        public static Color WithOpacity(this Color color, double opacity)
        {
            return Color.FromArgb((byte)(color.A * opacity), color.R, color.G, color.B);
        }

        public static string ToCss(this Color color)
        {
            if (color == Colors.White)
            {
                return "white";
            }
            if (color == Colors.Black)
            {
                return "black";
            }
            if (color.A == 255)
            {
                return $"rgb({color.R}, {color.G}, {color.B})";
            }
            return $"rgba({color.R}, {color.G}, {color.B}, {color.A / 255.0:F})";
        }
    }
}
