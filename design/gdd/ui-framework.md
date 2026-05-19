# UI 框架 (UI Framework)

> **Status**: In Design
> **Author**: 用户 + ui-programmer + gameplay-programmer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 4 (画卷中有呼吸) — 间接支撑——UI 是画卷的"画框"，框的手感影响观画的体验

## Overview

UI 框架是《回响》中所有用户界面的基础架构。它封装 Unity UI Toolkit 的面板栈管理、USS 样式系统和输入导航逻辑，为上层 UI 系统（主菜单、HUD、设置界面、结局呈现等）提供一致的界面基础设施——包括面板的打开/关闭/层叠、统一的视觉样式变量、键盘导航焦点管理、以及 UI 与 Gameplay 输入切换的门控逻辑。

在技术层面，它是一个面板管理器（Panel Stack）+ 一个样式资产库（Theme.uss + 颜色/字体变量）+ 一个导航控制器（焦点组遍历 + 输入消费）。UI 框架不包含任何面板的具体内容——每个菜单、HUD、对话框的具体布局和逻辑由各自的 UI 系统负责。UI 框架只回答三个问题：哪个面板在最前面、全局视觉风格是什么、键盘导航走到哪里去。

## Player Fantasy

UI 框架是基础设施——玩家不会"感受"到面板栈或 USS 样式表。玩家感受到的是：每次按下 Escape，暂停菜单悄无声息地滑入——不是"弹"出来，而是像宣纸被轻轻铺在画卷上。按钮悬停时边缘微微发亮，不是刺眼的闪烁，而是墨迹边缘正在渗开的那种缓慢扩散。所有这些触觉质感——面板的呼吸感、按钮的响应节奏、视觉焦点流动的自然感——是 UI 框架在无声中提供的。当 UI 框架做对了，玩家唯一会说的是"这游戏手感真好"——他们永远不会说"这面板管理器设计得真好"。

## Detailed Design

### Core Rules

**规则 1 — 底层技术：Unity UI Toolkit**：UI 框架基于 Unity UI Toolkit（`com.unity.ui`）构建。不使用 UGUI（Unity 6 已弃用于新项目）。

- 所有 UI 面板使用 UXML 定义结构 + USS 定义样式
- 每个面板是一个 `UIDocument` 或动态 `VisualElement` 树
- UI Toolkit 的 retained mode 渲染提供比 UGUI Canvas 更好的性能——对叙事游戏的淡入淡出过渡尤为重要
- 与 Unity Localization Package 兼容——`LocalizedString` 通过 `TextElement.text` 绑定

**规则 2 — 面板栈管理（Panel Stack）**：

```
[Top]    模态对话框 (选择确认、存档覆盖提示)
         ↓
         设置面板 (暂停菜单内)
         ↓
         主菜单 / 暂停菜单
         ↓
         HUD (始终在底层，Gameplay 状态可见)
[Bottom]
```

- **UIPanelStack** 类管理所有打开的 UI 面板——后进先出（LIFO）
- 栈操作：
  - `PushPanel(panelId)` — 打开面板，压入栈顶。前一个栈顶面板失去焦点但不关闭
  - `PopPanel()` — 关闭栈顶面板。新栈顶恢复焦点
  - `ReplaceTop(panelId)` — 替换栈顶面板（用于菜单间切换，如主菜单→设置）
- 最底层的 HUD 不在面板栈中——它是持久层，由 `GameplayInputActive` 标志控制可见性
- 面板栈为空 → 所有模态 UI 关闭 → 切换回 Gameplay Action Map

**规则 3 — 输入门控与 Action Map 切换**：

| 面板栈状态 | Action Map | HUD 可见 | 行为 |
|-----------|-----------|---------|------|
| 栈空（Gameplay） | Gameplay | 可见 | 输入系统 GDD 规则 4 的全部层级生效 |
| 栈非空（UI 打开） | UI | 隐藏（或变暗） | Gameplay Action Map 禁用，UI Action Map 启用 |
| 模态对话框在栈顶 | UI | 隐藏 | 仅对话框响应输入，下层 UI 面板不响应 |

- 面板栈从空变为非空 → `InputSystemManager.SwitchToUIMap()`（禁用 Gameplay，启用 UI）
- 面板栈从非空变为空 → `InputSystemManager.SwitchToGameplayMap()`
- 输入系统 GDD 规则的 `EventSystem.current.IsPointerOverGameObject()` 检测在 UI Toolkit 下通过 `PanelEventHandler` 替代——UI Toolkit 有自己的事件系统

