using AngleSharp.Css.Dom;
using AngleSharp.Css.Values;

namespace FancyWM.ThemeEngine
{

    internal interface ICssValueVisitor<out TReturn>
    {
        private static bool IsNullable()
        {
            return Nullable.GetUnderlyingType(typeof(TReturn)) != null || !typeof(TReturn).IsValueType;
        }

        TReturn Dispatch(ICssValue value)
        {
            if (value == null)
            {
                return IsNullable() ? default! : throw new ArgumentNullException(nameof(value));
            }
            return this.Visit((dynamic)value);
        }

        TReturn Visit(ICssValue value);

        TReturn Visit(ICssPrimitiveValue value) => Visit((ICssValue)value);
        TReturn Visit(CssBackgroundSizeValue value) => Visit((ICssPrimitiveValue)value);
        TReturn Visit(Angle value) => Visit((ICssPrimitiveValue)value);
        TReturn Visit(Color value) => Visit((ICssPrimitiveValue)value);
        TReturn Visit<T>(Constant<T> value) => Visit((ICssPrimitiveValue)value);
        TReturn Visit(CounterDefinition value) => Visit((ICssPrimitiveValue)value);
        TReturn Visit(Frequency value) => Visit((ICssPrimitiveValue)value);
        TReturn Visit(Length value) => Visit((ICssPrimitiveValue)value);
        TReturn Visit(LineNames value) => Visit((ICssPrimitiveValue)value);
        TReturn Visit(Point value) => Visit((ICssPrimitiveValue)value);
        TReturn Visit(Resolution value) => Visit((ICssPrimitiveValue)value);
        TReturn Visit(Time value) => Visit((ICssPrimitiveValue)value);

        TReturn Visit(ICssCompositeValue value) => Visit((ICssValue)value);
        TReturn Visit(CssBorderImageSliceValue value) => Visit((ICssCompositeValue)value);
        TReturn Visit(CssGradientStopValue value) => Visit((ICssCompositeValue)value);
        TReturn Visit(CssImageRepeatsValue value) => Visit((ICssCompositeValue)value);
        TReturn Visit(CssOriginValue value) => Visit((ICssCompositeValue)value);
        TReturn Visit(CssShadowValue value) => Visit((ICssCompositeValue)value);

        TReturn Visit(ICssFunctionValue value) => Visit((ICssValue)value);
        TReturn Visit(CssAttrValue value) => Visit((ICssFunctionValue)value);
        TReturn Visit(ICssGradientFunctionValue value) => Visit((ICssFunctionValue)value);
        TReturn Visit(CssRadialGradientValue value) => Visit((ICssGradientFunctionValue)value);
        TReturn Visit(CssCalcValue value) => Visit((ICssFunctionValue)value);
        TReturn Visit(CssContentValue value) => Visit((ICssFunctionValue)value);
        TReturn Visit(CssRunningValue value) => Visit((ICssFunctionValue)value);
        TReturn Visit(CssShapeValue value) => Visit((ICssFunctionValue)value);
        TReturn Visit(CssSkewValue value) => Visit((ICssFunctionValue)value);
        TReturn Visit(CssUrlValue value) => Visit((ICssFunctionValue)value);
        TReturn Visit(CssVarValue value) => Visit((ICssFunctionValue)value);

        TReturn Visit(ICssMultipleValue value) => Visit((ICssValue)value);
        //TReturn Visit<T>(CssListValue<T> value) where T : ICssValue => Visit((ICssMultipleValue)value);
        //TReturn Visit<T>(CssPeriodicValue<T> value) where T : ICssValue => Visit((ICssMultipleValue)value);
        //TReturn Visit<T>(CssRadiusValue<T> value) where T : ICssValue => Visit((ICssMultipleValue)value);
        //TReturn Visit<T>(CssTupleValue<T> value) where T : ICssValue => Visit((ICssMultipleValue)value);
    }
}
