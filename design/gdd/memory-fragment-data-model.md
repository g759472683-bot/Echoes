# 记忆碎片数据模型 (Memory Fragment Data Model)

> **Status**: Designed (pending review)
> **Author**: 用户 + systems-designer + game-designer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 1 (选择即重写) + Pillar 3 (关联的网络)

## Overview

记忆碎片数据模型是《回响》中**游戏世界的最小原子单位**的定义。它规定了"一个记忆碎片是什么"——不仅是一张画面，而是画面上每一个可触碰的物件、每一段关联到其他记忆的情感标签、每一个可供玩家选择的分支、以及每一种选择可能引发的画面变化。这个系统不加载资源、不渲染画面、不追踪变化——它只定义 Schema。但它定义的每一条字段，都直接决定了玩家能在记忆画卷中**看到什么、碰到什么、改变什么**。

在技术层面，MemoryFragment 是一个 ScriptableObject——设计时在 Unity Editor 中创作，运行时只读。一个碎片包含：插图引用、情感标签列表、交互物件数组、选项分支树、内容变化条件、以及关联权重。数据管理系统 (#2) 在启动时加载全部碎片元数据，但碎片定义的字段结构归本系统管辖。如果一个碎片是一幅画，这个系统就是画的"构图法则"——它规定画面中必须有留白处放可交互物件、有隐藏处放关联线索、有空白处留给玩家的选择去填补。

## Player Fantasy

玩家不会"看见"数据结构，但数据结构的每一条规则都塑造着他们在记忆画卷中的感受。这个系统将每个记忆碎片定义为一幅**未完成的画**——有些笔触（核心画面、角色身份、不可更改的事件）已经干透，触碰也不会改变；有些笔触（物件的选择状态、情感关联的强度、画面中某些元素的可见性）墨迹未干，玩家的选择会晕开它、覆盖它、或让它永远褪色。

玩家感受到的是一种**迟疑的掌控感**——他们既像闯入旧画室的外人，又像唯一能完成这幅画的画师。每一次面对一个可交互的物件，玩家内心的犹豫不是"这是什么"，而是"我可以动它吗？动了之后，这幅画会变成什么样子？"

## Detailed Design

### Core Rules

**规则 1 — MemoryFragment 是 ScriptableObject，设计时创作，运行时只读**

MemoryFragment 在 Unity Editor 中作为 ScriptableObject 资产创建和编辑。运行时由数据管理系统 (#2) 加载后以只读方式提供给所有下游系统。任何运行时修改（玩家选择导致的变化）不直接写入 SO——而是由记忆变化追踪系统 (#12) 维护叠加层 `Dictionary<(fragmentId, choiceId), ContentOverrides>`。查询时合并基础 SO + 叠加层。

**规则 2 — 字段分为"已干的墨"与"未干的墨"**

每个字段声明其可变性：

| 分类 | 含义 | 示例 |
|------|------|------|
| **已干的墨 (Immutable)** | 运行时永不改变——固定叙事事实 | FragmentId、插图引用、物件碰撞区域 |
| **未干的墨 (Mutable)** | 运行时可被玩家选择覆盖 | 图层可见性、物件状态、标签权重、文本内容 |

不可变字段的修改需要重新构建游戏（设计时修改 SO）。可变字段的运行时值由叠加层管理——SO 中的值是"默认状态"，叠加层中的值是"当前状态"。

**规则 3 — MemoryFragment 包含 8 个数据类别**

**类别 1: 核心标识**（全部已干）

| 字段 | C# 类型 | 必填 | 说明 |
|------|---------|------|------|
| `FragmentId` | `string` | 是 | 唯一标识。格式: `"ch1_frag_03"`。Addressables Key + 叠加层 Key |
| `ChapterId` | `string` | 是 | 所属章节。必须匹配一个 ChapterDefinition |
| `SequenceIndex` | `int` | 是 | 规范排序（0-based），设计师可调整 |
| `FragmentName` | `TableReference` | 是 | 本地化显示名。仅开发工具使用 |

**类别 2: 视觉字段**

| 字段 | C# 类型 | 必填 | 墨 | 说明 |
|------|---------|------|-----|------|
| `BaseIllustration` | `AssetReferenceSprite` | 是 | 干 | 核心画面。始终渲染，永不改变 |
| `VisualLayers` | `List<VisualLayer>` | 否 | 逐层 | 叠加在基础插图之上的图层 |

VisualLayer 子结构:

| 字段 | C# 类型 | 必填 | 墨 | 说明 |
|------|---------|------|-----|------|
| `LayerId` | `string` | 是 | 干 | 图层标识，叠加层用此 ID 切换 |
| `SpriteReference` | `AssetReferenceSprite` | 是 | 干 | 图层精灵引用 |
| `DefaultVisible` | `bool` | 是 | **湿** | 默认可见性。玩家选择可翻转 |
| `SortOrder` | `int` | 是 | 干 | 渲染顺序 |
| `PositionOffset` | `Vector2` | 否 | 干 | 相对于基础插图的位置偏移 |
| `IsMutable` | `bool` | 是 | 干 | 若为 false，叠加层系统拒绝切换此层——"永久属于这幅画" |

**类别 3: 交互物件**

| 字段 | C# 类型 | 必填 | 墨 | 说明 |
|------|---------|------|-----|------|
| `InteractiveObjects` | `List<InteractiveObject>` | 否 | 逐物件 | 画面中可触碰的物件列表 |

InteractiveObject 子结构:

| 字段 | C# 类型 | 必填 | 墨 | 说明 |
|------|---------|------|-----|------|
| `ObjectId` | `string` | 是 | 干 | 物件标识 |
| `InteractionType` | `enum` | 是 | 干 | `Touch` (点击) / `Drag` (拖拽) / `Hover` (悬停) / `Examine` (详细查看) |
| `HitboxCenter` | `Vector2` | 是 | 干 | 碰撞区域中心（画布坐标） |
| `HitboxSize` | `Vector2` | 是 | 干 | 碰撞区域尺寸 |
| `DefaultState` | `enum` | 是 | **湿** | `Active` / `Hidden` / `Disabled`。玩家选择可改变 |
| `DefaultSprite` | `AssetReferenceSprite` | 是 | 干 | 默认外观 |
| `HoverSprite` | `AssetReferenceSprite` | 否 | 干 | 悬停时的外观变化（发光、颤动） |
| `InteractCondition` | `ConditionGroup` | 否 | 干 | 激活条件——默认为 Always（始终可交互） |
| `OnInteract` | `InteractionResult` | 是 | 干 | 交互触发的行为 |

InteractionType 说明:

| 类型 | 占比 | 使用场景 | 玩家感受 |
|------|------|---------|---------|
| `Touch` | ~70% | 默认交互——触碰物件、激活微动画、展开选择 | "我碰到了它" |
| `Drag` | ~20% | 需要"揭开"感的物件——掀信、推窗、拂尘 | "是我让这个东西暴露出来的" |
| `Hover` | ~10% | 不确定是否该碰的物件——悬停时微微颤动/变亮，需点击确认 | "我可以碰它吗？" |

InteractionResult 子结构:

| 字段 | C# 类型 | 必填 | 说明 |
|------|---------|------|------|
| `ResultType` | `enum` | 是 | `PlayAnimation` / `ShowText` / `PresentChoice` / `TransitionToFragment` / `RevealObject` |
| `AnimationId` | `string` | 条件 | ResultType = PlayAnimation 时必填 |
| `TextContent` | `TableReference` | 条件 | ResultType = ShowText 时必填 |
| `ChoiceGroupId` | `string` | 条件 | ResultType = PresentChoice 时必填——引用本碎片的 ChoiceGroup |
| `TargetFragmentId` | `string` | 条件 | ResultType = TransitionToFragment 时必填 |
| `TargetObjectId` | `string` | 条件 | ResultType = RevealObject 时必填——显示隐藏物件 |

**类别 4: 情感标签**

| 字段 | C# 类型 | 必填 | 墨 | 说明 |
|------|---------|------|-----|------|
| `EmotionalTags` | `List<EmotionalTag>` | 否 | 逐标签 | 碎片携带的情感标签列表 |

EmotionalTag 子结构:

| 字段 | C# 类型 | 必填 | 墨 | 说明 |
|------|---------|------|-----|------|
| `TagId` | `string` | 是 | 干 | 标签标识。词汇表由情感标签系统 (#10) 定义 |
| `BaseWeight` | `float` | 是 | **湿** | 默认权重 [0.0, 1.0]。玩家选择可通过 ModifyTagWeight 改变 |
| `IsPrimary` | `bool` | 否 | 干 | 是否为主导情感标签。可选——设计师可标记一个。关联引擎 (#13) 用于情感节奏控制。默认为 false |

**类别 5: 选择分支**

| 字段 | C# 类型 | 必填 | 墨 | 说明 |
|------|---------|------|-----|------|
| `ChoiceGroups` | `List<ChoiceGroup>` | 否 | 干 | 碎片内定义的选择组列表 |

ChoiceGroup 子结构:

| 字段 | C# 类型 | 必填 | 说明 |
|------|---------|------|------|
| `GroupId` | `string` | 是 | 选择组标识 |
| `GroupLabel` | `TableReference` | 是 | 本地化选择提示文本（如"你拿起了这封信——然后呢？"） |
| `MaxSelections` | `int` | 是 | MVP 固定为 1（单选）。Full Vision 可 >1（多选） |
| `Choices` | `List<ChoiceOption>` | 是 | 选项列表（2-3 个） |

ChoiceOption 子结构:

| 字段 | C# 类型 | 必填 | 说明 |
|------|---------|------|------|
| `ChoiceId` | `string` | 是 | 选项标识 |
| `ChoiceText` | `TableReference` | 是 | 本地化选项文本 |
| `ChoiceCondition` | `ConditionGroup` | 否 | 选项的可用条件——默认为 Always |
| `IsRepeatable` | `bool` | 是 | 是否可重复选择。默认 false |
| `ContentChanges` | `List<ContentChange>` | 是 | 选择此选项后触发的内容变化列表 |

**类别 6: 内容变化定义（6 种类型）**

`ContentChange` 是一个多态结构——`ChangeType` 决定使用哪些字段：

| # | ChangeType | 关键字段 | 效果 | 目标 |
|---|-----------|---------|------|------|
| 1 | `ToggleVisualLayer` | `TargetFragmentId`, `LayerId`, `Visible` | 显示/隐藏一个视觉图层 | 可跨碎片（TargetFragmentId 可为其他碎片） |
| 2 | `SetObjectState` | `TargetFragmentId`, `ObjectId`, `NewState` | 改变物件交互状态 (Active/Hidden/Disabled) | 可跨碎片 |
| 3 | `SetTextContent` | `TargetFragmentId`, `TextFieldId`, `NewText` (TableReference) | 替换显示的文本内容 | 可跨碎片 |
| 4 | `ModifyTagWeight` | `TargetFragmentId`, `TagId`, `Delta`, `Operation` (Add/Multiply/Set) | 调整情感标签权重 | 可跨碎片 |
| 5 | `UnlockAssociation` | `TargetFragmentId`, `AssociationTargetId` | 揭示两个碎片之间的隐藏关联 | 始终是双向的（A→B 和 B→A） |
| 6 | `SetFlag` | `FlagId`, `Value` | 设置一个全局叙事标记 | 跨章节——FlagId 在整个游戏范围唯一 |

**规则 4 — 跨碎片变化是核心能力，但有约束**

一个碎片上的选择可以改变同一章内任意其他碎片的内容（Category 6 中的 TargetFragmentId）。这是 Pillar 3（关联的网络）的关键实现——"涟漪效应"。约束：
- 跨碎片变化的目标必须是**同一章节内**的碎片（ChapterId 相同）。跨章变化通过 `SetFlag` + 条件系统间接触发——碎片 A 设置 Flag、碎片 B 的物件/选项通过 ConditionGroup 检查 Flag
- 叠加层的 Key 为 `(TargetFragmentId, ChoiceId)`——意味着如果碎片 A 和碎片 B 的不同选择都修改了碎片 C 的同一图层，最后一次保存的选择生效
- 内容创作者在编辑器中定义跨碎片变化时必须能看到目标碎片的字段（编辑器工具在 Inspector 中显示目标碎片的图层和物件列表）

**规则 5 — 统一条件系统**

所有需要条件判断的字段（物件的 `InteractCondition`、选项的 `ChoiceCondition`、结局的 `TriggerCondition`）使用同一套条件系统：

`ConditionGroup` 包含一个 `Combinator` 和一个条件列表：

| Combinator | 含义 |
|-----------|------|
| `All` | 所有子条件满足 → 通过 |
| `Any` | 任一子条件满足 → 通过 |
| `Not` | 子条件不满足 → 通过 |

6 种叶子条件（`Condition`）:

| 条件类型 | 参数 | 含义 |
|---------|------|------|
| `Always` | 无 | 始终满足 |
| `ChoiceMade` | `fragmentId`, `choiceId` | 玩家在指定碎片中选择了指定选项 |
| `FlagSet` | `flagId`, `value` | 指定全局标记等于给定值 |
| `ObjectStateIs` | `fragmentId`, `objectId`, `state` | 指定物件的当前状态等于给定值 |
| `VisitedFragment` | `fragmentId` | 玩家曾访问过指定碎片 |
| `ChapterCompleted` | `chapterId` | 指定章节已完成 |

ConditionGroup 支持嵌套——一个 ConditionGroup 的子条件可以是另一个 ConditionGroup（深度 ≤ 3 层，防止无限嵌套）。

**规则 6 — 关联权重定义在碎片中**

| 字段 | C# 类型 | 必填 | 墨 | 说明 |
|------|---------|------|-----|------|
| `ExplicitAssociations` | `List<FragmentAssociation>` | 否 | 逐关联 | 显式定义的碎片关联 |

FragmentAssociation 子结构:

| 字段 | C# 类型 | 必填 | 墨 | 说明 |
|------|---------|------|-----|------|
| `TargetFragmentId` | `string` | 是 | 干 | 关联目标碎片 |
| `AssociationType` | `enum` | 是 | 干 | `Thematic` / `Narrative` / `Visual` / `Emotional` / `Causal` |
| `BaseWeight` | `float` | 是 | **湿** | 默认权重 [0.0, 1.0]——关联引擎的基础输入 |
| `IsBidirectional` | `bool` | 是 | 干 | 是否双向关联。若 true，目标碎片也隐式具有回向关联 |
| `VisibilityCondition` | `ConditionGroup` | 否 | 干 | 关联可见的条件——未满足时关联隐藏（对玩家不可见） |
| `Description` | `string` | 否 | 干 | 仅供开发工具的说明——不运行时使用 |

**规则 7 — 结局触发条件定义在碎片中**

| 字段 | C# 类型 | 必填 | 墨 | 说明 |
|------|---------|------|-----|------|
| `EndingTriggers` | `List<EndingTrigger>` | 否 | 干 | 碎片为结局系统提供的触发条件 |

EndingTrigger 子结构:

| 字段 | C# 类型 | 必填 | 说明 |
|------|---------|------|------|
| `EndingId` | `string` | 是 | 结局标识 |
| `TriggerCondition` | `ConditionGroup` | 是 | 触发条件——如 `ChoiceMade("ch1_frag_07", "keep_letter") AND ChapterCompleted("ch3")` |
| `ContributionWeight` | `float` | 是 | 贡献权重 [0.0, 1.0]——多结局判定时累加 |
| `IsEssential` | `bool` | 是 | 是否为必要触发条件。若 true，缺此条件结局不触发 |

结局判定逻辑归多结局系统 (#14)——碎片只定义条件。

**规则 8 — 碎片中的物件数量与选择复杂度约束**

| 参数 | MVP 约束 | Full Vision 约束 |
|------|---------|-----------------|
| 每碎片物件数 | 2-5 | 2-7 |
| 每碎片 ChoiceGroup 数 | 0-2 | 0-3 |
| 每 ChoiceGroup 的选项数 | 2-3 | 2-4 |
| ChoiceGroup.MaxSelections | 固定为 1 | 1-3 |
| 必触物件（有明显发光） | 1-2 个/碎片 | 1-2 个/碎片 |
| 可选物件（微弱发光） | 1-3 个/碎片 | 1-5 个/碎片 |
| 氛围物件（无分支，纯触发） | 0-2 个/碎片 | 0-3 个/碎片 |

设计师应在 Inspector 中看到当前碎片的物件数和选项数的实时计数——若超过约束上限显示黄色警告。

### States and Transitions

本系统在运行时无状态机——MemoryFragment 不持有运行时状态。它是纯数据定义。

内容创作者的编辑工作流状态（碎片处于"草稿/待审/已批准"）由外部工具管理，不编码到 SO 中。

### Interactions with Other Systems

| 方向 | 系统 | 数据流向 | 接口 |
|------|------|---------|------|
| 上游 | **数据管理 (#2)** | 加载 MemoryFragment SO → 提供给下游 | `GetFragmentAsync(chapterKey, fragmentId)` → `Task<MemoryFragment>` |
| 下游 | **情感标签系统 (#10)** | 读取 `EmotionalTags` 列表 | `fragment.EmotionalTags` |
| 下游 | **记忆画卷交互系统 (#11)** | 读取 `InteractiveObjects` + `VisualLayers` | `fragment.InteractiveObjects`, `fragment.BaseIllustration`, `fragment.VisualLayers` |
| 下游 | **记忆变化追踪 (#12)** | 读取 `ContentChange` 定义 → 填充叠加层 | `choice.ContentChanges` |
| 下游 | **网状关联引擎 (#13)** | 读取 `EmotionalTags` + `ExplicitAssociations` | `fragment.EmotionalTags`, `fragment.ExplicitAssociations` |
| 下游 | **多结局系统 (#14)** | 读取 `EndingTriggers` | `fragment.EndingTriggers` |
| 下游 | **存档系统 (#7)** | ChangeOverlay 的 Key 包含 `fragmentId` | 叠加层 Key = `(fragmentId, choiceId)` |

## Formulas

记忆碎片数据模型不包含运行时计算公式。它定义数据结构、字段类型和约束——所有值的计算和使用归下游系统负责。

**本系统定义的数值类型与范围（供下游公式引用）**:

| 字段 | 类型 | 范围 | 下游使用者 |
|------|------|------|-----------|
| `EmotionalTag.BaseWeight` | float | [0.0, 1.0] | 网状关联引擎 (#13) — 关联权重计算的基础输入 |
| `FragmentAssociation.BaseWeight` | float | [0.0, 1.0] | 网状关联引擎 (#13) — 显式关联的强度 |
| `EndingTrigger.ContributionWeight` | float | [0.0, 1.0] | 多结局系统 (#14) — 结局判定累加 |
| `ModifyTagWeight.Delta` | float | [-1.0, 1.0] | 记忆变化追踪 (#12) — 标签权重变化量 |

**Schema 完整性约束（非公式，但影响性能与范围）**:

- 单碎片 SO 序列化大小: 5-10KB（设计目标）
- 全量元数据内存占用: 60-100 碎片 × 8KB ≈ 480KB-800KB（数据管理 #2 已验证 < 1MB）
- ConditionGroup 最大嵌套深度: 3 层
- ContentChange 跨碎片目标: 仅限同章节（ChapterId 匹配）

## Edge Cases

- **如果碎片定义了 0 个 InteractiveObjects**：碎片合法——这是一个"纯观看"碎片，玩家只能看画面，无法交互。场景管理器正常加载，交互系统检测到空列表后不渲染任何交互提示。碎片仍然可以有情感标签和关联权重，在关联网络中作为"途经节点"。

- **如果 ChoiceGroup 的 Choices 只有 1 个选项**：碎片合法——这是一个"非选择"式的揭示。玩家点击物件 → 唯一选项自动展示内容变化。UI 不弹出选择面板，直接展示结果。这在叙事上用于"无法回避的事实"——玩家只能接受。

- **如果 ChoiceGroup 定义了选项但 MaxSelections > Choices.Count**：编辑器验证报错——"最大选择数不能超过可用选项数"。运行时防御：若验证失败通过构建层拦截，取 `Min(MaxSelections, Choices.Count)`。

- **如果碎片不在任何 ExplicitAssociations 中被引用（孤立节点）**：碎片合法——它仍然可以通过情感标签系统被关联引擎自动关联。孤立碎片只能通过章节的 SequenceIndex 到达——在关联网络中没有边连接。

- **如果 ContentChange.TargetFragmentId 指向不存在的碎片**：编辑器验证报错——"目标碎片不存在"。运行时防御：叠加层尝试应用到一个不存在的 TargetFragmentId → 忽略该变化，记录 Warning 日志。不阻塞游戏。

- **如果 ModifyTagWeight 的 Delta 将 BaseWeight 推到 [0.0, 1.0] 范围外**：运行时夹紧到 [0.0, 1.0]。`Operation = Set` 时直接夹紧；`Operation = Add` 时加后夹紧；`Operation = Multiply` 时乘后夹紧。

- **如果 EndingTrigger 的 ContributionWeight 累加超过 1.0**：多结局系统 (#14) 负责截断——数据模型不定义截断逻辑。但 ContributionWeight 字段本身定义上限为 1.0。

- **如果 ConditionGroup 形成循环嵌套（A 引用 B，B 引用 A）**：编辑器验证检测循环引用——评估每个 ConditionGroup 的依赖图，若检测到环则报错。运行时不验证（信任构建时检查）。

- **如果 FragmentId 在章节内不唯一**：编辑器验证报错——"碎片 ID 必须在同一章节内唯一"。构建层二次交叉验证。

- **如果 VisualLayer.IsMutable = false，但某个 ContentChange 尝试修改该层**：运行时叠加层拒绝修改——记录 Warning 日志"尝试修改不可变图层 [LayerId]"，不抛出异常。该变化被静默跳过。

- **如果玩家 30 秒内没有触碰任何必触物件**：这不是数据模型层面的规则——交给交互系统 (#11) + HUD (#17) 处理。碎片 Schema 只定义物件列表，不定义"超时"行为。

- **如果碎片同时出现在两个章节的 ChapterDefinition 中**：每个碎片 ChapterId 字段只能有一个值——它属于且只属于一个章节。如果两个 ChapterDefinition 引用同一个 FragmentId，编辑器交叉验证警告——"碎片 [ID] 的 ChapterId 与引用它的 ChapterDefinition 不匹配"。

- **如果 TableReference 指向不存在的本地化 Key**：编辑器验证警告——"引用的本地化 Key [key] 不存在于任何已加载的字符串表中"。运行时显示 Key 原文（如 `[missing: frag_03_choice_keep]`）作为降级显示。

## Dependencies

**硬依赖（系统无法运行的前提）**:

| 系统 | 依赖性质 | 提供的接口 |
|------|----------|-----------|
| **数据管理 (#2)** | 硬依赖 | `GetFragmentAsync(chapterKey, fragmentId)` → `Task<MemoryFragment>`。本系统的 SO 由数据管理加载并提供给下游 |

**下游系统（依赖本系统提供的 Schema）**:

| 系统 | 依赖性质 | 读取的字段 |
|------|----------|-----------|
| **情感标签系统 (#10)** | 硬依赖 | `EmotionalTags` — 标签词汇表由 #10 定义，但每个碎片分配哪些标签由本系统 Schema 定义 |
| **记忆画卷交互系统 (#11)** | 硬依赖 | `InteractiveObjects`, `BaseIllustration`, `VisualLayers` — 渲染交互物件和画面的全部输入 |
| **记忆变化追踪 (#12)** | 硬依赖 | `ContentChange` 定义 — 需要知道"每个选择触发什么变化"才能填充叠加层 |
| **网状关联引擎 (#13)** | 硬依赖 | `EmotionalTags`, `ExplicitAssociations` — 关联计算的原始数据 |
| **多结局系统 (#14)** | 硬依赖 | `EndingTriggers` — 结局判定的原子条件 |
| **存档系统 (#7)** | 软依赖 | 叠加层 Key 格式 `(fragmentId, choiceId)` — 存档系统不解析 Schema 内容，只序列化 Key |
| **本地化系统 (#4)** | 软依赖 | `TableReference` 类型字段 — 碎片名、选择文本、TextContent 等。本地化系统提供对应的字符串查找 |

**双向一致性检查**:
- 数据管理 GDD (#2) 的 Interactions 表中已列出"下游: 记忆碎片数据模型 (#8)"——方向匹配 ✅
- 存档系统 GDD (#7) 的 `SaveData.ChangeOverlay` 使用 `Dictionary<string, string>`，Key 为 `"fragmentId:choiceId"`——与本系统的叠加层 Key 格式兼容 ✅

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 每碎片物件数上限 (MVP) | 5 | 3–7 | 超过上限 Inspector 显示黄色警告。MVP 固定为 5 |
| 每碎片 ChoiceGroup 数上限 | 2 | 1–3 | 每碎片最大选择组数。过多选择组导致决策疲劳 |
| 每 ChoiceGroup 选项数上限 (MVP) | 3 | 2–4 | 影响选择面板 UI 布局 |
| ConditionGroup 最大嵌套深度 | 3 | 2–5 | 深层嵌套增加计算开销和创作者编错概率 |
| 情感标签 BaseWeight 默认值 | 0.5 | 0.1–1.0 | 新标签的默认权重。值越高，新碎片在网络中的初始引力越大 |
| 关联 BaseWeight 默认值 | 0.5 | 0.1–1.0 | 新显式关联的默认强度 |
| ContentChange 跨碎片目标范围 | 同章节 | 同章节 / 全局 | 当前固定为同章节。若改为全局，状态空间增长显著 |
| VisualLayer 最大层数 (每碎片) | 10 | 5–20 | 限制图层叠加数量，防止渲染性能退化 |
| FragmentId 格式 | `"ch{id}_frag_{seq}"` | — | 不建议修改——影响 Addressables Key 和存档兼容性 |

## Visual/Audio Requirements

本系统不产生视觉或音频输出。MemoryFragment 是纯数据定义——画面渲染、物件发光、交互动画等视觉呈现由记忆画卷交互系统 (#11) 和交互反馈系统 (#18) 负责。

## UI Requirements

本系统不包含玩家可见的 UI。编辑器工具 UI（MemoryFragment Inspector 验证面板、批量验证窗口）属于开发工具，不在 GDD 范围内。

## Acceptance Criteria

- **GIVEN** Unity Editor 中创建了一个新的 MemoryFragment SO，**WHEN** 填写必填字段（FragmentId, ChapterId, SequenceIndex, FragmentName, BaseIllustration）并保存，**THEN** SO 可通过数据管理系统的 `GetFragmentAsync` 加载，返回完整 MemoryFragment 对象，所有必填字段非空。

- **GIVEN** 一个包含 2 个 InteractiveObject 的 MemoryFragment，**WHEN** 交互系统 (#11) 读取 `InteractiveObjects` 列表，**THEN** 两个物件的 ObjectId、HitboxCenter、HitboxSize、DefaultState、OnInteract 均按 SO 中定义的值返回。物件的碰撞区域坐标与 SO 中的值一致。

- **GIVEN** 一个 ChoiceGroup 定义了 2 个 ChoiceOption，每个 ChoiceOption 各含 1 个 ToggleVisualLayer 类型的 ContentChange，**WHEN** 变化追踪系统 (#12) 读取 ChoiceOption.ContentChanges，**THEN** 每个 ContentChange 的 ChangeType、TargetFragmentId、LayerId、Visible 值与 SO 定义一致。

- **GIVEN** 一个 ContentChange 的 TargetFragmentId 与当前碎片不同（跨碎片变化），**WHEN** 编辑器批量验证运行，**THEN** 若目标碎片在同一章节内且存在——验证通过。若目标碎片不存在或属于其他章节——验证报错。

- **GIVEN** 一个 ConditionGroup 嵌套深度为 3 层，**WHEN** 编辑器验证运行，**THEN** 验证通过。若嵌套深度为 4 层——验证报错"条件嵌套超过最大深度 3"。

- **GIVEN** 一个 VisualLayer 的 IsMutable = false，**WHEN** 某个 ContentChange 尝试 ToggleVisualLayer 修改该图层，**THEN** 运行时叠加层拒绝修改，记录 Warning 日志，不抛出异常，游戏继续运行。

- **GIVEN** 两个 MemoryFragment 在同一章节内使用相同的 FragmentId，**WHEN** 编辑器批量验证运行，**THEN** 验证报错"碎片 ID [fragId] 在章节 [chapterId] 中重复"。

- **GIVEN** 一个 MemoryFragment 定义了 3 个 EmotionalTag，其中一个 IsPrimary = true，**WHEN** 情感标签系统 (#10) 查询该碎片的标签，**THEN** 返回 3 个标签，其中 IsPrimary 为 true 的标签被正确标记。

- **GIVEN** 一个 MemoryFragment 定义了 2 个 ExplicitAssociations，其中一个 IsBidirectional = true，**WHEN** 关联引擎 (#13) 读取关联列表，**THEN** 返回 2 个关联，双向关联的目标碎片可以隐式回向关联到源碎片。

- **GIVEN** 一个 EndingTrigger 的 TriggerCondition 为 `ChoiceMade("ch1_frag_07", "keep_letter") AND ChapterCompleted("ch1")`，**WHEN** 多结局系统 (#14) 评估该触发条件，**THEN** 仅当玩家在 frag_07 中选了 keep_letter 且第一章已完成时条件为 true。

- **GIVEN** 一个 MemoryFragment 的 TableReference 字段指向不存在的本地化 Key，**WHEN** 本地化系统 (#4) 查找该 Key，**THEN** 运行时降级显示 Key 原文（如 `[missing: key_name]`），编辑器验证预先警告该 Key 不存在。

- **GIVEN** 修改了 MemoryFragment SO 的一个不可变字段（如 FragmentId），**WHEN** 已存在使用旧 ID 的存档文件，**THEN** 存档系统加载时发现 fragmentId 不匹配——叠加层中旧 ID 对应的 ChangeOverlay 成为孤儿条目，被标记为无效但不阻塞加载。注意：修改 FragmentId 是破坏性变更——生产阶段不应执行。

## Open Questions

- **ConditionGroup 的多态序列化**：Unity ScriptableObject 不支持原生多态序列化。`ConditionGroup` 包含 6 种叶子 Condition 类型 + 嵌套 ConditionGroup——需要使用 Unity 的 `[SerializeReference]` 属性或多态 SerializedObject 包装。`[SerializeReference]` 在 Unity 6.3 中的稳定性和 Inspector 支持需要验证。（Owner: gameplay-programmer，在架构阶段用 ADR 决定序列化方案）

- **情感标签词汇表的归属**：`EmotionalTag.TagId` 是字符串引用——但标签词汇表本身（哪些标签存在、标签之间的层级关系）由情感标签系统 (#10) 定义。本系统只使用 TagId。如果 #10 的词汇表发生变更（标签重命名），碎片 SO 中的 TagId 引用会断裂。需要协调两个系统的编辑器验证——当标签被重命名时，自动更新所有引用碎片的 TagId。（Owner: 情感标签系统 #10 设计者，设计 #10 时处理）

- **FragmentId 命名规范的自动化**：`"ch{id}_frag_{seq}"` 格式是约定而非强制约束。如果内容创作者创建了不符合规范的 FragmentId（如 `"my_custom_fragment"`），系统仍可运行但会丧失可读性。是否应在创建新 Fragment SO 时自动生成 ID，禁止手动编辑？还是保留手动编辑的灵活性？（Owner: 工具程序员，在编辑器工具 ADR 中决定）

- **TableReference 的运行时类型**：TableReference 在当前 GDD 中被引用为类型，但其具体实现（直接存字符串 Key、存整数 ID、或存 `LocalizedString` 包装）未在本系统中定义。这属于本地化系统 (#4) 的接口——需要确认 TableReference 在 Unity 中的具体形式。（Owner: 本地化系统 #4，在 #4 的 Schema 中确认）

- **跨章节 ContentChange 的延迟绑定**：当前跨碎片变化限制在同一章节内（ChapterId 匹配）。如果未来需要真正的跨章变化（第一章的选择直接改变第三章的画面），可以通过 SetFlag + ConditionGroup 间接触发——但这是两步操作，不如直接 TargetFragmentId 跨章直观。是否在 Full Vision 中放宽此约束？（Owner: game-designer，在 Full Vision 规划时评估）

