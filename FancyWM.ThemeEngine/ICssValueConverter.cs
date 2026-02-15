using AngleSharp.Css.Dom;

namespace FancyWM.ThemeEngine
{
    internal interface ICssValueConverter
    {
        bool CanConvert(Type inType, Type outType);
        object? Convert(ICssValue cssValue);
    }

    internal interface ICssValueConverter<out TOut> : ICssValueConverter
    {
        object? ICssValueConverter.Convert(ICssValue cssValue) => Convert(cssValue);
        new TOut Convert(ICssValue cssValue);
    }

    internal interface ICssValueConverter<in TIn, out TOut> : ICssValueConverter, ICssValueConverter<TOut>
        where TIn : ICssValue
    {
        bool ICssValueConverter.CanConvert(Type inType, Type outType)
        {
            return typeof(TIn).IsAssignableFrom(inType) && outType.IsAssignableFrom(typeof(TOut));
        }

        TOut ICssValueConverter<TOut>.Convert(ICssValue cssValue)
        {
            return Convert((TIn)cssValue);
        }

        TOut Convert(TIn cssValue);
    }
}
