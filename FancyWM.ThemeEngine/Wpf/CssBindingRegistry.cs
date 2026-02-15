using System.Runtime.CompilerServices;
using System.Windows;

namespace FancyWM.ThemeEngine.Wpf
{
    public static class CssBindingRegistry
    {
        private class Binding(DependencyProperty property, CssResourceExtension extension)
        {
            public readonly DependencyProperty Property = property;
            public readonly CssResourceExtension Extension = extension;
        }

        private static readonly ConditionalWeakTable<DependencyObject, List<Binding>> s_bindings = new();
        private static readonly ConditionalWeakTable<System.Windows.Data.Binding, CssResourceExtension> s_indirectBindings = new();
        private static readonly object s_lock = new();

        static CssBindingRegistry()
        {
            CssManager.ThemeChanged += Invalidate;
        }

        public static void Register(DependencyObject obj, DependencyProperty dp, CssResourceExtension extension)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(dp);
            ArgumentNullException.ThrowIfNull(extension);

            var b = new Binding(dp, extension);
            lock (s_lock)
            {
                if (s_bindings.TryGetValue(obj, out var list))
                {
                    list.Add(b);
                }
                else
                {
                    s_bindings.Add(obj, [b]);
                }
            }
        }

        public static void Register(System.Windows.Data.Binding binding, CssResourceExtension extension)
        {
            ArgumentNullException.ThrowIfNull(binding);
            ArgumentNullException.ThrowIfNull(extension);

            lock (s_lock)
            {
                s_indirectBindings.Add(binding, extension);
            }
        }

        private static void Invalidate()
        {
            lock (s_lock)
            {
                foreach (var (obj, bindings) in s_bindings)
                {
                    foreach (var b in bindings)
                    {
                        obj.SetValue(b.Property, b.Extension.GetValue());
                    }
                }
                foreach (var (binding, extension) in s_indirectBindings)
                {
                    extension.NotifyPropertyChanged();
                }
            }
        }
    }

}
