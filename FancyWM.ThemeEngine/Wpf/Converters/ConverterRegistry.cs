using System.Collections.Concurrent;
using System.Windows;

using AngleSharp.Css.Dom;
using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine.Wpf.Converters
{
    internal class ConverterRegistry
    {
        private record struct ConversionVisitor(Type OutType) : ICssValueVisitor<object?>
        {
            public Type OutType { get; } = OutType;

            public object? Visit(ICssValue value)
            {
                var converter = Instance.GetConverter(value.GetType(), OutType);
                if (converter is null)
                {
#if DEBUG
                    throw new NotSupportedException($"Conversion from {value.CssText} ({value.GetType()} : {string.Join(", ", value.GetType().GetInterfaces().Select(x => x.FullName))}) to {OutType}");
#else
                    return null;
#endif
                }
                return converter.Convert(value);
            }

            public object? Visit(ICssMultipleValue value)
            {
                if (value.Count == 1)
                {
                    return (this as ICssValueVisitor<object?>).Dispatch(value[0]);
                }
                return Visit(value as ICssValue);
            }

            public object? Visit(ICssCompositeValue value)
            {
                return Visit(value as ICssValue);
            }
        }

        public static ConverterRegistry Instance { get; private set; }

        static ConverterRegistry()
        {
            Instance = new ConverterRegistry();
        }

        private readonly ICssValueConverter[] m_converters =
        [
            new LengthDoubleConverter(),
            new LengthThicknessConverter(),
            new LengthFontWeightConverter(), 
            
            new ColorColorConverter(),
            new ColorSolidColorBrushConverter(),
            new UrlValueImageBrushConverter(),
            new BackgroundValueBrushConverter(),
            new BackgroundLayerBrushConverter(),

            new BorderRadiusCornerRadiusConverter(),
            new BorderRadiusDoubleConverter(),

            new LinearGradientBrushConverter(),
            new RadialGradientBrushConverter(),

            new PeriodicValueThicknessConverter(),
            
            new DropShadowFilterValueDropShadowEffectConverter(),
            
            new PrimitiveValueFontFamilyConverter(),
        ];

        private readonly ConcurrentDictionary<(Type fromType, Type toType), ICssValueConverter> m_convertersCache = [];

        public ConverterRegistry()
        {
        }

        public ICssValueConverter? GetConverter(Type inType, Type outType)
        {
            if (m_convertersCache.TryGetValue((inType, outType), out var converter))
            {
                return converter;
            }

            foreach (var c in m_converters)
            {
                if (c.CanConvert(inType, outType))
                {
                    m_convertersCache.TryAdd(((inType, outType)), c);
                    return c;
                }
            }
            return null;
        }

        public TOut Convert<TOut>(ICssValue? value)
        {
            return (TOut)Convert(value, typeof(TOut))!;
        }

        public object? Convert(ICssValue? value, Type outType)
        {
            if (value == null)
            {
                return null;
            }
            return (new ConversionVisitor(outType) as ICssValueVisitor<object?>).Dispatch(value);
        }
    }
}
