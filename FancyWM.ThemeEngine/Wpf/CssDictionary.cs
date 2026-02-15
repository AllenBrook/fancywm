using System.Collections;
using System.Diagnostics.CodeAnalysis;

using AngleSharp.Css.Dom;

namespace FancyWM.ThemeEngine.Wpf
{
    internal class CssDictionary : IReadOnlyDictionary<string, CssValue>
    {
        private readonly IReadOnlyDictionary<string, ICssStyleDeclaration> m_styles;
        private readonly Dictionary<string, CssValue> m_flattened = [];

        public CssDictionary(IReadOnlyDictionary<string, ICssStyleDeclaration> stylesDict)
        {
            m_styles = stylesDict;
            foreach (var (selector, styles) in stylesDict)
            {
                foreach (var property in styles)
                {
                    m_flattened.Add($"{selector}/{property.Name}", new CssValue(property.RawValue));
                }
            }
        }

        public CssValue this[string key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }
                throw new KeyNotFoundException(key);
            }
        }

        public IEnumerable<string> Keys => m_flattened.Keys;

        public IEnumerable<CssValue> Values => m_flattened.Values;

        public int Count => m_flattened.Count;

        public bool ContainsKey(string key)
        {
            return m_flattened.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<string, CssValue>> GetEnumerator()
        {
            return m_flattened.GetEnumerator();
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out CssValue value)
        {
            if (!m_flattened.TryGetValue(key, out value))
            {
                var parts = key.Split('/');
                var element = m_styles[parts[0]];
                var property = element.GetProperty(parts[1]);
                if (property == null)
                {
                    return false;
                }
                value = new CssValue(property.RawValue);
                m_flattened.Add(key, value);
            }
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
