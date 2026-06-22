# FancyWM 主要需求与约定

本文档记录产品主需求、已发现问题与修复、以及用户建议中属于需求范畴的条目。  
**改代码前请先对照本文；涉及需求的新 BUG、建议、行为变更须同步更新本文。**

最后更新：2026-06-16

---

## 1. 核心功能需求（必须满足）

### 1.1 窗口自动平铺：白名单模式

- **原则**：窗口**自动**纳入平铺须采用**白名单**，而非上游默认的「除排除项外一律自动平铺」（黑名单式）策略。
- **默认**：新窗口**保持浮动**，维持原始尺寸与位置；**仅当**匹配「设置 → 规则」中的 Include 列表时才自动平铺：
  - 按进程名（`ProcessIncludeList`）
  - 按进程实例（`ProcessInstanceIncludeList`）
  - 按窗口类（`ClassIncludeList`）
- **动机**：自动排版会把大量**临时弹出窗口**（对话框、工具窗、菜单窗等）纳入布局，导致尺寸被拉伸或缩到异常大小。
- **与 `CanManage` 的区别**：`CanManage` 表示窗口*可被* FancyWM 管理；**不等于**应自动平铺。自动注册路径须以 `ShouldAutoTile` / `InclusionMatchers` 为准。
- **边界**：
  - **手动操作**（快捷键平铺/分割/Stack、overlay 操作等）仍可对**当前**窗口生效。
  - **不得**因用户手动平铺某一窗口，就隐式把**同进程后续弹窗**加入白名单（除非用户在 overlay 选「为进程添加规则」，或在规则页显式配置）。
  - **显式批量操作**不受本条限制：托盘 Set Panel Stack（§1.2）、Win+Shift+F（§1.4）等仍按各自需求执行。
- **实现要点**：`ShouldAutoTile` 仅匹配 `InclusionMatchers`；`ToggleFloat` 不写入 `ProcessInstanceIncludeList` / `m_autoTileProcessIds`；`DetectChanges` 自动路径须校验白名单。

### 1.2 托盘「Set Panel Stack」

- **入口**：**仅**托盘右键菜单；无其他快捷键或入口可触发整屏 stack。
- **行为**：将本屏每个**符合条件**的可管理窗口，**全部并入该显示器布局树的根级 `StackPanel`**（屏幕**顶部共享 stack 标签栏**）。实现上通过 `GetOrCreateRootStackPanel` + `MergeWindowsIntoRootStack` / `MergeWindowsIntoStack`；**不得**对每个句柄单独 `WrapInStackPanel` 造「每窗/每进程独立 stack 壳」。
- **范围**：多显示器时，各 `TilingService`（独立布局树）分别把**本屏**窗口并入**本屏**根级标签栏。
- **窗口枚举**：刷新工作区后，遍历当前虚拟桌面上**任务栏可见**窗口（`GetSnapshot` / `IsTopLevelVisible`）；仅处理 **已还原**（非最小化、非最大化）且落在该显示器上的窗口。
- **跳过**：最小化、最大化、辅助弹窗（§1.6）、`CanManage` 为 false 的窗口。

### 1.3 Stack 模式与顶部标签栏

- **共享标签栏**：每块显示器在 stack 模式下只有**一个**根级 `StackPanel` 顶栏；各主窗口以**标签页**形式同栏切换，**不是**每窗一套独立 stack 面板。
- **右键拖动标签**：在 stack 顶栏用**右键**拖动标签，可调整 stack 内窗口顺序（`TabBar` + `StackTabReorderRequested`）。
- **标签不得无故消失**：stack ↔ 非 stack 切换、布局刷新后，标签栏应与 stack 子节点一致（见 §3 已修复项）。
- **全部最小化/注销后**：根级 `StackPanel` 无子窗口时**不显示**顶部 stack 标签栏（overlay 过滤空 stack）；布局树可保留空 stack 以便还原后恢复 stack 模式。

### 1.4 Win + Shift + F / F1 与 Stack

