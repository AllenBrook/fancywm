# FancyWM 问题与解决记录

本文档记录开发/联调过程中**遇到的问题**、**用户（客户）期望的实现方式**，以及**最终通过修改什么解决**。  
与 `FancyWM-主要需求与约定.md` 的关系：主文档写「要什么」；本文写「出过什么事、怎么修的」。

**Agent 必须遵守**：见 `.cursor/rules/issue-resolution-log.mdc`。

最后更新：2026-06-16

---

## 维护说明

1. **新条目只在文件最底部追加**（以 `---` 与上一条分隔），**勿**在中间或文首日期区插入。
2. 文首的**索引**、**记录格式**固定不动；仅当主文档 §3 有仅简表、本文无详述的条目时，酌情更新索引表。
3. 若条目已写入主文档 §3，本文须写**更完整的用户原话与方案演进**（含走过的弯路，如「曾误改为 split」）。
4. 改代码仍须同步 `FancyWM-主要需求与约定.md` §1–§4 与 `.github/pending_commit_notes.txt`。
5. 查阅时从**底部向上**读最近条目即可；全文按时间**正序**排列（旧在上、新在下）。

---

## 索引：主文档 §3 中未展开条目的简述

| 日期 | 标题 | 处理要点 | 文件 |
|------|------|----------|------|
| 2026-06-14 | 空 stack 标题栏崩溃 | `FirstOrDefault` | `TilingPanel.xaml.cs` |
| 2026-06-14 | Monorepo 编译失败 | `GitVersionBaseDirectory` | `Directory.Build.props` |
| 2026-06-14 | 再次 Stack 后标签全没 | overlay 修剪顺序 | `TilingOverlayRenderer.cs` |
| 2026-06-14 | 全屏+弹窗后取消全屏仅半屏 | 保留空 stack、`RepairRootStackLayout` | `PanelNode.cs`、`TilingService*.cs` |

---

## 记录格式（新增条目请沿用）

```markdown
## YYYY-MM-DD · 简短标题

- **现象**：用户看到什么问题
- **用户期望**：客户想怎样
- **原因**：根因（可多条）
- **最终处理**：改了什么文件/函数/策略
- **备注**：易踩坑、回归测试点（可选）
```

---

## 2026-06-14 · 自动平铺误纳入临时弹窗

- **现象**：大量对话框、工具窗被自动拉进平铺，尺寸异常。
- **用户期望**：只有规则白名单里的应用才自动平铺；临时弹窗保持原尺寸浮动。
- **原因**：上游黑名单式 `ShouldAutoTile` / `m_autoTileProcessIds` 默认平铺。
- **最终处理**：`ShouldAutoTile` 仅匹配 `InclusionMatchers`；`ToggleFloat` 不写白名单；`DetectChanges` 自动路径校验白名单。  
  文件：`TilingService.Private.cs`、`TilingService.cs`；主需求 §1.1。

---

## 2026-06-14 · Win+Shift 无关 toast 过多

- **现象**：按 Win+Shift 出现捐赠、等待操作、F12 帮助等提示。
- **用户期望**：只保留快捷键列表（`ShowContextHints`）；异常类提示保留。
- **原因**：`MainWindow.xaml.cs` 中多种 balloon/toast 挂在激活流程。
- **最终处理**：移除捐赠/等待/未识别键 toast；保留 `ShowContextHints` 与失败异常提示。  
  文件：`MainWindow.xaml.cs`；主需求 §1.7。

---

## 2026-06-14 · 构建输出多一层 Framework 目录

- **现象**：产物在 `Release\Framework\<时间戳>`，与约定不符。
- **用户期望**：`Release\<时间戳>`、`Release\latest`、`Release\latestmin`。
- **最终处理**：`AutoBuild.bat`、`AutoBuild_Framework.bat`、`AutoBuildAndRun.bat` 的 `RELEASE_ROOT` 改为仓库下 `Release`。  
  主需求 §2.2。

---

## 2026-06-14 · Stack 时辅助弹窗被纳入（Notepad++/Excel 查找框等）

- **现象**：主窗 stack 后，同进程查找/查询框也进 stack 或被拉大。
- **用户期望**：辅助弹窗保持浮动、原尺寸。
- **原因**：stack 模式下同进程新窗一律进 stack。
- **最终处理**：新增 `AuxiliaryWindowRules`；`TryRegisterAutoTiledWindow`、`SetPanelStack`、`DetectChanges` 等路径识别辅助窗并保持浮动。  
  文件：`AuxiliaryWindowRules.cs`、`TilingService*.cs`；主需求 §1.6。

---

## 2026-06-14 · 双屏 Set Panel Stack 后切换激活显示器标签变少

