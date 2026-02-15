using System.Windows.Media;
using System.Windows.Media.Imaging;

using AngleSharp.Css.Dom;
using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal class UrlValueImageBrushConverter : ICssValueConverter<CssUrlValue, ImageBrush>
    {
        public ImageBrush Convert(CssUrlValue cssValue)
        {
            var bitmap = new BitmapImage(new Uri(cssValue.AsUrl(), UriKind.RelativeOrAbsolute));
            var brush = new ImageBrush(bitmap) { Stretch = Stretch.None, TileMode = TileMode.Tile };
            return brush;
        }
    }
}
