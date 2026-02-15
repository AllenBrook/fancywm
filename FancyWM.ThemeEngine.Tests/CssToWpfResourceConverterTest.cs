using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

using FancyWM.ThemeEngine.Wpf;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.ThemeEngine.Tests
{
    [TestClass]
    public class CssToWpfResourceConverterTest
    {
        [TestMethod]
        public void TestSpecificity()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<a><button></button><button class='primary'></button></a>";
            var cssText = @"
                button { color: red; }
                button.primary { color: blue; }
                button:hover { color: lime; }
            ";

            var resources = converter.Convert(htmlTemplate, cssText);

            Assert.AreEqual(Colors.Red, resources["button/color"].As<Color>());
            Assert.AreEqual(Colors.Lime, resources["button:hover/color"].As<Color>());
            Assert.AreEqual(Colors.Blue, resources["button.primary/color"].As<Color>());
            Assert.AreEqual(Colors.Lime, resources["button.primary:hover/color"].As<Color>());
        }

        [TestMethod]
        public void TestPrecedence()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = @"
                a, button { color: blue; }
                button { color: red; }
            ";

            var resources = converter.Convert(htmlTemplate, cssText);
            Assert.AreEqual(Colors.Red, resources["button/color"].As<Color>());
        }

        [TestMethod]
        public void TestColorProperty()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = "button { color: #AABBCC; }";

            var resources = converter.Convert(htmlTemplate, cssText);
            Assert.AreEqual(Color.FromRgb(0xAA, 0xBB, 0xCC), resources["button/color"].As<Color>());
        }

        [TestMethod]
        public void TestBackgroundColorProperty()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = "button { background-color: rgba(10, 20, 30, 0.5); }";

            var resources = converter.Convert(htmlTemplate, cssText);
            Assert.AreEqual(Color.FromArgb(128, 10, 20, 30), resources["button/background-color"].As<Color>());
        }

        [TestMethod]
        public void TestBackgroundImageProperty()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = "button { background-image: url('file:///C:/Windows/Web/Wallpaper/Windows/img0.jpg'); }";

            var resources = converter.Convert(htmlTemplate, cssText);
            Assert.IsInstanceOfType(resources["button/background-image"].As<Brush>(), typeof(ImageBrush));
        }

        [TestMethod]
        public void TestBackgroundProperty()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = "button { background: rgba(10, 20, 30, 0.5); }";

            var resources = converter.Convert(htmlTemplate, cssText);
            Assert.AreEqual(Color.FromArgb(128, 10, 20, 30), resources["button/background-color"].As<Color>());
        }

        [TestMethod]
        public void TestBorderColorProperty()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = "button { border-color: red; }";

            var resources = converter.Convert(htmlTemplate, cssText);
            Assert.AreEqual(Colors.Red, resources["button/border-top-color"].As<Color>());
        }

        [TestMethod]
        public void TestBorderWidthProperty()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = "button { border-width: 1px 2px 3px 4px; }";

            var resources = converter.Convert(htmlTemplate, cssText);
            Assert.AreEqual(new Thickness(4, 1, 2, 3), resources["button/border-width"].As<Thickness>());
            Assert.AreEqual(1, resources["button/border-top-width"].As<double>());
        }


        [TestMethod]
        public void TestBorderRadiusProperty()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = "button { border-radius: 1px 2px 3px 4px; }";

            var resources = converter.Convert(htmlTemplate, cssText);
            Assert.AreEqual(new CornerRadius(1, 2, 3, 4), resources["button/border-radius"].As<CornerRadius>());
        }

        [TestMethod]
        public void TestFilterDropShadowProperty()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = "button { filter: drop-shadow(2px 2px rgba(0,0,0,.2)); }";

            var resources = converter.Convert(htmlTemplate, cssText);
            var filter = resources["button/filter"].As<Effect>();
            Assert.IsInstanceOfType(filter, typeof(DropShadowEffect));
        }

        [TestMethod]
        public void TestFontWeightProperty()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = "button { font-weight: 800; }";

            var resources = converter.Convert(htmlTemplate, cssText);
            Assert.AreEqual(FontWeight.FromOpenTypeWeight(800), resources["button/font-weight"].As<FontWeight>());
        }

        [TestMethod]
        public void TestFontFamilityProperty()
        {
            var converter = new CssToWpfResourceConverter();
            var htmlTemplate = "<button></button>";
            var cssText = "button { font-family: 'Segoe UI'; }";

            var resources = converter.Convert(htmlTemplate, cssText);
            Assert.AreEqual("Segoe UI", (resources["button/font-family"].As<FontFamily>())?.FamilyNames.First().Value);
        }
    }
}