**规则 4 — UI Toolkit 导航系统（Keyboard Navigation）**：

- 使用 UI Toolkit 的 `FocusController` 和 `Focusable` 接口管理键盘焦点
- 所有可交互元素（Button、TextField、Toggle、Slider）在 UXML 中设置 `focusable="true"`
- 导航键映射（通过输入系统的 UI Action Map）：
  - `Navigate` (Arrow Keys / D-Pad / Left Stick) → `FocusController.FocusNextInDirection()`
  - `TabNext` (Tab) → 焦点移到下一个焦点组（Focus Ring）
  - `TabPrevious` (Shift+Tab) → 焦点移到上一个焦点组
  - `Confirm` (Enter / Gamepad A) → 触发当前聚焦元素的 `clicked` 或 `Submit` 事件
  - `Cancel` (Escape / Gamepad B) → `PopPanel()` 关闭栈顶面板
- 面板切换时自动聚焦到新面板的第一个可交互元素
- 面板关闭时焦点回到前一个面板的最后聚焦位置

**规则 5 — 全局主题系统（Theme）**：

- 单一的 `Theme.uss` 文件定义全局样式变量——所有面板的 USS 使用 USS `var()` 引用这些变量
- 主题变量分类：

| 类别 | 变量前缀 | 示例 | 包含 |
|------|---------|------|------|
| 颜色 | `--color-` | `--color-bg-primary`, `--color-text-primary`, `--color-accent` | 背景、文本、强调色、边框色 |
| 字体 | `--font-` | `--font-size-h1`, `--font-size-body`, `--font-family` | 字号层级、字体系列 |
| 间距 | `--spacing-` | `--spacing-xs` (4px) 到 `--spacing-xl` (32px) | 内边距、外边距、元素间距 |
| 过渡 | `--transition-` | `--transition-fast` (0.1s), `--transition-normal` (0.3s) | 动画时长 |
| 面板 | `--panel-` | `--panel-bg-opacity`, `--panel-border-radius` | 面板外观 |
| 按钮 | `--button-` | `--button-height`, `--button-hover-brightness` | 按钮默认外观 |

- 每个面板 USS 的开头导入 Theme.uss：`@import url("theme.uss");`
- 颜色变量使用 RGB 分量（非 hex）以支持透明度动画：`--color-accent: 200, 160, 100;` 使用时 `rgba(var(--color-accent), 0.8)`
- 主题切换能力：Theme.uss 可被替换（如无障碍的高对比度主题）

**规则 6 — 视觉风格基调（Ink-Painting UI）**：

UI 视觉语言遵循游戏的水墨美学：
- **不透明度过渡**：面板出现/消失使用 opacity 淡入淡出（`transition-property: opacity`），不使用 Transform 位移动画——面板像墨在纸上显现，不像机械滑动
- **悬停反馈**：可交互元素 hover 时使用 `background-color` 微调 + `scale` 微放大（`transform: scale(1.02)`），过渡时间 0.2s——像光在墨迹边缘亮起，不像按钮高亮
- **手绘边框**：面板和按钮使用九宫格切片的装饰性边框图片（`-unity-slice-scale: 1`），模拟毛笔勾勒的不规则边缘
- **字体**：中文使用手写风格的衬线字体（如方正启体或类似毛笔手写体），英文使用相应的 serif 字体
- 以上规则为**全局默认**——每个面板 USS 可覆盖具体属性

**规则 7 — UI 过渡与动画**：

- 框架提供两个预定义的过渡类，在 USS 中通过 `opacity` + `transition` 实现：
  - `.fade-in` — opacity 0→1, transition 0.3s ease-out。面板打开时添加到根 VisualElement
  - `.fade-out` — opacity 1→0, transition 0.2s ease-in。面板关闭前添加，动画结束后移除 VisualElement
- 面板过渡由 `UIPanelStack` 自动触发——子系统不需手动管理动画
- 过渡时长通过 Theme.uss 的 `--transition-normal` 和 `--transition-fast` 变量控制

**规则 8 — 与本地化系统集成**：

- 所有文本使用 `LocalizedString` 绑定——不在代码中硬编码字符串
- UXML 中的 `TextElement` 通过 C# 端 `textElement.text = localizedString.GetLocalizedString()` 赋值
- `LocaleChanged` 事件触发时，所有当前可见面板刷新文本——由 Unity LP 的 `LocalizedString.StringChanged` 事件自动处理
- UI 框架不直接管理 StringTable——它只消费本地化系统已加载的字符串