- **现象**：两屏都 stack 后，切换活动屏，主屏 stack 标签减少。
- **用户期望**：各屏 stack 标签与 stack 子节点一致。
- **原因**：`Refresh` 把已 stack 窗标 floating 导致误注销；`PruneUnreachableViewModels` 误删。
- **最终处理**：已注册窗不因 floating 注销；`Refresh` 仅处理本屏；overlay 修剪顺序调整。  
  文件：`TilingService*.cs`、`TilingOverlayRenderer.cs`。

---

## 2026-06-14 · 跨屏拖动 stack 窗口变 float（单向：拖过去失败、拖回可以）

- **现象**：从 A 屏拖到 B 屏后窗口变浮动；拖回 A 屏有时正常。
- **用户期望**：目标屏若已是 stack，迁入窗应加入该屏 stack，不丢语义。
- **原因**：目标屏 `DetectChanges` 早于源屏写入跨屏转移标记；白名单拦截；目标屏对未入 backend 的窗提前 return。
- **最终处理**：`RememberCrossDisplayStackTransfer`；`TryAcceptCrossDisplayStackWindow` / `FinalizeCrossDisplayLeave`；`CrossDisplayStackTransferReady` 由 `MultiDisplayTilingService` 在目标屏 `Admit`；拖动结束与焦点切换协调。  
  文件：`TilingService.Private.cs`、`TilingService.cs`、`MultiDisplayTilingService.cs`；主需求 §1.5。

---

## 2026-06-15 · Win+Shift+F 与整屏 stack 职责混淆

- **现象**：实现时把 F 当成整屏 stack 或取消后变成横向 split，与用户习惯不符。
- **用户期望（当时记录）**：
  1. **Win+Shift+F**：单窗 stack 切换（当前激活窗口句柄）。
  2. **整屏全部 stack**：**仅**托盘右键 Set Panel Stack。
  3. （**已废止**，见 2026-06-16 澄清）托盘对各句柄 `WrapInStackPanel`、不要根级共享标签栏。
- **原因**：`ApplyStackLayout` 多窗时调 `StackAllWindows`；默认快捷键 F 绑在 `ToggleFloatingMode` 上。
- **最终处理（已被 2026-06-16 澄清取代）**：曾拆分为 F 单窗 + 托盘逐句柄 `WrapInStackPanel`。  
  文件：`TilingService.Private.cs`、`TilingService.cs`、`BindableAction.cs`、`MainWindow.xaml.cs`。

---

## 2026-06-15 · Win+Shift+F 取消 stack 后无法再次 stack

- **现象**：F 可取消 stack，但再按 F 无法再次 stack；窗处于浮动，尺寸像居中缩小而非 stack 全屏。
- **用户期望**：
  - **取消 stack**：恢复**原始窗口**（浮动 + 原尺寸/位置），**不要**改成 split 平铺。
  - **再次 F**：应能再次 stack。
- **原因（取消行为）**：中间误改为 `UnwrapStackShell` / `DetachWindowFromStackKeepTiled`（用户不要 split）。
- **原因（无法再次 stack）**：浮动态重注册与 stack 路径不一致。
- **最终处理**：取消用 `FloatSingleWindowFromStack`；再次 stack 见 2026-06-16 澄清（根级共享标签栏 + `TryJoinRootStack`）。  
  文件：`TilingService.Private.cs`。
- **回归测试**：F 进 stack → F 取消（原窗浮动）→ F 再进 stack，循环三次。

---

## 2026-06-15 · F1 单窗 stack 快捷键（可配置）

- **需求**：Win+Shift+F 使用频繁，增加单独按 **F1** 触发相同单窗 stack 切换；可在设置中启用/关闭，**默认启用**。
- **处理**：
  - `Settings.EnableF1StackHotkey`（默认 `true`）；设置页交互区复选框。
  - `MainWindow.RebindF1StackHotkey` 注册低级 F1 钩子，触发 `CreateStackPanel`（与 Win+Shift+F 同路径）。
  - 与 `Win+Shift+F1`（切换显示器）无冲突：F1 为无修饰键直接模式。
- **文件**：`Settings.cs`、`SettingsViewModel.cs`、`MainWindow.xaml.cs`、`InteractionPage.xaml`、`Strings*.resx`。

---

## 2026-06-16 · 托盘 Set Panel Stack 漏窗

- **现象**：托盘 Set Panel Stack 后，部分桌面上可见、任务栏有的窗口未进入 stack。
- **原因**：
  1. 未注册窗口未正确纳入 stack 流程。
  2. 枚举未覆盖任务栏可见窗，且曾错误包含最大化窗。
