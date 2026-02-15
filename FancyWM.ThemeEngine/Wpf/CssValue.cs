using System.Windows;

using AngleSharp.Css.Dom;

using FancyWM.ThemeEngine.Wpf.Converters;

namespace FancyWM.ThemeEngine.Wpf
{
    public class CssValue
    {
        private readonly ICssValue? m_value;
        private readonly Dictionary<Type, object?> m_cache = [];

        internal CssValue(ICssValue? value)
        {
            m_value = value;
        }

        public TOut As<TOut>()
        {
            return (TOut)As(typeof(TOut))!;
        }

        public object? As(Type outType)
        {
            if (m_cache.TryGetValue(outType, out var converted))
            {
                return converted;
            }
            var newConverted = ConverterRegistry.Instance.Convert(m_value, outType);
            if (newConverted is Freezable freezable)
            {
                freezable.FreezeIfPossible();
            }
            m_cache.Add(outType, newConverted);
            return newConverted;
        }
    }
}
