using System;
using System.IO;

namespace FancyWM.Utilities
{
    internal static class AppPaths
    {
        public static string ApplicationDirectory { get; } = Path.GetFullPath(AppContext.BaseDirectory);

        public static string ThemesDirectory { get; } = Path.Combine(ApplicationDirectory, "themes");

        public static string SettingsFile { get; } = Path.Combine(ApplicationDirectory, "settings.json");

        public static string AdministratorModeMarker { get; } = Path.Combine(ApplicationDirectory, "administrator-mode");
    }
}
