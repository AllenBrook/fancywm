using System.Diagnostics;

using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Css.Converters;
using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Extensions
{
    internal static class CustomDeclarationFactory
    {
        public static IConfiguration WithCustomDeclarationFactory(this IConfiguration config)
        {
            var factory = config.Services
                       .OfType<DefaultDeclarationFactory>()
                       .First();

            try { factory.Unregister(PropertyNames.Filter); } catch { }

            var converter = ValueConverters.Or(
                new DropShadowConverter(),
                new StandardValueConverter(new Constant<object>(CssKeywords.None, null!))
            );

            factory.Register(PropertyNames.Filter, new DeclarationInfo(
                PropertyNames.Filter,
                converter,
                PropertyFlags.Animatable,
                new Constant<object>(CssKeywords.None, null!)
            ));

            Debug.Assert(factory.Create(PropertyNames.Filter).Flags == PropertyFlags.Animatable);
            return config;
        }
    }
}
