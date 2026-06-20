using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Media;

using FancyWM.Utilities;

namespace FancyWM.Models
{
    public interface ITilingServiceSettings
    {
        bool AllocateNewPanelSpace { get; }
        bool AnimateWindowMovement { get; }
        int WindowPadding { get; }
        int PanelHeight { get; }
        int AutoSplitCount { get; }
        bool ShowFocus { get; }
        bool AutoCollapsePanels { get; }
        bool DelayReposition { get; }
        bool AutoFloatNewWindows { get; }
        bool StackAppendRestoredTabsToEnd { get; }
        /// <summary>取消最大化后是否自动回到 stack 标签栏；默认 true。</summary>
        bool AutoStackOnUnmaximize { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class DefaultKeybindingAttribute(params KeyCode[] keys) : Attribute
    {
        public readonly KeyCode[] Keys = keys;
    }

    public record class Settings : IEquatable<Settings>, ITilingServiceSettings
    {
        public Settings()
        {

        }

        [JsonConverter(typeof(Converters.ActivationHotkeyConverter))]
        public ActivationHotkey ActivationHotkey { get; init; } = ActivationHotkey.Default;

        public bool ActivateOnCapsLock { get; init; } = false;

        public bool ShowStartupWindow { get; init; } = false;

        public bool NotifyVirtualDesktopServiceIncompatibility { get; init; } = true;

        public bool AllocateNewPanelSpace { get; init; } = true;

        public bool AutoCollapsePanels { get; init; } = false;

        public int AutoSplitCount { get; init; } = 2;

        public bool DelayReposition { get; init; } = true;
        public bool AutoFloatNewWindows { get; init; } = true;

        public bool StackAppendRestoredTabsToEnd { get; init; } = true;

        /// <summary>
        /// 单独按 F6 切换当前窗 stack（与 Win+Shift+F 相同）；默认启用。
        /// </summary>
        public bool EnableF6StackHotkey { get; init; } = true;

        /// <summary>
        /// 取消最大化后自动回到 stack 标签栏；关闭则保持浮动（不自动进 stack）。
        /// </summary>
        public bool AutoStackOnUnmaximize { get; init; } = true;

        public bool AnimateWindowMovement { get; init; } = true;

        public bool ModifierMoveWindow { get; init; } = false;

        public bool ModifierMoveWindowAutoFocus { get; init; } = false;

        public int WindowPadding { get; init; } = 0;

        public int PanelHeight { get; init; } = 22;

        public int PanelFontSize { get; init; } = 12;

        public bool ShowFocus { get; init; } = false;

        public bool ShowFocusDuringAction { get; init; } = true;

        public bool OverrideAccentColor { get; init; } = false;

        [JsonConverter(typeof(Converters.ColorConverter))]
        public Color CustomAccentColor { get; init; } = Color.FromRgb(0, 100, 255);

        [JsonConverter(typeof(Converters.KeybindingConverter))]
        public KeybindingDictionary Keybindings { get; init; } = [];

        public List<string> ProcessIncludeList { get; init; } = [];

        public List<string> ProcessInstanceIncludeList { get; init; } = [];

        public List<string> ClassIncludeList { get; init; } = [];

        public bool RemindToRateReview { get; init; } = true;

        public bool ShowContextHints { get; init; } = true;

        public bool MultiMonitorSupport { get; init; } = true;

        public bool SoundOnFailure { get; init; } = true;

        public bool CheckForUpdates { get; init; } = true;

        /// <summary>界面语言；默认简体中文。</summary>
        public string UiLanguage { get; init; } = LocalizationService.ChineseSimplified;

        /// <summary>启动时请求管理员权限（UAC）；默认启用。</summary>
        public bool RunsAsAdministrator { get; init; } = true;
    }
}
