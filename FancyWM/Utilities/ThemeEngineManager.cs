using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;

using FancyWM.ThemeEngine.Wpf;

using ModernWpf;

namespace FancyWM.Utilities
{
    internal static partial class ThemeEngineManager
    {
        private const string DefaultCssFileName = "_default.css";

        private static string _themeDir = null!;
        private static string _themeFilePath = null!;
        private static string _defaultCss = null!;
        private static FileSystemWatcher? _watcher;

        private static Timer? _debounceTimer;
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

        public static void Initialize(string themeDir, string themeFileName)
        {
            if (string.IsNullOrWhiteSpace(themeDir))
                throw new ArgumentException("Theme directory must not be empty.", nameof(themeDir));
            if (string.IsNullOrWhiteSpace(themeFileName))
                throw new ArgumentException("Current theme file name must not be empty.", nameof(themeFileName));

            Directory.CreateDirectory(themeDir);
            _themeDir = Path.GetFullPath(themeDir);

            GetDefaultCss().Subscribe(UpdateDefaultCss);
            Debug.Assert(_defaultCss != null);
            
            SetTheme(themeFileName);
        }

        private static void UpdateDefaultCss(string css)
        {
            _defaultCss = css;
            WriteDefaultCss();
            ApplyFromFile();
        }

        public static void SetTheme(string themeFileName)
        {
            _themeFilePath = Path.Combine(_themeDir, themeFileName);

            if (themeFileName != DefaultCssFileName)
            {
                EnsureCustomThemeFileExists();
            }

            ApplyFromFile();

            StartWatcher(themeFileName);
        }

        private static void WriteDefaultCss()
        {
            string path = Path.Combine(_themeDir!, DefaultCssFileName);
            File.WriteAllText(path, _defaultCss, Encoding.UTF8);
        }

        private static void EnsureCustomThemeFileExists()
        {
            if (!File.Exists(_themeFilePath))
            {
                File.WriteAllText(
                    _themeFilePath!,
                    "/* Custom theme - add your overrides below. See _default.css for examples rules. */\n",
                    Encoding.UTF8);
            }
        }

        private static void StartWatcher(string currentTheme)
        {
            _watcher?.Dispose();

            _watcher = new FileSystemWatcher(_themeDir!)
            {
                Filter = currentTheme,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                _ => ApplyFromFile(),
                state: null,
                dueTime: DebounceDelay,
                period: Timeout.InfiniteTimeSpan);
        }

        private static void ApplyFromFile()
        {
            if (_themeFilePath is null) return;

            string customCss;
            try
            {
                customCss = ReadWithRetry(_themeFilePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ThemeEngineManager] Could not read theme file: {ex.Message}");
                return;
            }

            ApplyThemeCss(customCss);
        }

        private static string ReadWithRetry(string path, int attempts = 5, int delayMs = 80)
        {
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    return reader.ReadToEnd();
                }
                catch (IOException) when (i < attempts - 1)
                {
                    Thread.Sleep(delayMs);
                }
            }
            throw new IOException($"File '{path}' could not be read after {attempts} attempts.");
        }

        private static void ApplyThemeCss(string customCssText)
        {
            var css = _defaultCss + "\n" + customCssText;
            var converter = new CssToWpfResourceConverter();
            var resources = converter.Convert(HtmlTemplate, css);
            CssManager.ApplyTheme(resources);
        }

        private static IObservable<string> GetDefaultCss()
        {
            return App.Current.Dispatcher.Invoke(() =>
            {
                var themeChanges = Observable.FromEventPattern<TypedEventHandler<ThemeManager, object>, object>(
                    e => ThemeManager.Current.ActualApplicationThemeChanged += e,
                    e => ThemeManager.Current.ActualApplicationThemeChanged -= e);

                var accentChanges = Observable.FromEventPattern<TypedEventHandler<ThemeManager, object>, object>(
                    e => ThemeManager.Current.ActualAccentColorChanged += e,
                    e => ThemeManager.Current.ActualAccentColorChanged -= e);

                var host = new ResourceProxyHost();
                var initial = GetDefaultCss(host.BindResource);

                var s = new ReplaySubject<string>();
                s.OnNext(initial);

                var updates = themeChanges.Merge(accentChanges).Select(_ => GetDefaultCss(host.FindResource));
                return s.Merge(updates);
            });
        }
    }
}