- **处理**：
  - `CollectTaskbarVisibleWindowsOnThisDisplay`：`RefreshConfiguration` 后按 `GetSnapshot` + 已还原 + 本显示器筛选。
  - 跳过最小化/最大化；双屏仍由各 `TilingService` 分别处理本屏。
- **文件**：`TilingService.Private.cs`；主需求 §1.2。

---

## 2026-06-16 · F1 再次 stack 报「堆叠面板不能包含其他面板」

- **现象**：托盘 Set Panel Stack 或 F1 取消 stack 后，再按 F1 提示 `NestingInStackPanel`。
- **原因**：单窗独立 `WrapInStackPanel` 与根级共享 `StackPanel` 并存，注册路径冲突。
- **处理（过渡，已废止）**：曾新增 `RegisterWindowForManualStack` 绕过根 stack；见下条用户澄清后改为 `TryJoinRootStack`。
- **文件**：`TilingWorkspace.cs`、`TilingService.Private.cs`。

---

## 2026-06-16 · 澄清：stack 须统一顶部共享标签栏（非每窗独立壳）

- **用户原话**：「都放进顶部的 stack 标签，不是要每个进程独立设置 stack。」
- **纠正**：2026-06-15 文档/实现曾写成托盘与 F 对各句柄 `WrapInStackPanel`（每窗独立 stack 壳）、「不得 `StackAllWindows`」——**与用户真实需求相反**，属 Agent 误记。
- **用户期望（以本条为准）**：
  1. 每块显示器 stack 模式下只有**一个**根级顶部标签栏（`GetOrCreateRootStackPanel`）。
  2. **托盘 Set Panel Stack**：本屏符合条件窗口**全部并入**该标签栏。
  3. **Win+Shift+F / F6**：仅 toggle **当前焦点窗**进出**同一**标签栏（取消仍浮动原尺寸）。
  4. **不要**每窗/每进程一套独立 `WrapInStackPanel` stack 壳。
- **处理**：
  - `SetPanelStackCore` → 枚举本屏任务栏可见窗 + `MergeWindowsIntoRootStack`。
  - `StackWindow` → `TryJoinRootStack`（根 stack 注册/merge）；取消仍 `FloatSingleWindowFromStack`。
  - 废弃 `EnsureRegisteredForManualStack` / `RegisterWindowForManualStack` 路径。
  - 主需求 §1.2–§1.4、§4 已按用户澄清重写。
- **文件**：`TilingWorkspace.cs`、`TilingService.Private.cs`、`TilingService.cs`、主需求文档、`.cursor/rules/design-requirements.mdc`。

---

## 2026-06-16 · stack 直接快捷键由 F1 改为 F6

- **用户期望**：可配置的 stack 直接快捷键使用 **F6**（原 F1），避免与系统/应用 F1 帮助冲突。
- **处理**：
  - `Settings.EnableF6StackHotkey`；`MainWindow.RebindF6StackHotkey` 注册 `KeyCode.F6`。
  - 设置文案与主需求 §1.4 同步为 F6。
- **文件**：`Settings.cs`、`SettingsViewModel.cs`、`MainWindow.xaml.cs`、`InteractionPage.xaml`、`Strings*.resx`。

---

## 2026-06-16 · 全部最小化后仍显示空 stack 标签栏

- **现象**：所有窗口已最小化，屏幕顶部仍显示 stack 标签栏（空栏）。
- **用户期望**：无可见 stack 窗口时不应显示标签栏。
- **原因**：最大化/最小化时窗口从布局树注销，根级 `StackPanel` 刻意保留为空（`PanelNode.RemoveIfEmpty`），overlay 仍渲染该空 panel。
- **处理**：`UpdateLayoutAsync` 构建 snapshot 时过滤 `Children.Count == 0` 的 `StackPanelNode`。
- **文件**：`TilingService.Private.cs`；主需求 §1.3。

---

## 2026-06-16 · 取消最大化自动进 stack 无开关

- **现象**：stack 中窗口取消最大化后自动回到 stack 标签栏，无法关闭；用户认为缺配置项。
- **用户期望**：保留默认自动回 stack 行为，但提供设置开关；关闭后取消最大化保持浮动，可手动 F6 再进 stack。
- **处理**：`Settings.AutoStackOnUnmaximize`（默认 `true`）；`OnWindowStateChanged` 在 `Maximized → Restored` 且关闭时走 `RestoreFromMaximizedWithoutStack`（仅浮动，不注册 stack）；**最小化还原**不受影响。
- **文件**：`Settings.cs`、`SettingsViewModel.cs`、`MainWindow.xaml.cs`、`InteractionPage.xaml`、`Strings*.resx`；主需求 §1.4。

---