### States and Transitions

| 状态 | 描述 | 条件 |
|------|------|------|
| **Empty** | 无模态 UI 面板打开（HUD 可见）。Gameplay Action Map 激活 | 游戏正常进行 |
| **PanelOpen** | 至少一个面板在栈中。UI Action Map 激活。栈中面板从底到顶依次渲染 | PushPanel() 触发 |
| **ModalDialog** | 模态对话框在栈顶。栈中的下层面板仍在但不响应输入 | 选择确认、保存覆盖提示等触发 |
| **Transitioning** | 面板正在打开或关闭的过渡动画进行中。过渡期间忽略同一面板的重复 Push/Pop | 面板淡入/淡出进行中 |

**状态转换**：
- Empty → PanelOpen（PushPanel 触发，自动切换到 UI Action Map）
- PanelOpen → PanelOpen（PushPanel——栈增加；PopPanel——栈减少）
- PanelOpen → Empty（PopPanel 移除最后一个面板，切换到 Gameplay Action Map）
- PanelOpen → ModalDialog（模态面板 PushPanel 到栈顶）
- ModalDialog → PanelOpen（模态面板 PopPanel 关闭）
- PanelOpen ↔ Transitioning（动画开始/结束）

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **输入系统 (#1)** | Navigate, Confirm, Cancel, TabNext, TabPrevious, Point | UI Action Map 激活时消费输入事件。Point 用于鼠标悬浮检测 |
| 上游 | **本地化 (#4)** | LocalizedString 组件 + StringChanged 事件 | 所有 UI 文本通过本地化系统获取 |
| 下游 | **游戏内 HUD (#17)** | 常驻 VisualElement 层 | HUD 是 UI 框架管理的持久层——不在面板栈中 |
| 下游 | **交互反馈 (#18)** | 过渡视觉元素（发光、涟漪）的渲染层 | 交互反馈的视觉表现叠加在 UI 框架之上 |
| 下游 | **主菜单 (#19)** | PushPanel / PopPanel API | 菜单系统通过面板栈管理所有菜单面板 |
| 下游 | **结局呈现 (#20)** | PushPanel API + 全屏覆盖层 | 结局画面的全屏呈现 |
| 下游 | **章节选择 (#21)** | PushPanel / PopPanel API | 章节选择界面的面板管理 |
| 下游 | **无障碍 (#22)** | Theme.uss 替换 + 焦点导航扩展 | 高对比度主题替换、文本缩放、减少动画开关 |

## Formulas

UI 框架不包含数学公式。它是一个面板管理、样式和导航基础设施。面板过渡的 opacity 插值和焦点导航的矩形计算由 Unity UI Toolkit 内部处理。

## Edge Cases

- **如果玩家在面板过渡动画期间快速按 Escape 两次**：Transitioning 状态下忽略重复的 Push/Pop 请求——等待当前动画完成后再处理下一个栈操作。过渡时长 < 0.3s，玩家感知不到延迟
- **如果面板栈为空但 UI Action Map 仍在激活（逻辑错误）**：框架自动检测并强制切换回 Gameplay Action Map。记录警告日志——这是不应发生的后台状态
- **如果 UXML 文件缺失或损坏**：`VisualTreeAsset.CloneTree()` 返回 null → 面板打开失败，记录错误。框架在 Release Build 中显示通用错误面板（引擎内置），在 Development Build 中显示缺失文件路径
- **如果键盘焦点组为空（面板中无可聚焦元素）**：焦点保持在上一面板的最后一个聚焦位置。在退出该面板之前，Cancel 键始终有效（Escape 总是触发 PopPanel）
- **如果 Theme.uss 文件缺失**：面板仍可显示但无样式——使用 Unity UI Toolkit 的默认样式（系统字体、默认颜色）。游戏可运行但视觉不一致
- **如果手柄热插拔**：UI 导航提示自动更新——手柄连接后显示手柄提示，断开后恢复键盘提示。输入系统 GDD 规则 8 负责设备检测，UI 框架订阅 `OnGamepadConnectionChanged` 事件
- **如果同时打开的面板数超过 10 层（不应发生但需防护）**：栈深度上限为 10。超过时拒绝 PushPanel 并记录错误。正常游戏应该不超过 3-4 层深度
- **如果 UI Toolkit 事件与 Gameplay 输入同时触发（边界条件）**：Action Map 切换在 PushPanel/PopPanel 之前执行——UI Action Map 激活后 Gameplay 输入不再被处理。不存在"同一帧同时处理"的情况

## Dependencies

**硬依赖**：

| 系统 | 依赖性质 | 接口 |
|------|----------|------|
| 输入系统 (#1) | 硬依赖 | Navigate, Confirm, Cancel, TabNext, TabPrevious, Point + Action Map 切换 |
| 本地化 (#4) | 硬依赖 | LocalizedString 组件 |

**下游系统**（全部硬依赖——7 个系统）：
HUD (#17)、交互反馈 (#18)、主菜单 (#19)、结局呈现 (#20)、章节选择 (#21)、无障碍 (#22)、画廊 (#24)

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| Panel Fade-In Duration | 0.3s | 0.1–0.5s | 面板打开淡入时长 |
| Panel Fade-Out Duration | 0.2s | 0.1–0.3s | 面板关闭淡出时长 |
| Max Panel Stack Depth | 10 | 5–20 | 面板栈最大深度 |
| Hover Scale Amount | 1.02 | 1.0–1.05 | 按钮悬停放大比例 |
| Hover Transition Duration | 0.2s | 0.1–0.3s | 悬停效果过渡时长 |

## Visual/Audio Requirements

UI 框架提供全局视觉样式资产（Theme.uss + 装饰性边框素材），但本身不产生独立的视觉或音频输出。具体的视觉呈现由各 UI 子系统负责。音频反馈（按钮点击声、面板开关声）由交互反馈系统 (#18) 播放——UI 框架仅提供过渡动画的视觉部分。

## UI Requirements

UI 框架本身**是** UI 基础设施——它不存在"它的 UI"。它的"用户"是 UI 程序员——通过 UXML/USS 创作面板，通过面板栈 API 管理面板流。

## Acceptance Criteria

- **GIVEN** 游戏处于 Gameplay 状态，**WHEN** 玩家按下 Escape，**THEN** 暂停面板 PushPanel 到栈顶，Gameplay Action Map 禁用，UI Action Map 启用。面板以 opacity 淡入（0.3s）出现
- **GIVEN** 暂停面板打开，**WHEN** 玩家再次按 Escape，**THEN** PopPanel 关闭暂停面板，切换到 Gameplay Action Map。面板以 opacity 淡出（0.2s）
- **GIVEN** 两个面板在栈中（暂停 → 设置），**WHEN** 玩家按 Escape 两次，**THEN** 设置面板先关闭（回到暂停），暂停面板再关闭（回到 Gameplay）。焦点正确恢复到前一个面板
- **GIVEN** 设置面板中"音量滑块"聚焦，**WHEN** 玩家按 Arrow Down，**THEN** 焦点移到下一个可交互元素。焦点视觉指示器（outline）可见
- **GIVEN** 无手柄连接，**WHEN** 任意菜单打开，**THEN** 不显示手柄按钮提示——仅显示键盘提示
- **GIVEN** Theme.uss 中 `--color-accent` 被修改为高对比度值，**WHEN** 菜单面板重新渲染，**THEN** 所有使用该变量的元素反映新颜色
- **GIVEN** 面板栈为空，**WHEN** 检查 Action Map 状态，**THEN** Gameplay Action Map 激活，UI Action Map 禁用
- **GIVEN** UXML 文件缺失，**WHEN** 尝试 PushPanel，**THEN** Development Build 中记录错误并显示包含文件路径的错误信息。Release Build 中显示通用错误面板

## Open Questions

- **字体具体选型**：中文手写风格衬线字体的具体选择（方正启体 vs 其他）——由 art-bible 决定。如果所选字体文件较大（>10MB），需放入 Shared_UI Addressables 组并评估启动加载时间
- **UI Toolkit 与世界空间 UI 的兼容性**：MVP 阶段所有 UI 为 Screen Space。如果 Vertical Slice 需要在记忆画卷中嵌入世界空间 UI（如浮动的记忆碎片标签），UI Toolkit 的 World Space 支持需要验证
- **CSS Transition 动画 vs C# 协程动画**：当前设计使用 USS `transition` 属性做淡入淡出——这是声明式动画。如果 Vertical Slice 需要更复杂的过渡（如面板从特定位置展开），可能需要 C# 协程驱动动画。是否应预留在 UIPanelStack 中？
