using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp.Css.Values;
using AngleSharp.Text;

namespace FancyWM.ThemeEngine.Extensions
{
    public sealed class CssDropShadowFilterValue : ICssValue
    {
        public const string FunctionName = "drop-shadow";
        public required string CssText { get; init; }
        public ICssValue? OffsetX { get; init; }
        public ICssValue? OffsetY { get; init; }
        public ICssValue? BlurRadius { get; init; }
        public Color? Color { get; init; }
    }

    public sealed class DropShadowConverter : IValueConverter
    {
        public ICssValue? Convert(StringSource source)
        {
            var pos = source.Index;

            var ident = source.ParseIdent();
            if (ident == null || !ident.Isi(CssDropShadowFilterValue.FunctionName) || source.Current != Symbols.RoundBracketOpen)
            {
                source.BackTo(pos);
                return null;
            }

            source.SkipCurrentAndSpaces();

            var offsetX = source.ParseLength();
            if (offsetX == null) { source.BackTo(pos); return null; }
            source.SkipSpacesAndComments();

            var offsetY = source.ParseLength();
            if (offsetY == null) { source.BackTo(pos); return null; }
            source.SkipSpacesAndComments();

            ICssValue? blur = source.ParseLength();
            if (blur != null) source.SkipSpacesAndComments();

            Color? color = null;
            if (source.Current != Symbols.RoundBracketClose)
            {
                color = source.ParseColor();
                source.SkipSpacesAndComments();
            }

            if (source.Current != Symbols.RoundBracketClose)
            {
                source.BackTo(pos);
                return null;
            }

            source.SkipCurrentAndSpaces();
            return new CssDropShadowFilterValue
            {
                CssText = source.Content,
                OffsetX = offsetX,
                OffsetY = offsetY,
                BlurRadius = blur,
                Color = color,
            };
        }

        public ICssValue? Collect(IEnumerable<ICssProperty> properties) => null;
    }
}