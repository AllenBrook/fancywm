using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;

using FancyWM.Resources;

namespace FancyWM.Utilities
{
    internal static class LocalizationService
    {
        public const string English = "en";
        public const string ChineseSimplified = "zh-CN";

        public static string CurrentLanguage { get; private set; } = ChineseSimplified;

        public static void ApplyFromSettingsFile(string settingsPath)
        {
            var language = ChineseSimplified;
            try
            {
                if (File.Exists(settingsPath))
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
                    if (document.RootElement.TryGetProperty(nameof(Models.Settings.UiLanguage), out var property))
                    {
                        language = property.GetString() ?? ChineseSimplified;
                    }
                }
            }
            catch
            {
            }

            Apply(language);
        }

        public static void Apply(string? language)
        {
            CurrentLanguage = Normalize(language);

            CultureInfo culture;
            if (CurrentLanguage == ChineseSimplified)
            {
                culture = CultureInfo.GetCultureInfo(ChineseSimplified);
            }
            else
            {
                culture = CultureInfo.GetCultureInfo(English);
            }

            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            Strings.Culture = CurrentLanguage == ChineseSimplified
                ? culture
                : null;
        }

        public static string Normalize(string? language)
            => language == ChineseSimplified ? ChineseSimplified : English;
    }
}
