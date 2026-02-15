using System.Windows.Media;

using FancyWM.ThemeEngine.Wpf;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.ThemeEngine.Tests
{
    [TestClass]
    public class WpfGradientTest
    {
        private static Brush Background(string cssValue)
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<a></a>";
            var cssText = $"a {{ background-image: {cssValue}; }}";

            var resources = converter.Convert(htmlTemplate, cssText);
            return resources["a/background"].As<Brush>();
        }

        private static LinearGradientBrush LinearGradient(string cssValue)
        {
            var brush = Background(cssValue);
            Assert.IsInstanceOfType(brush, typeof(DrawingBrush));
            return GetInnerBrush(brush) as LinearGradientBrush;
        }

        private static RadialGradientBrush RadialGradient(string cssValue)
        {
            var brush = Background(cssValue);
            return GetInnerBrush(brush) as RadialGradientBrush;
        }

        private static double GetAngle(LinearGradientBrush brush)
        {
            return (brush.RelativeTransform as RotateTransform).Angle;
        }

        private static Brush GetInnerBrush(Brush brush)
        {
            if (brush is DrawingBrush) 
            {
                return ((brush as DrawingBrush).Drawing as GeometryDrawing).Brush;
            }
            return brush;
        }

        [TestMethod]
        public void TestSimpleLinearGradient()
        {
            var gradient = LinearGradient("linear-gradient(to right, blue, red)");
            Assert.AreEqual(2, gradient.GradientStops.Count);
            Assert.AreEqual(0.0, gradient.GradientStops[0].Offset);
            Assert.AreEqual(1.0, gradient.GradientStops[1].Offset);
            Assert.AreEqual(90, GetAngle(gradient));
        }

        [TestMethod]
        public void TestAngle()
        {
            var gradient = LinearGradient("linear-gradient(45deg, blue, red)");
            Assert.AreEqual(45, GetAngle(gradient));
        }

        [TestMethod]
        public void TestIndeterminate()
        {
            var gradient = LinearGradient("linear-gradient(blue 10%, green, yellow, red 80%)");
            Assert.AreEqual(4, gradient.GradientStops.Count);
            Assert.AreEqual(0.1, gradient.GradientStops[0].Offset);
            Assert.AreEqual(0.33, gradient.GradientStops[1].Offset, 0.01);
            Assert.AreEqual(0.56, gradient.GradientStops[2].Offset, 0.01);
            Assert.AreEqual(0.8, gradient.GradientStops[3].Offset);
        }

        [TestMethod]
        public void TestSimpleRadialGradient()
        {
            var gradient = RadialGradient("radial-gradient(blue, red)");
            Assert.AreEqual(2, gradient.GradientStops.Count);
            Assert.AreEqual(0.0, gradient.GradientStops[0].Offset);
            Assert.AreEqual(1.0, gradient.GradientStops[1].Offset);
        }

    }
}
