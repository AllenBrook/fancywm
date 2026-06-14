#!/usr/bin/env python3
"""Generate FancyWM/Resources/Strings.zh-CN.resx from Strings.resx."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "FancyWM" / "Resources" / "Strings.resx"
DST = ROOT / "FancyWM" / "Resources" / "Strings.zh-CN.resx"

TRANSLATIONS = {
    "Cancel": "取消",
    "Cancel the action": "取消当前操作",
    "Wrap in horizontal panel": "包裹为水平面板",
    "Create a new horizontal panel around the focused window": "在焦点窗口周围创建新的水平面板",
    "Wrap in stack panel": "包裹为堆叠面板",
    "Create a new stack panel around the focused window": "在焦点窗口周围创建新的堆叠面板",
    "Wrap in vertical panel": "包裹为垂直面板",
    "Create a new vertical panel around the focused window": "在焦点窗口周围创建新的垂直面板",
    "Decrease height": "减小高度",
    "Decrease the height of the focused window": "减小焦点窗口的高度",
    "Decrease width": "减小宽度",
    "Decrease the width of the focused window": "减小焦点窗口的宽度",
    "Increase height": "增加高度",
    "Increase the height of the focused window": "增加焦点窗口的高度",
    "Increase width": "增加宽度",
    "Increase the width of the focused window": "增加焦点窗口的宽度",
    "Move down": "下移",
    "Move the window next to the window below": "将窗口移动到下方窗口旁边",
    "Move focus down": "焦点下移",
    "Focus the window below the current window": "将焦点移至当前窗口下方的窗口",
    "Move focus left": "焦点左移",
    "Focus the window to the left of the current window": "将焦点移至当前窗口左侧的窗口",
    "Move focus right": "焦点右移",
    "Focus the window to the right of the current window": "将焦点移至当前窗口右侧的窗口",
    "Move focus up": "焦点上移",
    "Focus the window above the current window": "将焦点移至当前窗口上方的窗口",
    "Move left": "左移",
    "Move the window next to the window on the left": "将窗口移动到左侧窗口旁边",
    "Move right": "右移",
    "Move the window next to the window on the right": "将窗口移动到右侧窗口旁边",
    "Move up": "上移",
    "Move the window next to the window above": "将窗口移动到上方窗口旁边",
    "Move to upper level": "移至上一级",
    "Move the window out of its containing panel": "将窗口移出所在面板",
    "Force refresh workspace": "强制刷新工作区",
    "Force a change-detection cycle of the whole workspace": "强制对整个工作区执行一次变更检测",
    "Swap down": "与下方交换",
    "Swap the current window with the window below it": "将当前窗口与下方窗口交换",
    "Swap left": "与左侧交换",
    "Swap the current window with the window on its left": "将当前窗口与左侧窗口交换",
    "Swap right": "与右侧交换",
    "Swap the current window with the window on its right": "将当前窗口与右侧窗口交换",
    "Swap up": "与上方交换",
    "Swap the current window with the window above it": "将当前窗口与上方窗口交换",
    "Float window": "浮动窗口",
    "Toggle floating mode for the focused window": "切换焦点窗口的浮动模式",
    "Toggle window management": "切换窗口管理",
    "Toggle FancyWM's window management functionality": "切换 FancyWM 的窗口管理功能",
    "Reserve empty space in new panels": "在新面板中预留空白空间",
    "New panels have will have empty space until a second window is added.": "新面板在添加第二个窗口之前会保留空白空间。",
    "Animate window movement": "窗口移动动画",
    "General": "常规",
    "Language": "语言",
    "Restart FancyWM for changes to take effect!": "请重启 FancyWM 以使更改生效！",
    "Show settings.json in containing folder": "在文件夹中显示 settings.json",
    "Override accent color": "覆盖强调色",
    "Panel font size": "面板字体大小",
    "Panel height": "面板高度",
    "Run with administrator privileges": "以管理员权限运行",
    "FancyWM is currently running with administrator privileges and can manage all of your windows.": "FancyWM 当前正以管理员权限运行，可以管理所有窗口。",
    "FancyWM cannot manage windows from elevated processes unless it is run as administrator. Enable this option and restart FancyWM to fix this.": "除非以管理员身份运行，否则 FancyWM 无法管理提升权限进程的窗口。启用此选项并重启 FancyWM 可解决此问题。",
    "Run automatically at system startup": "系统启动时自动运行",
    "Show contextual hints in pop-up": "在弹出窗口中显示上下文提示",
    "Shows the list of available keybindings after 1s of inactivity. The command pop-up stays on-screen for longer, but you can close it with": "无操作 1 秒后显示可用快捷键列表。命令弹出窗口会停留更久，您可以用以下键关闭：",
    "Show border around the focused window ᴺᴱᵂ": "在焦点窗口周围显示边框 ᴺᴱᵂ",
    "Highlights the focused window.": "高亮显示焦点窗口。",
    "Show focused window reminder": "显示焦点窗口提醒",
    "Highlights the focused window when the command pop-up is shown.": "显示命令弹出窗口时高亮焦点窗口。",
    "Show the startup window at program startup": "程序启动时显示启动窗口",
    "Play a sound when an action fails": "操作失败时播放提示音",
    "Sounds": "声音",
    "Startup": "启动",
    "Tiling behavior": "平铺行为",
    "Window gap": "窗口间距",
    "Displays": "显示器",
    "Enable tiling on all connected displays": "在所有已连接显示器上启用平铺",
    "App version:": "应用版本：",
    "About": "关于",
    "Copyright © 2024 Vesko Karaganev. All Rights Reserved.": "Copyright © 2024 Vesko Karaganev. 保留所有权利。",
    "Credits": "致谢",
    "Made possible by the following awesome open-source software.": "得益于以下优秀的开源软件。",
    "Advanced": "高级",
    "Generate starter script": "生成入门脚本",
    "Easily extend FancyWM with AutoHotkey - free tool for simple Windows shortcuts. No coding needed: generate a ready-to-edit script with all commands listed as examples.": "使用免费的 AutoHotkey 轻松扩展 FancyWM。无需编程：生成可直接编辑的脚本，其中列出所有命令示例。",
    "Reset window layout": "重置窗口布局",
    "Reset the tiling layout for all monitors? Your settings in settings.json (borders, fonts, keybindings, rules, etc.) will be kept.": "重置所有显示器的平铺布局？settings.json 中的设置（边框、字体、快捷键、规则等）将保留。",
    "Clears the in-memory tiling layout and re-registers open windows. Use this when a monitor stops responding to layout commands. Does not change settings.json.": "清除内存中的平铺布局并重新注册已打开的窗口。当某台显示器不再响应布局命令时使用。不会修改 settings.json。",
    "contact the developer": "联系开发者",
    "Details": "详情",
    "A fatal error has occurred in FancyWM!": "FancyWM 发生致命错误！",
    "FancyWM will now close. If this issue persists, please check for a solution and/or open a bug report on": "FancyWM 即将关闭。如果问题持续存在，请查找解决方案和/或在以下位置提交错误报告：",
    "By submitting a log you can help diagnose the issue. The log file contains a history of the events leading up to the crash. Sensitive information is obfuscated.": "提交日志有助于诊断问题。日志文件包含崩溃前的事件记录，敏感信息已做脱敏处理。",
    "Submit log": "提交日志",
    "EXPERIMENTAL": "实验性",
    "Help": "帮助",
    "Activation hotkey": "激活快捷键",
    "Keybindings": "快捷键",
    "Registers the keybinding as a system hotkey, allowing it to be used without the Activation hotkey. The keybinding must use at least one modifier key (Ctrl, Shift, Alt, Win).": "将快捷键注册为系统热键，无需激活快捷键即可使用。快捷键必须包含至少一个修饰键（Ctrl、Shift、Alt、Win）。",
    "Windows will not allow FancyWM to register a hotkey that is already used by it or another application.": "Windows 不允许 FancyWM 注册已被系统或其他应用占用的热键。",
    "No": "否",
    "Off": "关",
    "On": "开",
    "Quit": "退出",
    "Rules": "规则",
    "Automatically tile by window class": "按窗口类自动平铺",
    "Windows with a matching window class will be automatically tiled. The matching is not case-sensitive and you may use regular expressions.": "窗口类匹配的窗口将自动平铺。匹配不区分大小写，可使用正则表达式。",
    "Managed windows": "受管窗口",
    "Automatically tile by process name": "按进程名自动平铺",
    'Windows belonging to one of the listed processes will be automatically tiled. Enter one process name per line, excluding the ".exe" extension (e.g. "explorer"). The matching is not case-sensitive and you may use regular expressions. This applies to all instances of the process.': '所列进程的窗口将自动平铺。每行输入一个进程名，不含“.exe”扩展名（例如“explorer”）。匹配不区分大小写，可使用正则表达式。适用于该进程的所有实例。',
    "Automatically tile by process instance": "按进程实例自动平铺",
    'Windows from a specific process instance will be automatically tiled. Use the format processname:pid (e.g. "devenv:12345"). Each running instance can have different layout settings.': '特定进程实例的窗口将自动平铺。格式为 processname:pid（例如“devenv:12345”）。每个运行实例可有不同的布局设置。',
    "Set panel stack": "设置面板堆叠",
    "Settings": "设置",
    "FancyWM Settings": "FancyWM 设置",
    "Yes": "是",
    "Please see": "请参阅",
    "FancyWM Startup": "FancyWM 启动",
    "FancyWM is now running!": "FancyWM 已在运行！",
    "First-time users:": "首次使用：",
    "You can always find it in the System Tray.": "您可以在系统托盘中找到它。",
    "To change the layout for the focused window, press the activation keybinding, [⇧ Shift] and [⊞ Win], and once the toast popup is visible, press [H], [V] or [S].": "要更改焦点窗口的布局，请按下激活快捷键 [⇧ Shift] 和 [⊞ Win]，弹出提示后按 [H]、[V] 或 [S]。",
    "You can also hover over the top border of the focused window to change the layout.": "您也可以将鼠标悬停在焦点窗口的上边框上来更改布局。",
    "Show this window when FancyWM starts": "FancyWM 启动时显示此窗口",
    "Add new": "添加",
    "Clear all": "全部清除",
    "Moved": "已移动",
    "to": "到",
    "window": "窗口",
    "If you are, please take a moment to review it on the Microsoft Store or consider sponsoring the project's development.": "如果是，请在 Microsoft Store 上评价，或考虑赞助项目开发。",
    "The window cannot be resized to fit!": "无法调整窗口大小以适应！",
    "Could not": "无法",
    "Could not register one or more keybindings as system hotkeys!": "无法将一个或多个快捷键注册为系统热键！",
    "Enjoying FancyWM?": "喜欢 FancyWM 吗？",
    "Floating mode enabled for": "已启用浮动模式：",
    "is already on": "已在",
    "No focused window!": "没有焦点窗口！",
    "Nothing is assigned to": "未分配给",
    "Nothing to do on this desktop.": "此桌面上没有可执行的操作。",
    "Operation failed!": "操作失败！",
    "Press F12 for help": "按 F12 获取帮助",
    "Received invalid command": "收到无效命令",
    "FancyWM Error": "FancyWM 错误",
    "Communication with the Microsoft Store failed with code": "与 Microsoft Store 通信失败，错误代码",
    "Unrecognized keybinding!": "无法识别的快捷键！",
    "FancyWM Update Check": "FancyWM 更新检查",
    "A new version of FancyWM is available! Download the latest update in the background?": "有新版本可用！是否在后台下载最新更新？",
    "Update download completed! Restart FancyWM to install it?": "更新下载完成！是否重启 FancyWM 以安装？",
    "Waiting for action...": "等待操作…",
    "Cannot nest panel inside itself!": "不能将面板嵌套在自身内！",
    "The target panel does not support this operation!": "目标面板不支持此操作！",
    "No adjacent window!": "没有相邻窗口！",
    "Cannot modify the top-level panel!": "无法修改顶级面板！",
    "Stack panel cannot contain other panels!": "堆叠面板不能包含其他面板！",
    "No valid placement exists!": "没有有效的放置位置！",
    "Cannot move up any further!": "无法再上移！",
    "Cannot fit in the target panel!": "无法放入目标面板！",
    "Restart FancyWM": "重启 FancyWM",
    "Add tiling rule for window class": "为窗口类添加平铺规则",
    "Add tiling rule for process": "为进程添加平铺规则",
    "More options": "更多选项",
    "Right click to move to upper level": "右键单击移至上一级",
    "Ctrl + drag to reorder tabs": "按住 Ctrl 并拖动以调整标签顺序",
    "Right-click and drag to reorder tabs": "按住右键并拖动以调整标签顺序",
    "Single click to focus": "单击以聚焦",
    "No previous desktop.": "没有上一个虚拟桌面。",
    "Move window to previous desktop": "将窗口移至上一个虚拟桌面",
    "Move window to right desktop": "将窗口移至右侧虚拟桌面",
    "Move window to left desktop": "将窗口移至左侧虚拟桌面",
    "Move the focused window from the current virtual desktop to the previous virtual desktop": "将焦点窗口从当前虚拟桌面移至上一个虚拟桌面",
    "Switch to previous desktop": "切换到上一个虚拟桌面",
    "Switch from the current virtual desktop to the previous virtual desktop": "从当前虚拟桌面切换到上一个虚拟桌面",
    "Drag over another window to group": "拖到另一个窗口上以分组",
    "Automatically collapse panels": "自动折叠面板",
    "Panels with a single window will be removed automatically.": "只含一个窗口的面板将自动移除。",
    "Append restored stack tabs to the right": "将恢复的堆叠标签追加到最右侧",
    "When a minimized window is restored in a stack panel, place its tab at the rightmost position. When disabled, tabs return to their previous order.": "堆叠面板中最小化的窗口恢复后，将其标签放在最右侧。关闭此选项时，标签将回到原来的位置。",
    "No previous display.": "没有上一个显示器。",
    "display": "显示器",
    "Show desktop": "显示桌面",
    "Show/hide the desktop": "显示/隐藏桌面",
    "Focus": "焦点",
    "Panels": "面板",
    "Sizing": "尺寸",
    "Virtual Desktops": "虚拟桌面",
    "Windows": "窗口",
    "Close": "关闭",
    "Your version of Windows is not supported!": "您的 Windows 版本不受支持！",
    "Use without Activation hotkey?": "不使用激活快捷键？",
    "Move windows by holding": "按住以下键移动窗口",
    "Automatically activate the moved window": "自动激活被移动的窗口",
    "will activate the window": "将激活该窗口",
    "Window movement": "窗口移动",
    "Show warning on Virtual Desktop Service failure": "虚拟桌面服务失败时显示警告",
    "FancyWM tries to use the internal Windows Virtual Desktop Service, which is unsupported by Microsoft. FancyWM can warn you if it is unsupported on this version of Windows.": "FancyWM 尝试使用 Windows 内部的虚拟桌面服务，该服务不受 Microsoft 官方支持。若当前 Windows 版本不支持，FancyWM 可以提醒您。",
    "You do not have to release the key to activate. ⇪ Caps Lock will no longer function normally": "无需松开按键即可激活。⇪ Caps Lock 将不再正常工作",
    "Use ⇪ Caps Lock as  the Activation hotkey": "使用 ⇪ Caps Lock 作为激活快捷键",
    "Automatic split threshold ᴺᴱᵂ": "自动拆分阈值 ᴺᴱᵂ",
    "Triggers automatic panel creation when the focused panel has more than the specified number of children.": "当焦点面板的子项超过指定数量时，自动创建面板。",
    "FancyWM is free and open-source, but requires the same time and effort as any other software.": "FancyWM 是免费开源软件，但需要与其他软件相同的时间和精力来维护。",
    "Release window to move ᴺᴱᵂ": "松开鼠标后移动窗口 ᴺᴱᵂ",
    "When enabled, the tiling layout will update once the window has finished moving.": "启用后，平铺布局将在窗口移动完成后更新。",
    "Interaction": "交互",
    "Interface": "界面",
    "Colors": "颜色",
    "Sizes & margins": "尺寸与边距",
    "Sizes &amp; margins": "尺寸与边距",
    "Visuals": "视觉效果",
    "Switch to desktop on the left": "切换到左侧虚拟桌面",
    "Switch to desktop on the right": "切换到右侧虚拟桌面",
    "Switch from the current virtual desktop to the virtual desktop on the left": "从当前虚拟桌面切换到左侧虚拟桌面",
    "Switch from the current virtual desktop to the virtual desktop on the right": "从当前虚拟桌面切换到右侧虚拟桌面",
    "Move the focused window from the current virtual desktop to virtual desktop on the left": "将焦点窗口从当前虚拟桌面移至左侧虚拟桌面",
    "Move the focused window from the current virtual desktop to virtual desktop on the right": "将焦点窗口从当前虚拟桌面移至右侧虚拟桌面",
    "Another instance of FancyWM is already running!": "FancyWM 的另一个实例已在运行！",
    "Activate windows on hover": "悬停时激活窗口",
    'Change which window is focused without clicking. Enables the built-in Windows feature called "Activate on hover".': '无需单击即可切换焦点窗口。启用 Windows 内置的“悬停时激活”功能。',
    "A newer version is available": "有更新版本可用",
}

PATTERNS = [
    (re.compile(r"^Switch to desktop (\d+)$"), r"切换到虚拟桌面 \1"),
    (re.compile(r"^Switch from the current virtual desktop to virtual desktop (\d+)$"), r"从当前虚拟桌面切换到虚拟桌面 \1"),
    (re.compile(r"^Move window to desktop (\d+)$"), r"将窗口移至虚拟桌面 \1"),
    (re.compile(r"^Move the focused window from the current virtual desktop to virtual desktop (\d+)$"), r"将焦点窗口从当前虚拟桌面移至虚拟桌面 \1"),
    (re.compile(r"^Switch to display (\d+)$"), r"切换到显示器 \1"),
    (re.compile(r"^Switch from the current display to display (\d+)$"), r"从当前显示器切换到显示器 \1"),
    (re.compile(r"^Move window to display (\d+)$"), r"将窗口移至显示器 \1"),
    (re.compile(r"^Move the focused window from the current display to display (\d+)$"), r"将焦点窗口从当前显示器移至显示器 \1"),
    (re.compile(r"^Move window to previous display$"), "将窗口移至上一个显示器"),
    (re.compile(r"^Move the focused window from the current display to the previous display$"), "将焦点窗口从当前显示器移至上一个显示器"),
    (re.compile(r"^Switch to previous display$"), "切换到上一个显示器"),
    (re.compile(r"^Switch from the current display to the previous display$"), "从当前显示器切换到上一个显示器"),
]

MULTILINE = {
    "FancyWM tries to use the internal Windows Virtual Desktop Service, which is unsupported by Microsoft. This message can be disabled in Settings.\n\nIt appears that in your version of Windows, Microsoft has changed the VDS interface, and FancyWM cannot detect your virtual desktops.\n\nIf you are running a supported non-Insider Windows build and you see this message, please select Yes to report it.": (
        "FancyWM 尝试使用 Windows 内部的虚拟桌面服务，该服务不受 Microsoft 官方支持。可在设置中关闭此提示。\n\n"
        "在您的 Windows 版本中，Microsoft 似乎已更改 VDS 接口，FancyWM 无法检测您的虚拟桌面。\n\n"
        "如果您运行的是受支持的非 Insider 版本并看到此消息，请选择“是”进行报告。"
    ),
}

DATA_BLOCK = re.compile(
    r'(<data name="(?!Name1|Color1|Bitmap1|Icon1)[^"]+"[^>]*>\s*<value>)(.*?)(</value>)',
    re.DOTALL,
)


def translate(text: str) -> str:
    if text in MULTILINE:
        return MULTILINE[text]
    if text in TRANSLATIONS:
        return TRANSLATIONS[text]
    for pattern, repl in PATTERNS:
        if pattern.match(text):
            return pattern.sub(repl, text)
    raise KeyError(f"Missing translation: {text!r}")


def replace_value(match: re.Match[str]) -> str:
    original = match.group(2)
    translated = translate(original)
    return f"{match.group(1)}{translated}{match.group(3)}"


def main() -> None:
    content = SRC.read_text(encoding="utf-8")
    marker = '  <data name="Keybinding.Cancel.Caption"'
    split_at = content.index(marker)
    prefix, translatable = content[:split_at], content[split_at:]
    missing = []

    def safe_replace(match: re.Match[str]) -> str:
        try:
            return replace_value(match)
        except KeyError:
            missing.append(match.group(2))
            return match.group(0)

    translatable = DATA_BLOCK.sub(safe_replace, translatable)
    if missing:
        raise SystemExit("Missing translations:\n" + "\n".join(repr(x) for x in missing))
    DST.write_text(prefix + translatable, encoding="utf-8")
    print(f"Wrote {DST}")


if __name__ == "__main__":
    main()
