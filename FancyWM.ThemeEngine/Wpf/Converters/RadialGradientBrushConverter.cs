using System.Windows.Media;

using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    public class RadialGradientBrushConverter : ICssValueConverter<CssRadialGradientValue, Brush?>
    {
        public Brush? Convert(CssRadialGradientValue cssValue)
        {
            if (cssValue.IsRepeating || cssValue.Mode != CssRadialGradientValue.SizeMode.None)
            {
                return null;
            }
            
            var gradientBrush = new RadialGradientBrush();
            System.Windows.Point center = new(cssValue.Position.X.AsPercent() ?? 0, cssValue.Position.Y.AsPercent() ?? 0);
            gradientBrush.Center = center;
            gradientBrush.GradientOrigin = center;

            gradientBrush.SpreadMethod = cssValue.IsRepeating ? GradientSpreadMethod.Repeat : GradientSpreadMethod.Pad;
            gradientBrush.RadiusX = cssValue.MajorRadius?.AsPercent() ?? 1;
            gradientBrush.RadiusY = cssValue.MinorRadius?.AsPercent() ?? 1;
            gradientBrush.GradientStops = cssValue.Stops.ToGradientStopCollection();

            return cssValue.IsCircle ? gradientBrush.CreateUniformDrawingBrush() : gradientBrush;
        }
    }
}
