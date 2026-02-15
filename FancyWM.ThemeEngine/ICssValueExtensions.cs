using AngleSharp.Css.Dom;
using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine
{
    internal static class ICssValueExtensions
    {
        public static double? AsPercent(this ICssValue value)
        {
            if (value is Length { Type: Length.Unit.Percent } length)
            {
                return length.Value / 100.0;
            }

            if (value is ICssMultipleValue { Count: 1 } cssMultipleValue)
            {
                return cssMultipleValue[0].AsPercent();
            }

            if (value is ICssSpecialValue { Value: not null } cssSpecialValue)
            {
                return cssSpecialValue.Value.AsPercent();
            }

            return null;
        }

        public static double ToDeg(this Angle angle)
        {
            double degrees = angle.ToRadian() * (180.0 / Math.PI);
            return (degrees % 360 + 360) % 360;
        }
    }
}
