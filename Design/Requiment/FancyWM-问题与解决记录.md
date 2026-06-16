# FancyWM 问题与解决记录

本文档记录开发/联调过程中**遇到的问题**、**用户（客户）期望的实现方式**，以及**最终通过修改什么解决**。  
与 `FancyWM-主要需求与约定.md` 的关系：主文档写「要什么」；本文写「出过什么事、怎么修的」。

**Agent 必须遵守**：见 `.cursor/rules/issue-resolution-log.mdc`。

最后更新：2026-06-15

---

## 记录格式（新增条目请沿用）

```markdown
### YYYY-MM-DD · 简短标题

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
- **用户期望（明确）**：
  1. **Win+Shift+F**：始终**单窗** stack 切换（当前激活窗口句柄）。
  2. **整屏全部 stack**：**仅**托盘右键 Set Panel Stack，无其他入口。
  3. 托盘 stack：对本屏每个符合条件句柄，**各执行一次**与 F「进入 stack」相同的单窗 `WrapInStackPanel`，不要 `StackAllWindows` 根级共享标签栏。
- **原因**：`ApplyStackLayout` 多窗时调 `StackAllWindows`；默认快捷键 F 绑在 `ToggleFloatingMode` 上。
- **最终处理**：
  - `Stack()` → `StackWindow(FocusedWindow)`；`WrapFocusedNodeInStackPanel` 仅 `WrapInStackPanel(焦点节点)`。
  - `SetPanelStackCore` → 遍历句柄 `TryEnterStackForWindow`。
  - 默认键：`CreateStackPanel` = F，`ToggleFloatingMode` = T。
  - 文件：`TilingService.Private.cs`、`TilingService.cs`、`BindableAction.cs`、`MainWindow.xaml.cs`；主需求 §1.2、§1.4。

---

## 2026-06-15 · Win+Shift+F 取消 stack 后无法再次 stack

- **现象**：F 可取消 stack，但再按 F 无法再次 stack；窗处于浮动，尺寸像居中缩小而非 stack 全屏。
- **用户期望**：
  - **取消 stack**：恢复**原始窗口**（浮动 + 原尺寸/位置），**不要**改成 split 平铺。
  - **再次 F**：应能再次单窗 stack。
- **原因（取消行为）**：中间误改为 `UnwrapStackShell` / `DetachWindowFromStackKeepTiled`（用户不要 split）。
- **原因（无法再次 stack）**：浮动态重注册走 `TryRegisterAutoTiledWindowCore` 的 `IsStackModeActive` 根 stack 路径，只进共享标签栏，无法 `WrapInStackPanel`。
- **最终处理**：
  - 取消：**恢复** `FloatSingleWindowFromStack` → `floatingSet` + `UnregisterWindow` + `OnWindowFloated`。
  - 再次 stack：新增 `EnsureRegisteredForManualStack`，用 `RegisterWindow(window, maxTreeWidth: 100)` 普通平铺注册后再 `WrapInStackPanel`。
  - 文件：`TilingService.Private.cs`（`StackWindow`、`UnstackSingleWindow`、`EnsureRegisteredForManualStack`）。
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
  1. `TryEnterStackForWindow` 依赖 `DetectChanges` + 已注册节点，未注册窗口直接 `return false`（未走 `EnsureRegisteredForManualStack`）。
  2. 枚举仅合并快照与 `m_windowSet`，且包含最大化窗（先还原再 stack），与用户「仅已还原可见窗」不符。
- **处理**：
  - `CollectTaskbarVisibleWindowsOnThisDisplay`：`RefreshConfiguration` 后按 `GetSnapshot`（任务栏可见）+ 当前虚拟桌面 + `WindowState.Restored` + 本显示器筛选。
  - `TryEnterStackForWindow` 改为 `EnsureRegisteredForManualStack`（与 Win+Shift+F 一致）。
  - 跳过最小化/最大化；双屏仍由各 `TilingService` 分别处理本屏。
- **文件**：`TilingService.Private.cs`；主需求 §1.2。

---

## 索引：主文档 §3 中未展开条目的简述

| 日期 | 标题 | 处理要点 | 文件 |
|------|------|----------|------|
| 2026-06-14 | 空 stack 标题栏崩溃 | `FirstOrDefault` | `TilingPanel.xaml.cs` |
| 2026-06-14 | Monorepo 编译失败 | `GitVersionBaseDirectory` | `Directory.Build.props` |
| 2026-06-14 | 再次 Stack 后标签全没 | overlay 修剪顺序 | `TilingOverlayRenderer.cs` |
| 2026-06-14 | 全屏+弹窗后取消全屏仅半屏 | 保留空 stack、`RepairRootStackLayout` | `PanelNode.cs`、`TilingService*.cs` |

---

## 维护说明

1. **每次**用户报 BUG、澄清产品行为、或完成一轮实质性修复后，在本文**顶部日期区下追加一节**（最新在上亦可，但须保持格式一致）。
2. 若条目已写入主文档 §3，本文须写**更完整的用户原话与方案演进**（含走过的弯路，如「曾误改为 split」）。
3. 改代码仍须同步 `FancyWM-主要需求与约定.md` §1–§3 与 `.github/pending_commit_notes.txt`。