- **用途**：对**当前激活窗口**做 stack **单窗 toggle**（`Stack()` → `StackWindow`）；与托盘整屏 stack **共用同一根级标签栏**，不是独立 stack 壳。
- **进入 stack**：焦点窗不在 stack 标签栏内时，**加入**本屏根级 `StackPanel`（`TryJoinRootStack` / `RegisterWindow(window, rootStack)` + merge）。
- **取消 stack**（再按 Win+Shift+F 或 F1）：从标签栏移出该窗，恢复**原始窗口**（浮动 + `OnWindowFloated` 原尺寸/位置），**不得**拆成 split 平铺。
- **再次 stack**：浮动态重新注册并 merge 进根级共享标签栏（`GetOrCreateRootStackPanel`）。
- **与 1.2 的关系**：托盘 = 本屏**全部**符合条件窗口一次并入标签栏；Win+Shift+F / F1 = **仅当前焦点窗** toggle 进出标签栏。
- **默认快捷键**：`CreateStackPanel` = Win+Shift+F；`ToggleFloatingMode`（单窗浮动）= Win+Shift+T。
- **F6 直接快捷键**（可选）：单独按 **F6** 与 Win+Shift+F 相同；设置 → 交互 →「启用 F6 stack 快捷键」/`EnableF6StackHotkey`，**默认启用**；关闭后不注册 F6 钩子。
- **取消最大化回 stack**（可选）：设置 → 交互 →「取消最大化后回到 stack」/`AutoStackOnUnmaximize`，**默认启用**（当前行为）；关闭后从最大化还原的窗口**保持浮动**，不自动进 stack 标签栏（从最小化还原仍按原逻辑回 stack）。

### 1.5 双显示器行为一致

- **原则**：窗口在 stack / 非 stack 状态下，从一块屏拖到另一块屏，**布局语义应一致**。
- **Stack 跨屏**：
  - 目标屏若已是 stack 模式，迁入窗口应**加入该屏 stack**，不得作为根级分屏占半屏。
  - 跨屏离开 stack 时记录转移；进入新屏后恢复 stack 归属。
- **与 1.2 的区别**：托盘一次 stack 本屏全部窗口；Win+Shift+F / F1 只 toggle **当前焦点窗**进出**同一**根级标签栏。

### 1.6 Stack 模式下新弹出窗口

- **禁止**：在 stack 模式下，新窗口**不得自动分屏**挤占半屏。
- **应然行为**：
  - **主窗口**（已纳入 stack 的应用主界面）→ 保持 stack。
  - **辅助弹窗**（同进程内的查找/替换、查询对话框、`#32770`、有 Owner 的对话框、无任务栏按钮的小窗等）→ **保持浮动**，维持**原始尺寸与位置**，**不得**加入 stack（见 `AuxiliaryWindowRules`）。
  - **全新进程**（本屏尚无同进程已管理窗口）→ **保持浮动**，按**原始窗口尺寸**显示，不强制平铺。
- **识别依据**（主窗口 vs 辅助弹窗，可组合）：
  - 窗口类 `#32770`、扩展样式 `WS_EX_DLGMODALFRAME` / `WS_EX_TOOLWINDOW`（且无 `WS_EX_APPWINDOW`）。
  - 同进程内存在 **Owner** 关系（`GW_OWNER`）。
  - 无最大化/最小化按钮的 `WS_POPUP` 标题窗。
  - 同进程内已有更显著主窗口，且本窗面积明显更小（如 Notepad++/Excel 查找框）。
- **与白名单关系**：辅助弹窗视同**不在自动平铺白名单**，即使用户在规则里配置了进程名。
- **全屏/最大化**：stack 内窗口最大化时会从布局树注销；须**保留根级空 `StackPanel`**，避免 stack 模式丢失；取消全屏后 stack 应恢复**整屏**而非半屏。

### 1.7 Win+Shift 激活时的提示

- **保留**：快捷键列表（「设置 → 界面」→「显示上下文提示」/`ShowContextHints` 开启时，按下 Win+Shift 后显示当前可用快捷键）。
- **移除**（Win+Shift 流程内）：
  - 捐赠/评价类托盘 balloon（原「喜欢 FancyWM 吗？」/ 赞助提示）。
  - 「等待操作…」「按 F12 获取帮助」等非快捷键 toast。
  - 未识别快捷键的文字 toast（可保留短促 beep 作反馈）。
- **保留**：操作失败、平铺异常、崩溃等**异常类**提示（`TilingFailedException`、`OnTilingFailed`、`OnWorkspaceUnhandledException` 等）。

### 1.8 本分支默认设置（与上游差异）

