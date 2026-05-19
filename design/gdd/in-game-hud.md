# 游戏内HUD (In-Game HUD)

> **Status**: Designed (pending review)
> **Author**: 用户 + ui-programmer + game-designer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 1 (选择即重写) + Pillar 3 (关联的网络) — HUD 是选择界面和关联路径的视觉呈现

## Overview

游戏内 HUD 是《回响》中玩家与记忆世界之间的"视觉语言"。当关联引擎计算出候选路径、当交互系统弹出选择面板、当碎片展示一段文本——HUD 将这些信息转化为画卷上的视觉元素：不遮挡画面的墨色提示、不打断沉浸的选择面板、不破坏呼吸感的进度指示。HUD 是 UI 框架 (#5) 中最底层的持久面板——Gameplay 状态下始终可见，UI 面板打开时隐藏。

在技术层面，它是一个 UI Toolkit VisualElement 树，挂载在 Game 场景的 UIDocument 上。它消费来自交互系统 (#11)、关联引擎 (#13)、章节管理 (#15) 的数据，将它们渲染为墨迹风格的界面元素。HUD 自身不产生游戏逻辑——它只负责"把数据画在屏幕上，把玩家的点击传回系统"。

## Player Fantasy

HUD 不是"UI 覆盖层"——它是画卷上的墨迹。选择面板的文字不是系统字体打印的——它们是手写体，每一笔都有墨的浓淡。候选路径不显示为按钮列表——它们散落在画面边缘，像墨滴在宣纸上洇开的痕迹，有的浓烈（Strong），有的若有若无（Faint）。你看不到"按 Tab 切换选项"的提示——你看到的是一条墨迹从你当前的位置伸向下一幅画的边缘，而那幅画的边缘正在微微发光。

## Detailed Design

### Core Rules

**规则 1 — HUD 架构: UI Toolkit VisualElement 树**

```
HUD (VisualElement, Game 场景 UIDocument 内)
├── #fragment-text-overlay      // 碎片文本浮层 (ShowFragmentText)
├── #choice-panel               // 选择面板 (ShowChoicePanel)
│   ├── #choice-prompt          // 选择提示文本
│   └── #choice-options         // 选项列表容器
│       ├── .choice-option      // 单个选项 (手写墨迹 + 朱砂墨点)
│       └── ...
├── #association-paths          // 关联路径可视化
│   ├── .path-candidate         // 单个候选路径
│   │   ├── .ink-trail          // 墨迹拖痕 (从当前碎片到候选)
│   │   ├── .scent-label        // 气味标签 (可选——显示关联强度)
│   │   └── .target-indicator   // 目的地标记
│   └── ...
├── #chapter-progress           // 章节进度
│   ├── #chapter-name           // 当前章节名
│   └── #fragment-count         // 已访问/总碎片数 (可选——极简显示)
└── #interaction-hint           // 交互提示 (悬停物件名、操作提示)
```

HUD 是 UI 框架 (#5) 面板栈之下的持久层——不在栈中，由 `GameplayInputActive` 标志控制可见性。

**规则 2 — 选择面板 (Choice Panel)**

由交互系统 (#11) 在 `ResultType = PresentChoice` 时通过 `ShowChoicePanel(ChoiceGroup)` 触发：

```
ShowChoicePanel(ChoiceGroup group):
  1. InputSystem.SwitchToUIMode()  // 暂停 Gameplay 交互
  2. #choice-panel 可见 = true
  3. #choice-prompt.text = group.GroupLabel (TableReference → 本地化)
  4. 为 group.Choices 中的每个 ChoiceOption 创建 .choice-option:
     - 朱砂墨点 (L1 墨点图标——默认静态)
     - 选项文本 (手写体, TextMeshPro 字体)
     - 悬停时: 墨点进入 L2 脉动 (微动画 #9)
  5. 面板定位在锚点物件旁边:
     - 优先右侧 (offset: +40px horizontal)
     - 空间不足时下方 (offset: +20px vertical)
     - 超出画面边缘时翻转到左侧
  6. 键盘焦点自动移到第一个选项

玩家选择:
  - 点击选项 → HUD 调用 ChangeTracker.ApplyChanges(option.ContentChanges) → HideChoicePanel
  - 按 Escape → HideChoicePanel (不做选择——"不做选择是有效的交互结果" #11 Edge Cases)
  - 若 group.MaxSelections=1 且仅 1 个可用选项 → 跳过面板，直接触发 (由交互系统 #11 处理——HUD 不参与)
```

**规则 3 — 关联路径可视化 (Association Paths)**

由关联引擎 (#13) ComputeAssociations 返回 AssociationCandidate[] 后渲染：

```
ShowAssociationPaths(AssociationCandidate[] candidates):
  1. 清空 #association-paths
  2. FOR EACH candidate IN candidates (最多 Top-5):
     创建 .path-candidate:
     
     视觉元素:
     - .ink-trail: 从画面中央 (当前碎片) 到目标方向的墨线
       强度映射: Strong → 浓墨, Medium → 半透明墨, Faint → 淡墨, Trace → 极淡/虚线
     - .target-indicator: 在墨线末端的目的地标记 (朱砂圈)
       强度 → 圈的大小: Strong=大, Medium=中, Faint=小, Trace=点
     - .scent-label (可选): 显示 DominantFactor 的文字提示
       "标签共鸣" / "叙事线索" / "新鲜的气味" / "调色板中的呼吸"
     
     交互:
     - 悬停 .path-candidate → .ink-trail 变亮 / .target-indicator 脉动 (L2)
     - 点击 .path-candidate → 调用 ChapterManager.TransitionToFragment(candidate.TargetFragmentId)
     - 键盘导航: Arrow Keys 在候选之间移动焦点
     
  3. 候选不足 3 个时: 不缩放视觉——保持正常大小。不填补空位
  4. 候选超过 5 个时: 仅渲染 Top-5 (关联引擎规则 8)
```

关联路径不渲染为按钮列表——它们渲染为从画面中心向外辐射的墨迹。每条墨迹的方向由候选碎片在章节中的"情感空间"位置决定（Editor 中预设的方向角度——若未设置则均匀分布）。

**规则 4 — 碎片文本浮层 (Fragment Text Overlay)**

由交互系统 (#11) 在 `ResultType = ShowText` 时通过 `ShowFragmentText(TextContent)` 触发：

```
ShowFragmentText(TableReference textRef):
  1. #fragment-text-overlay.text = Localization.GetString(textRef)
  2. 文本出现在画面指定位置 (由交互系统传入的 screenPosition 决定)
  3. 文本样式: 手写体, 半透明墨色, 无背景框
  4. 持续 4 秒后自动淡出 (或点击任意位置提前关闭)
  5. 文本不阻挡其他交互——picking-mode: ignore
```

**规则 5 — 章节进度指示 (Chapter Progress)**

```
UpdateChapterProgress(string chapterKey, int visitedCount, int totalFragments):
  #chapter-name.text = ChapterDefinition.DisplayNameKey (本地化)
  #fragment-count 更新:
    MVP 极简版: 一行墨点——每个碎片一个点
    - 已访问: 实心朱砂墨点
    - 未访问: 空心淡墨圈
    - 当前: 脉动 (L2)
    - 排列: 水平一行，在画面底部边缘
```

进度指示在每次 `OnFragmentChanged` 事件触发时更新。极简设计——不显示数字，不显示百分比。

**规则 6 — 交互提示 (Interaction Hint)**

光标悬停在 InteractiveObject 上时显示：

| 交互类型 | 提示文本 | 视觉 |
|---------|---------|------|
| Touch | 物件名 (TableReference) | 手写体小字，出现在光标上方 20px |
| Drag | "拖拽" + 物件名 | 同上 |
| Hover | 物件名 + "..." | 同上——但延迟 0.5s 后才出现 |
| Examine | "细看" + 物件名 | 放大镜图标 + 手写体 |

提示由交互系统 (#11) 的悬停检测触发——HUD 只负责渲染。

**规则 7 — HUD 显示/隐藏规则**

| 游戏状态 | HUD | 说明 |
|---------|-----|------|
| Gameplay 活跃 | 完全可见 | 关联路径 + 进度 + 悬停提示 |
| 选择面板展示中 | 仅选择面板可见 | 关联路径和进度隐藏（减少视觉噪音） |
| 过渡中 (FadeOut/FadeIn) | 完全隐藏 | SceneFader 遮罩覆盖 HUD |
| UI 面板打开 (暂停/设置) | 完全隐藏 | 面板栈非空——UI 框架规则 3 |
| 纯观看碎片 (0 InteractiveObject) | 仅进度 + 关联路径 | 无交互提示 |

**规则 8 — MVVM 数据绑定**

HUD 使用 UI Toolkit 的 `Binding` 系统连接数据源：

```csharp
// 关联路径数据源
public class AssociationPathsDataSource : INotifyBindablePropertyChanged
{
    public List<PathCandidateData> Candidates { get; set; }
}

// 章节进度数据源
public class ChapterProgressDataSource
{
    public string ChapterName { get; set; }
    public int VisitedCount { get; set; }
    public int TotalCount { get; set; }
}
```

- 数据变化时 → 数据源触发 property changed → UI Toolkit 自动更新绑定元素
- HUD 的 `Update()` 不轮询——事件驱动更新
- 但在 `OnFragmentChanged` 事件触发时必须手动刷新（UI Toolkit 绑定不会自动感知 Unity 事件）

**规则 9 — MVP 范围**

MVP 包含:
- 选择面板（2-3 选项，单选）
- 关联路径可视化（Top-5 候选，Strength 视觉分级）
- 碎片文本浮层
- 章节进度（极简墨点）

MVP 不包含:
- DominantFactor 的 .scent-label 文字标签（关联路径上仅视觉强度区分，不显示文字）
- 关联路径的情感空间方向预设（均匀分布即可）
- 复杂动画过渡——墨迹路径的动画留给 Vertical Slice

### States and Transitions

HUD 自身无独立状态机——其可见性和内容由外部系统控制。关键可见性规则见规则 7。

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **UI 框架 (#5)** | 面板栈状态、Theme.uss 变量、键盘导航 | HUD 是 UI 框架的持久底层——使用主题变量和导航系统 |
| 上游 | **交互系统 (#11)** | ShowChoicePanel(ChoiceGroup), ShowFragmentText(TextContent), HideChoicePanel() | 选择面板和文本浮层的触发来源 |
| 上游 | **关联引擎 (#13)** | AssociationCandidate[] (通过 ChapterManager 或直接订阅) | 关联路径的数据源 |
| 上游 | **章节管理 (#15)** | OnFragmentChanged 事件 → UpdateChapterProgress | 进度指示的更新触发 |
| 下游 | **变化追踪 (#12)** | ApplyChanges(ContentChange[]) | 玩家在选择面板中做出选择后触发 |
| 下游 | **章节管理 (#15)** | TransitionToFragment(targetFragmentId) | 玩家选择关联路径目标后触发 |
| 下游 | **微动画 (#9)** | L2 脉动 (选项悬停时) | 墨点的悬停动画 |

## Formulas

本系统不含自定义公式。元素位置计算公式:

- **选择面板定位**: 优先 `anchorPosition + (40, 0)`, 若超出画面右边界 → `anchorPosition - (panelWidth + 40, 0)`, 若仍超出 → `anchorPosition + (0, 20)` (下方)
- **关联路径墨线方向**: 均匀分布 `360° / candidateCount` 从正上方顺时针（若无预设方向）

## Edge Cases

- **关联引擎返回 0 个候选**: #association-paths 容器保持空——不显示任何路径。章节完成检测将触发——HUD 不需要特殊处理。
- **ChoiceGroup 的所有选项都有不可满足的 ChoiceCondition**: 交互系统 (#11) 应在调用 ShowChoicePanel 前过滤不可用选项。若过滤后为 0 → 不调用 HUD。若过滤后仅 1 个 → 直接触发 (规则 2)。
- **玩家在文本浮层展示中切换到新碎片**: 浮层自动关闭——OnFragmentChanged 触发时清除 #fragment-text-overlay。
- **关联路径方向重叠 (5 个候选均匀分布，但仅 3 个时角度间隙大)**: 方向数量 = 候选数量，均匀分布。无重叠问题。若未来支持预设方向——Editor 验证检测角度冲突。
- **长文本在浮层中溢出**: CSS `text-overflow: ellipsis` 在 UI Toolkit 中通过 `overflow: hidden` + 最大宽度 clamp 处理。

## Dependencies

### 硬依赖

| 系统 | 性质 | 接口 |
|------|------|------|
| **UI 框架 (#5)** | 硬依赖 — HUD 在 UI Toolkit 中渲染，依赖面板栈、主题、导航 | UIDocument, Theme.uss, 面板栈状态 |
| **交互系统 (#11)** | 硬依赖 — 选择面板/文本浮层的触发来源 | ShowChoicePanel, ShowFragmentText, HideChoicePanel |

### 软依赖

| 系统 | 性质 | 接口 |
|------|------|------|
| **关联引擎 (#13)** | 软依赖 — 关联路径数据源 | AssociationCandidate[] |
| **章节管理 (#15)** | 软依赖 — 进度更新、碎片切换触发 | OnFragmentChanged 事件 |
| **变化追踪 (#12)** | 软依赖 — 选择提交 | ApplyChanges(ContentChange[]) |
| **微动画 (#9)** | 软依赖 — 墨点悬停脉动 | L2 脉动触发 |
| **输入系统 (#1)** | 软依赖 — 键盘导航和点击 | UI Action Map (通过 UI 框架) |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 文本浮层持续时长 | 4.0s | 2.0–8.0s | 文本自动淡出前的展示时间 |
| 选择面板定位偏移 X | 40px | 20–80px | 面板距锚点物件的水平距离 |
| 关联路径墨线最大长度 | 200px | 100–300px | 墨迹辐射的最大长度 |
| 候选方向起始角度 | -90° (正上方) | 0°–360° | 第一个候选的方向 |

## Visual/Audio Requirements

- **字体**: 手写体中文字体 (TextMeshPro) — 选项文本、提示文本、章节名
- **墨点**: 朱砂红色圆形——L1 静态 / L2 脉动 / L3 内光 (由微动画 #9 管理)
- **墨线**: 半透明黑色曲线——Strong 浓 / Medium 半透明 / Faint 淡 / Trace 虚线
- **无背景框**: 选择面板没有传统 UI 背景——选项文字直接浮在画面上，周围有微弱的墨色光晕
- **音效**: 选择确认音效由交互反馈系统 (#18) 管理——HUD 不产生音频

## UI Requirements

所有 HUD 元素使用 UI Toolkit UXML + USS 定义。样式变量引用 Theme.uss (#5 规则 5)。键盘导航通过 UI 框架 (#5 规则 4) 的 FocusController 管理。详见规则 1 的 VisualElement 树结构。

## Acceptance Criteria

- **GIVEN** 交互系统调用 ShowChoicePanel(group with 2 options)，**WHEN** HUD 渲染选择面板，**THEN** 面板出现在锚点物件旁边 (优先右侧)，2 个选项显示为手写体文字 + 朱砂墨点。键盘焦点在第一个选项上。Action Map 切换到 UI。

- **GIVEN** 选择面板展示中，**WHEN** 玩家点击一个选项，**THEN** HUD 调用 ChangeTracker.ApplyChanges(option.ContentChanges) → HideChoicePanel 关闭面板 → Action Map 切回 Gameplay。

- **GIVEN** 选择面板展示中，**WHEN** 玩家按 Escape，**THEN** 面板关闭 → Action Map 切回 Gameplay。不触发任何 ContentChanges。

- **GIVEN** 关联引擎返回 3 个候选 (Strong, Medium, Faint)，**WHEN** HUD 渲染关联路径，**THEN** 3 条墨线从画面中心辐射出去。Strong 候选的墨线最浓、目的地标记最大。Faint 候选的墨线最淡、标记最小。

- **GIVEN** 玩家点击一条关联路径的 .path-candidate，**WHEN** 点击检测触发，**THEN** ChapterManager.TransitionToFragment(candidate.TargetFragmentId) 被调用。碎片过渡开始。

- **GIVEN** 交互系统调用 ShowFragmentText("一封泛黄的信...")，**WHEN** HUD 展示文本浮层，**THEN** 手写体文字出现在画面指定位置，4 秒后自动淡出。文字不阻挡鼠标事件。

- **GIVEN** 玩家从碎片 A 切换到碎片 B (OnFragmentChanged 触发)，**WHEN** HUD 更新章节进度，**THEN** 底部墨点中对应碎片 B 的墨点变为实心朱砂色，碎片 A 的墨点保持实心。

- **GIVEN** 碎片过渡中 (FadeOut/FadeIn)，**WHEN** SceneFader 遮罩覆盖，**THEN** HUD 完全隐藏——选择面板、关联路径、进度指示全部不可见。

## Open Questions

- **关联路径的"气味标签"文字**: MVP 中是否显示 DominantFactor 文字（"标签共鸣"/"叙事线索"）？还是仅靠视觉强度（墨线浓淡）传达？前者更透明但增加视觉噪音；后者更沉浸但玩家可能不理解为什么某些路径更强。建议 MVP 仅视觉强度——playtest 后再决定是否加文字。(Owner: ux-designer, game-designer)

- **章节进度的可视化形式**: 当前方案是水平墨点——但对于 15-25 碎片的章节，一排 25 个点会非常密集。是否改用分段显示（每 5 个一组）或滚动式？建议 MVP 用水平墨点 + 缩小尺寸（每个点 6px）→ playtest 验证可读性。(Owner: ui-programmer)
