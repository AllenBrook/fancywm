using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace FancyWM.Utilities
{
    public class ResourceProxyHost : FrameworkElement
    {
        public event EventHandler<PropertyChangedEventArgs>? PropertyChanged;

        private readonly Dictionary<string, DependencyProperty> _proxies = [];

        public object BindResource(string key)
        {
            var prop = GetProxyProperty(key);
            SetResourceReference(prop, key);
            return GetValue(prop);
        }

        private DependencyProperty GetProxyProperty(string key)
        {
            if (!_proxies.TryGetValue(key, out var prop))
            {
                prop = DependencyProperty.Register(key, typeof(object), typeof(ResourceProxyHost),
                    new PropertyMetadata(null, OnProxyChanged));
                _proxies[key] = prop;
            }
            return prop;
        }

        private static void OnProxyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ResourceProxyHost host)
            {
                host.PropertyChanged?.Invoke(host, new PropertyChangedEventArgs(e.Property.Name));
            }
        }
    }
}
