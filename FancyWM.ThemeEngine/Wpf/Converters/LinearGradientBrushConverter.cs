using System.Windows;
using System.Windows.Media;

using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    public class LinearGradientBrushConverter : ICssValueConverter<CssLinearGradientValue, Brush?>
    {
        public Brush? Convert(CssLinearGradientValue cssValue)
        {
            if (cssValue.IsRepeating || cssValue.Angle is not Angle)
            {
                return null;
            }

            var gradientBrush = new LinearGradientBrush();
            var angle = (Angle)cssValue.Angle;

            gradientBrush.StartPoint = new(0, 1);
            gradientBrush.EndPoint = new(0, 0);
            gradientBrush.SpreadMethod = GradientSpreadMethod.Pad;
            gradientBrush.MappingMode = BrushMappingMode.RelativeToBoundingBox;
            gradientBrush.GradientStops = cssValue.Stops.ToGradientStopCollection();
            gradientBrush.RelativeTransform = new RotateTransform(angle.ToDeg(), 0.5, 0.5);

            return gradientBrush.CreateUniformDrawingBrush();
        }
    }
}
