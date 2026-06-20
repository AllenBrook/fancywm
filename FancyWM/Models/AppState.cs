using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using FancyWM.Utilities;

namespace FancyWM.Models
{
    public class AppState
    {
        public IObservableFileEntity<Settings> Settings { get; }

        public AppState()
        {
            Settings = new ObservableJsonEntityWithCommentPreservation<Settings>(AppPaths.SettingsFile,
                () => new Settings
                {
                    AutoFloatNewWindows = true,
                    UiLanguage = LocalizationService.ChineseSimplified,
                    RunsAsAdministrator = true,
                    AutoStackOnUnmaximize = true,
                },
                new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    WriteIndented = true,
                    PropertyNamingPolicy = null,
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    Converters =
                    {
                        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                    }
                });
        }
    }
}