- **界面语言**：默认 **简体中文**（`UiLanguage` = `zh-CN`）；`LocalizationService` 无 `settings.json` 时亦回退中文。
- **管理员启动**：默认 **启用**（`RunsAsAdministrator` = `true`）；启动时若未提权则 UAC 重启；设置页可关闭并写入 `settings.json`；仍兼容旧版 `administrator-mode` 标记文件。
- **取消最大化回 stack**：见 §1.4，**默认启用**（`AutoStackOnUnmaximize` = `true`）。
- **说明**：已有 `settings.json` 中显式保存的值优先于代码默认值；重置或删除配置后按上列默认生效。

---

## 2. 工程与构建约定

### 2.1 单仓库（Monorepo）

- 仅根目录 `fancywm` 一个 Git 仓库；`ModernWpf`、`winman`、`winman-windows` 为普通目录，**不得**再作为子模块独立管理。
- `Directory.Build.props` 中 `GitVersionBaseDirectory` 指向仓库根，避免 NBGV 遍历历史子模块 gitlink 导致编译失败。

### 2.2 本地构建输出

| 路径 | 含义 |
|------|------|
| `Release\<时间戳>\` | 框架依赖完整构建（`AutoBuild_Framework.bat`） |
| `Release\latest\` | 最新完整构建（镜像） |
| `Release\latestmin\` | **增量更新包**（见下表） |
| `Release\SelfContained\...` | 自包含构建（`AutoBuild_SelfContained.bat`），同样有 `latest` / `latestmin` |

**`latestmin` 内容**（覆盖到目标机已有完整安装目录）：

- `FancyWM.dll`、`FancyWM.exe`
- `FancyWM-GUI.exe`、`FancyWM-GUI.dll`
- 各语言目录下的 `FancyWM.resources.dll`（若存在）

目标机使用 Framework 构建时需安装 **.NET 10 Desktop Runtime**。资源与 XAML 已编译进 DLL，**不能**只换 `FancyWM.dll` 而忽略 `FancyWM.exe`、`FancyWM-GUI.exe` 等启动文件。

### 2.3 提交工作流

- Agent 收尾写 `.github/pending_commit_notes.txt`，**不自动** `git commit` / `push`（见 `.cursor/rules/pending-commit-notes.mdc`）。

---

## 3. 已发现 BUG 与修复记录

| 日期 | 现象 | 原因/处理 | 相关文件/备注 |
|------|------|-----------|----------------|
| 2026-06-14 | 点击空 stack 面板标题栏崩溃 | `ChildNodes.First()` 在空集合抛异常 → `FirstOrDefault()` | `TilingPanel.xaml.cs` |
| 2026-06-14 | Monorepo 转换后编译失败 | NBGV 解析历史子模块 gitlink → `GitVersionBaseDirectory` | `Directory.Build.props` |
| 2026-06-14 | 再次 Stack 后标签全没 | `PruneUnreachableViewModels` 在 `SyncChildNodes` 之后误删 → 调整顺序 | `TilingOverlayRenderer.cs` |
| 2026-06-14 | 托盘 Set Panel Stack 后跨屏丢 stack | 新屏注册要求同进程才进 stack → 全屏 stack 下一律进 root stack + 跨屏转移 | `TilingWorkspace.cs`、`TilingService.Private.cs` |
| 2026-06-14 | 跨屏拖动 stack 窗口后变 float | 转移标记晚于目标屏 DetectChanges / 白名单拦截 → 拖动中提前 Remember、跨屏注册绕过 float | `TilingService.Private.cs` |
| 2026-06-20 | 双屏拖动 stack 到另一屏仍变 float | 拖动中 `DoWindowMove` 先脱离 stack，转移标记写不进去 → 记录 drag origin、拖动期间跳过布局移动 | `TilingService.cs`、`TilingService.Private.cs` |
| 2026-06-14 | 双屏切换激活后 stack 标签变少 | Refresh 时白名单策略把已 stack 窗标为 floating，下次 DetectChanges 误注销 → 已注册窗不因 floating 注销；Refresh 仅处理本屏 | `TilingService*.cs`、`TilingOverlayRenderer.cs` |
| 2026-06-14 | Stack 下同进程查找/查询弹窗被纳入 stack | 同进程新窗一律进 stack → `AuxiliaryWindowRules` 识别辅助弹窗并保持浮动 | `AuxiliaryWindowRules.cs`、`TilingService*.cs` |
| 2026-06-14 | Stack + 全屏 + 弹窗后取消全屏仅半屏 | 空 stack 被移除 + 新窗注册到根分屏 → 保留空 stack、stack 模式注册策略、`RepairRootStackLayout` | `PanelNode.cs`、`TilingService*.cs` |
| 2026-06-15 | 取消 stack 后无法再次 Win+Shift+F stack | 浮动态重注册误进根 stack 标签栏 → EnsureRegisteredForManualStack + RegisterWindow(maxTreeWidth) | `TilingService.Private.cs` |

---

## 4. 用户建议（已采纳 / 待办）

| 状态 | 内容 |
|------|------|
| 已采纳 | 构建后同步 `latest` 与 `latestmin` |
| 已采纳 | 增量目录名由 `OnlyUpdate` 改为 `latestmin` |
| 已采纳 | 需求与 BUG 记入本文，Agent 改代码须对照并更新 |
| 已采纳 | 窗口自动平铺采用白名单模式（§1.1），避免临时弹窗被自动拉伸 |
| 已采纳 | Win+Shift 激活时仅保留快捷键列表提示，移除捐赠/评价等无关 toast（§1.7） |
| 已采纳 | 辅助弹窗（查找/查询等）不纳入 stack，保持原尺寸浮动（§1.6） |
| 已采纳 | Stack 统一使用显示器**顶部共享标签栏**（根 `StackPanel`）；托盘整屏并入、F6/Win+Shift+F 单窗 toggle 进出同一标签栏（§1.2–§1.4） |
| 已废止 | ~~每窗/每进程独立 `WrapInStackPanel` stack 壳~~（2026-06-15 文档误记，非用户本意） |
| 已采纳 | 问题与解决记入 `FancyWM-问题与解决记录.md`（Agent 见 `issue-resolution-log.mdc`） |

---

## 5. 文档维护约定（Agent 与用户）

1. **改代码前**：阅读本文 §1、§2；浏览 `FancyWM-问题与解决记录.md` 相关条目。
2. **改代码后**：更新本文 §1–§4（若涉及）；在 `FancyWM-问题与解决记录.md` **底部**追加条目（规则见 `issue-resolution-log.mdc`）；写 `.github/pending_commit_notes.txt`。
3. **修复 BUG 或采纳建议后**：若涉及行为/需求，在 §3 或 §4 追加一行（日期、现象、处理、文件）。
4. **新增主需求**：写入 §1，并注明快捷键/入口/多显示器等边界。
5. **纯重构、无行为变化**：可不改本文；若不确定是否影响行为，宁可补一条说明。

---

## 6. 关键代码索引（便于检索）

| 主题 | 主要位置 |
|------|----------|
| Set Panel Stack | `TilingService.Private.cs` → `SetPanelStackCore`、`CollectTaskbarVisibleWindowsOnThisDisplay` |
| Win+Shift+F / F1 Stack | `TilingService.cs` → `Stack()` / `StackWindow`；`TilingService.Private.cs` → `TryJoinRootStack`、`FloatSingleWindowFromStack` |
| 根级共享标签栏 | `TilingWorkspace.cs` → `GetOrCreateRootStackPanel`、`MergeWindowsIntoRootStack`、`StackAllWindows` |
| 问题与解决全文 | `Design/Requiment/FancyWM-问题与解决记录.md` |
| 标签同步 | `TilingOverlayRenderer.cs` → `SyncChildNodes`、`UpdateViewModels` |
| 自动平铺白名单 | `TilingService.Private.cs` → `ShouldAutoTile`、`DetectChanges`；`MainWindow.xaml.cs` → `InclusionMatchers`；规则页 Include 列表 |
| 辅助弹窗识别 | `FancyWM/Utilities/AuxiliaryWindowRules.cs` |
| Stack 新窗注册 | `TilingService.Private.cs` → `TryRegisterAutoTiledWindow*`、`ShouldFloatNewWindowInStackMode`、`ShouldKeepAuxiliaryFloating` |
| 跨屏 stack | `TilingWorkspace.cs` → `TryGetRootStackPanel`、`GetOrCreateRootStackPanel` |
| 空 stack 保留 | `FancyWM.Layouts/.../PanelNode.cs` → `RemoveIfEmpty` |
| Win+Shift 提示 | `MainWindow.xaml.cs` → `OnCmdSequenceBegin`、`ShowWaitingForActionToast` |
| 构建同步 | `AutoBuild_SyncLastRelease.bat`、`AutoBuild_SyncOnlyUpdate.bat`（输出 `latestmin`） |
