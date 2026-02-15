using System.Windows.Media;
using System.Windows.Media.Effects;

using FancyWM.ThemeEngine.Extensions;


namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal class DropShadowFilterValueDropShadowEffectConverter : ICssValueConverter<CssDropShadowFilterValue, DropShadowEffect?>
    {
        public DropShadowEffect? Convert(CssDropShadowFilterValue cssValue)
        {
            var r = ConverterRegistry.Instance;
            return CssBoxShadowToDropShadowEffect(
                r.Convert<double>(cssValue.OffsetX), r.Convert<double>(cssValue.OffsetY),
                cssValue.BlurRadius is null ? 0 : r.Convert<double>(cssValue.BlurRadius),
                r.Convert<Color>(cssValue.Color));
        }

        private static DropShadowEffect CssBoxShadowToDropShadowEffect(double hOffset, double vOffset,
            double blur, Color color)
        {
            var effect = new DropShadowEffect
            {
                Color = color,
                BlurRadius = Math.Max(0, blur),
                RenderingBias = RenderingBias.Performance
            };

            effect.Direction = AngleFromOffsets(hOffset, vOffset);
            effect.ShadowDepth = Math.Sqrt(hOffset * hOffset + vOffset * vOffset);
            effect.Opacity = color.A / 255.0;

            return effect;
        }

        private static double AngleFromOffsets(double h, double v)
        {
            if (h == 0 && v == 0) return 0;
            double angle = Math.Atan2(v, h) * 180 / Math.PI;
            return -((angle + 360) % 360);
        }
    }
}
