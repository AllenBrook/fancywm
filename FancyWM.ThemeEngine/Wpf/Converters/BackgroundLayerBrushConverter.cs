using System.Windows.Media;

using AngleSharp.Css.Dom;
using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal class BackgroundLayerBrushConverter : ICssValueConverter<CssBackgroundLayerValue, Brush?>
    {
        public Brush? Convert(CssBackgroundLayerValue cssValue)
        {
            if (cssValue.Clip is not null
                || cssValue.Attachment is not null 
                || cssValue.Origin is not null
                || cssValue.Position is not null)
            {
                return null;
            }

            var r = ConverterRegistry.Instance;

            bool isGradientSupported = cssValue.Repeat is null && cssValue.Position is null && cssValue.Size is null;
            if (cssValue.Image is CssLinearGradientValue linear)
            {
                return isGradientSupported ? r.Convert<Brush>(linear) : null;
            }
            if (cssValue.Image is CssRadialGradientValue radial)
            {
                return isGradientSupported ? r.Convert<Brush>(radial) : null;
            }

            var repeat = cssValue.Repeat as CssImageRepeatsValue;
            var horizontalRepeat = (repeat?.Horizontal as Constant<BackgroundRepeat>?)?.Value ?? BackgroundRepeat.Repeat;
            var verticalRepeat = (repeat?.Vertical as Constant<BackgroundRepeat>?)?.Value ?? BackgroundRepeat.Repeat;

            var brush = r.Convert<ImageBrush>(cssValue.Image);

            brush.Viewport = new(0, 0, brush.ImageSource.Width, brush.ImageSource.Height);
            brush.ViewportUnits = BrushMappingMode.Absolute;

            if (cssValue.Size is CssBackgroundSizeValue size)
            {
                brush.Viewport = new System.Windows.Rect(0, 0, r.Convert<double>(size.Width), r.Convert<double>(size.Height));
                brush.ViewportUnits = BrushMappingMode.Absolute;
                brush.Stretch = Stretch.Fill;
            }

            TileMode tileMode;
            if (horizontalRepeat == BackgroundRepeat.NoRepeat &&
                verticalRepeat == BackgroundRepeat.NoRepeat)
            {
                tileMode = TileMode.None;
            }
            else if (horizontalRepeat == BackgroundRepeat.Repeat &&
                verticalRepeat == BackgroundRepeat.Repeat)
            {
                tileMode = TileMode.Tile;
            }
            else
            {
                return null;
            }
            brush.TileMode = tileMode;
            brush.AlignmentX = AlignmentX.Left;
            brush.AlignmentY = AlignmentY.Top;

            return brush;
        }
    }
}
