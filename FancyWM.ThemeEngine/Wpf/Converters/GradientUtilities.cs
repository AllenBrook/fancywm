using System.Windows;
using System.Windows.Media;

using AngleSharp.Css.Values;

using Color = System.Windows.Media.Color;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal static class GradientUtilities
    {
        public static GradientStopCollection ToGradientStopCollection(this CssGradientStopValue[] stops)
        {
            var r = ConverterRegistry.Instance;

            var colors = stops
                .Select(x => r.Convert<Color>(x.Color))
                .ToList();

            var offsets = stops
                .Select(x => x.Location?.AsPercent() ?? double.NaN)
                .ToList();

            if (double.IsNaN(offsets[0]))
            {
                offsets[0] = 0;
            }

            if (double.IsNaN(offsets[^1]))
            {
                offsets[^1] = 1;
            }

            double maxOffset = offsets[0];
            for (int i = 1; i < offsets.Count; i++)
            {
                if (!double.IsNaN(offsets[i]))
                {
                    if (offsets[i] < maxOffset)
                    {
                        offsets[i] = maxOffset;
                    }
                    else
                    {
                        maxOffset = offsets[i];
                    }
                }
            }

            for (int i = 1; i < offsets.Count - 1; i++)
            {
                if (double.IsNaN(offsets[i]))
                {
                    int runStart = i;
                    int runEnd = i;
                    while (runEnd < offsets.Count && double.IsNaN(offsets[runEnd]))
                    {
                        runEnd++;
                    }

                    var lo = offsets[runStart - 1];
                    var hi = offsets[runEnd];
                    int count = runEnd - runStart;

                    for (int j = 0; j < count; j++)
                    {
                        offsets[runStart + j] = lo + (hi - lo) * (j + 1.0) / (count + 1.0);
                    }

                    i = runEnd;
                }
            }

            var coll = new GradientStopCollection();
            foreach (var (c, o) in colors.Zip(offsets))
            {
                coll.Add(new GradientStop(c, o));
            }

            return coll;
        }

        public static DrawingBrush CreateUniformDrawingBrush(this Brush brush)
        {
            brush.FreezeIfPossible();
            var square = new RectangleGeometry(new Rect(0, 0, 1, 1));
            var geoDrawing = new GeometryDrawing(brush, null, square);
            var drawingBrush = new DrawingBrush(geoDrawing);
            drawingBrush.Stretch = Stretch.UniformToFill;
            return drawingBrush;
        }
    }
}