## 2026-06-20 · 启动崩溃：找不到 themes 目录

- **现象**：从 `Release\latest` 启动报 `FileNotFoundException`，路径 `...\Release\latest\themes`，堆栈在 `ThemeEngineManager.Initialize` → `Directory.CreateDirectory`。
- **原因**：`Path.GetFullPath("themes")` 依赖当前工作目录；`robocopy /MIR` 同步 `latest` 时会删掉构建产物中不存在的 `themes` 目录（及用户 `custom.css`）。
- **处理**：
  - 新增 `AppPaths`：主题/配置路径统一为 `AppContext.BaseDirectory` 下。
  - `ThemeEngineManager.EnsureThemeDirectory` 先确保父目录存在再创建。
  - `AutoBuild_SyncLastRelease.bat`：`/XD themes` 保留用户主题目录，并在同步后 `mkdir themes`（若不存在）。
- **文件**：`AppPaths.cs`、`ThemeEngineManager.cs`、`MainWindow.xaml.cs`、`Startup.cs`、`AppState.cs`、`AutoBuild_SyncLastRelease.bat`。

---

## 2026-06-20 · 默认设置改为中文 / 管理员 / 取消最大化回 stack

- **用户期望**：首次安装默认简体中文、默认 UAC 管理员启动、取消最大化自动进 stack 默认开启。
- **处理**：
  - `Settings.UiLanguage` 默认 `zh-CN`；`LocalizationService` 无配置时回退中文。
  - 新增 `Settings.RunsAsAdministrator`（默认 `true`）；`Startup.ShouldRunAsAdministrator` 读 settings / 旧 `administrator-mode` 标记。
  - `AutoStackOnUnmaximize` 保持默认 `true`（§1.4）；`AppState` 工厂默认值与 `SettingsViewModel` 字段默认对齐。
- **文件**：`Settings.cs`、`AppState.cs`、`LocalizationService.cs`、`Startup.cs`、`SettingsViewModel.cs`；主需求 §1.8。

---

## 2026-06-20 · 跨屏拖动 stack 窗口仍变浮动（双屏复现）

- **现象**：stack 窗口从 A 屏拖到 B 屏后变为普通浮动，未加入 B 屏 stack 标签栏；单屏 stack 正常。
- **用户期望**：跨屏后保持 stack 语义（§1.5）。
- **原因**：拖动过程中 `DoWindowMove` 按光标重排布局，窗口在中心越过边界前已脱离 stack；`RememberCrossDisplayStackTransfer` 仅认当前仍在 stack 树中的窗，转移标记未写入，目标屏被白名单/浮动逻辑拦截。
- **处理**：
  - 拖动开始时记录 `s_stackDragOrigins`（源屏 stack 窗）。
  - 跨屏转移与 `DetectChanges` 手动注册同时认 `stackDragOrigin` / 转移标记。
  - stack 窗用户拖动期间跳过 `DoWindowMove`；光标已离屏时不做布局移动。
- **文件**：`TilingService.cs`、`TilingService.Private.cs`；主需求 §1.5。

---

## 2026-06-20 · 顶部 overlay「浮动」在 stack 内无效

- **现象**：stack 中窗口点顶部「浮动」无效果；F6 正常。
- **用户期望**：stack → 浮动与 F6 相同（原尺寸浮动，不拆并排）；非 stack 并排/浮动切换保持原逻辑。
- **原因**：overlay 走 `ToggleFloat`（仅改 floatingSet），未从 stack 树 Unregister；F6 走 `StackWindow` → `FloatSingleWindowFromStack`。
- **处理**：提取 `TryUnstackToFloat`；`ToggleFloat`、`Float()`、overlay 浮动按钮在 stack 内优先调用（与 F6 同路径）。
- **文件**：`TilingService.cs`、`TilingService.Private.cs`；主需求 §1.4。

---

## 2026-06-27 · 托盘开启/关闭全部功能

- **用户期望**：有时用 FancyWM、有时用系统窗口管理；托盘**一个**菜单项切换；关闭/开启在**使用效果**上等同退出/打开 FancyWM，但**不结束进程**；重启后不保存关闭状态，启动即开启。
- **处理**：
  - 开启时菜单显示「关闭全部功能」，关闭时显示「开启全部功能」。
  - 关闭：`Stop` 还原窗口、注销快捷键、停用修饰键拖窗、停止发现、恢复 `WindowArranging`；托盘保留。
  - 开启：反向恢复并 `Start` + `Refresh`。
  - 状态仅内存持有，不写 `settings.json`。
- **文件**：`MainWindow.xaml.cs`、`Strings.resx`、`Strings.zh-CN.resx`；主需求 §1.9。

---
