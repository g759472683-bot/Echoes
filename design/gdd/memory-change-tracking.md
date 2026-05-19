# 记忆变化追踪 (Memory Change Tracking)

> **Status**: Designed (pending review)
> **Author**: 用户 + game-designer + gameplay-programmer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 1 (选择即重写) — 直接支撑——追踪是"选择改变记忆"的记录引擎

## Overview

记忆变化追踪是《回响》中"选择即重写"的记录之书。每当玩家在记忆画卷中做出一个选择——拿起那封信、推开那扇窗、保留了那段回忆——变化追踪系统将这个选择的后果刻入游戏世界。它不决定"变化是什么"（那是数据模型 #8 的职责），不触发"变化怎么展示"（那是交互系统 #11 和微动画 #9 的职责）——它只负责一件事：**记住玩家做过什么，并让世界如实地反映这些选择**。

在技术层面，它是一个叠加层引擎：每个 MemoryFragment 在加载时呈现其原始状态（SO 中的默认值），但变化追踪系统维护一个运行时叠加层 `Dictionary<(fragmentId, choiceId), ContentOverrides>`——记录哪些图层被翻转了、哪些物件的状态被改变了、哪些情感标签的权重被调整了。任何系统查询一个碎片的状态时，变化追踪系统合并"原始墨迹 + 玩家留下的指纹"，返回当前的真实状态。当玩家说"我改变了这段记忆"，变化追踪系统是那个真正记得**改变了什么、在哪里、变成了什么样**的系统。

## Player Fantasy

变化追踪系统没有"界面"，但它制造了《回响》中最安静也最有力的瞬间——**重访**。玩家在第一章做了一个选择：保留了一封信。两章之后，某个碎片中的一个角色出现了——不是因为你"解锁"了她，而是因为你当初的选择从关联网络中渗了过来。变化追踪系统是这一切背后的"因为"。

它的情感核心是一种**温柔的不可逆**。每一个选择都是在你翻阅的这本记忆之书上按下一个指印——手上沾了墨，碰过的页面就永远留着你的痕迹。你不能"撤销"一个选择回到"原来的样子"。被覆盖的图层、被改变了状态的物件、被调整了权重的标签——它们就像旧信纸上被手指摩挲过的墨迹，再也没有最初的锐利，但有了温度。这不让人后悔——让人感觉到**你的存在是有重量的**。

对于 Pillar 2（不完美才是力量），变化追踪系统是最沉默的证人——它不告诉你"这条路线比那条更好"，它只是忠实地记录每一次选择的后果，让某些画面永远改变了，让另一些关联永远无法被触发。完美的画面上不会有指纹——但这个游戏最美的就是那些指印。

## Detailed Design

### Core Rules

**规则 1 — 叠加层数据结构**

```
ChangeTracker (MonoBehaviour, Game 场景持久)
│
├── _overlay: Dictionary<(string targetFragmentId, string choiceId), ContentOverrides>
│   │   Key: (被修改的碎片ID, 触发变更的选择ID)
│   │   Value: ContentOverrides — 该选择产生的所有变更的合并结构体
│   │
├── _changeLog: List<ChangeLogEntry>
│   │   仅追加。每项包含:
│   │   - Timestamp (float, 游戏运行时间)
│   │   - TargetFragmentId
│   │   - ChoiceId (触发变更的选择)
│   │   - Changes: ContentChange[] (原始变更列表的副本)
│   │   - OrderIndex (int, 全局递增序号)
│   │
├── _flags: Dictionary<string, bool>
│   │   全局叙事标记。由 SetFlag 写入，由条件系统查询
│   │
└── OverlayVersion: int (每次 ApplyChanges 递增)
```

- `ContentOverrides` 是一个 `[Serializable]` 结构体，包含按类型分组的变更字段:
  - `ToggledLayers: List<(string layerId, bool visible)>`
  - `ObjectStates: List<(string objectId, ObjectState newState)>`
  - `TextOverrides: List<(string textFieldId, string newTextKey)>`
  - `TagWeightMods: List<(string tagId, float delta, ModOp operation)>`
  - `UnlockedAssociations: List<string>` (AssociationTargetId 列表)
  - `SetFlags: List<(string flagId, bool value)>`

**规则 2 — ApplyChanges 算法**

```
ApplyChanges(string targetFragmentId, string choiceId, ContentChange[] changes):
  1. 验证 targetFragmentId 存在于碎片注册表中 → 否则 LogWarning + 跳过
  2. 将 changes 转换为 ContentOverrides 结构体:
     ├── ToggleVisualLayer → ContentOverrides.ToggledLayers.Add(layerId, visible)
     ├── SetObjectState    → ContentOverrides.ObjectStates.Add(objectId, newState)
     ├── SetTextContent    → ContentOverrides.TextOverrides.Add(fieldId, newTextKey)
     ├── ModifyTagWeight   → ContentOverrides.TagWeightMods.Add(tagId, delta, operation)
     ├── UnlockAssociation → ContentOverrides.UnlockedAssociations.Add(targetId)
     └── SetFlag           → 直接写入 _flags[flagId] = value
  3. _overlay[(targetFragmentId, choiceId)] = contentOverrides
     (若 Key 已存在 → 覆盖。同一选择重复触发 = 最后一次生效)
  4. _changeLog.Add(new ChangeLogEntry { ... })  // 仅追加，不修改已有条目
  5. OverlayVersion++
  6. 触发事件 OnOverlayChanged(targetFragmentId)
     → 微动画系统 (#9) 订阅此事件以触发视觉变化动画
     → 场景管理系统 (#6) 订阅以刷新当前画面
```

**规则 3 — 状态合并算法 (GetCurrentState)**

```
GetCurrentState(string fragmentId) → ResolvedFragmentState:
  1. 从数据管理 (#2) 获取基础 SO: MemoryFragment base = DataManager.GetFragment(fragmentId)
  2. 若 base == null → 返回 null (碎片不存在)
  3. 查找所有 targetFragmentId == fragmentId 的叠加层条目
  4. 创建 ResolvedFragmentState 副本 (从 base 初始化):
     ├── VisualLayers: 从 base.VisualLayers 复制
     ├── InteractiveObjects: 从 base.InteractiveObjects 复制
     ├── EmotionalTags: 从 base.EmotionalTags 复制
     ├── ExplicitAssociations: 从 base.ExplicitAssociations 复制
     └── TextContent: 从 base 复制所有文本字段
  5. 按 OrderIndex 升序依次应用每个叠加层条目:
     ├── ToggledLayers: ResolvedState.VisualLayers[layerId].Visible = value
     │   └── 若 layer.IsMutable == false → 跳过 + LogWarning
     ├── ObjectStates: ResolvedState.InteractiveObjects[objectId].State = newState
     ├── TextOverrides: ResolvedState.TextFields[fieldId] = newTextKey
     ├── TagWeightMods:
     │   ├── Operation = Add: weight += delta → Clamp [0.0, 1.0]
     │   ├── Operation = Multiply: weight *= delta → Clamp [0.0, 1.0]
     │   └── Operation = Set: weight = delta → Clamp [0.0, 1.0]
     └── UnlockedAssociations: ResolvedState.Associations 中添加目标关联
         └── 若已存在 → 跳过 (幂等)
  6. 返回 ResolvedFragmentState

合并策略: 后发生的变更 (更高 OrderIndex) 覆盖先发生的变更的同名字段。
```

**规则 4 — 六种 ContentChange 的叠加逻辑**

| # | ChangeType | 叠加行为 | 冲突解决 |
|---|-----------|---------|---------|
| 1 | ToggleVisualLayer | 翻转目标图层的 Visible 为指定值 | 后发生覆盖先发生。若图层 IsMutable=false → 拒绝 + LogWarning |
| 2 | SetObjectState | 将目标物件状态设为 Active/Hidden/Disabled | 后发生覆盖先发生 |
| 3 | SetTextContent | 替换文本字段内容 | 后发生覆盖先发生 |
| 4 | ModifyTagWeight | 按 Operation 调整权重 (Add/Multiply/Set) → Clamp [0.0, 1.0] | 顺序叠加——第二个 ModifyTagWeight 在第一个的结果上操作 |
| 5 | UnlockAssociation | 揭示隐藏关联 (双向) | 幂等——已解锁的关联再次解锁无效果 |
| 6 | SetFlag | 直接写入全局标记字典 | 后发生覆盖先发生 |

**规则 5 — Flag 系统**

- Flag 是全局布尔标记，Key 为字符串 (如 `"ch1_letter_kept"`, `"ch2_window_opened"`)
- 命名约定: `"ch{chapterId}_{event}"` — 建议但非强制
- SetFlag 不经过叠加层——直接写入 `_flags` 字典，不关联到特定碎片
- Flag 是跨章节状态追踪 (#16) 的数据源——#16 持有 Flag 字典的所有权，本系统是写入方
- 条件系统 (ConditionGroup) 中的 `FlagSet` 条件查询: `_flags.TryGetValue(flagId, out bool value)`

**规则 6 — 变更历史日志**

`_changeLog` 仅追加——不修改、不删除已有条目。用途:
- **调试**: 开发阶段可在控制台输出完整选择历史
- **画廊/回忆录 (#24, Full Vision)**: 可重构"玩家经历了什么"的时间线
- **存档系统 (#7)**: 不直接使用——存档系统序列化 `_overlay` 和 `_flags`，不序列化日志

ChangeLog 在加载存档时**不完全重建**——只记录"此会话中的新选择"。跨会话的完整历史由画廊系统 (#24) 负责。

**规则 7 — 变更验证**

`ApplyChanges` 调用时执行以下验证:
1. `targetFragmentId` 在碎片注册表中存在 → 否则 LogWarning + 跳过
2. `LayerId` 在目标碎片的 VisualLayers 中存在 → 否则 LogWarning + 跳过该条
3. `ObjectId` 在目标碎片的 InteractiveObjects 中存在 → 否则 LogWarning + 跳过该条
4. `TagId` 在 EmotionalTagCatalog 中存在 → 否则 LogWarning + 跳过该条
5. `AssociationTargetId` 在碎片注册表中存在 → 否则 LogWarning + 跳过该条

验证失败不阻塞 ApplyChanges——仅跳过无效条目，有效条目仍然应用。

**规则 8 — 跨碎片变更的即时生效**

当碎片 A 的选择修改了碎片 B 的内容:
- ApplyChanges 立即更新叠加层
- 若碎片 B 是**当前正在显示的碎片** → `OnOverlayChanged(fragmentId_B)` 触发 → 场景管理器刷新画面 (在当前帧内完成)
- 若碎片 B **尚未被访问** → 变更静默记录在叠加层中。下次访问碎片 B 时 GetCurrentState 返回已变更的状态
- 跨章变更通过 SetFlag + ConditionGroup 间接触发——条件在 GetCurrentState 时评估

**规则 9 — 条件系统的运行时评估**

`ConditionGroup` 在以下时机被评估:
- 交互系统 (#11) 查询物件是否可交互 → `EvaluateCondition(obj.InteractCondition)`
- 交互系统 (#11) 查询选项是否可用 → `EvaluateCondition(choice.ChoiceCondition)`
- 多结局系统 (#14) 查询结局触发条件 → `EvaluateCondition(ending.TriggerCondition)`

评估逻辑:
```
EvaluateCondition(ConditionGroup group) → bool:
  ├── Combinator = All: 所有子条件 AND
  ├── Combinator = Any: 任一子条件 OR
  └── Combinator = Not: NOT 子条件
  
  叶子条件评估:
  ├── Always → true
  ├── ChoiceMade(fragId, choiceId) → _overlay.ContainsKey((fragId, choiceId))
  ├── FlagSet(flagId, value)       → _flags.TryGetValue(flagId) == value
  ├── ObjectStateIs(fragId, objId, state) → GetCurrentState(fragId).Objects[objId].State == state
  ├── VisitedFragment(fragId)      → _visitedFragments.Contains(fragId)
  └── ChapterCompleted(chapterId)  → _completedChapters.Contains(chapterId)
```

**规则 10 — 已访问碎片与章节完成追踪**

本系统额外维护:
- `_visitedFragments: HashSet<string>` — 玩家访问过的所有碎片 ID
- `_completedChapters: HashSet<string>` — 已完成的章节 ID

这些集合在场景管理 (#6) 触发事件时更新:
- 进入碎片 → `_visitedFragments.Add(fragmentId)`
- 章节完成 → `_completedChapters.Add(chapterId)`

这些集合支持条件系统的 `VisitedFragment` 和 `ChapterCompleted` 条件。

**规则 11 — 可重复选择的叠加行为**

当 `ChoiceOption.IsRepeatable = true`，玩家可多次选择同一选项:
- 每次选择都触发 `ApplyChanges`，使用相同的 `(targetFragmentId, choiceId)` Key
- 叠加层中该 Key 的 ContentOverrides 被**覆盖** (最后一次生效)
- 但 `_changeLog` 中保留**每次**选择的独立日志条目

若设计师需要累积效果 (如"每次选择增加某标签权重 +0.1")，ModifyTagWeight 的 Delta 设计应使单次选择的 ContentChanges 本身已包含目标效果——叠加层的"覆盖"语义不变，但变更日志记录的每次选择各自独立。

### States and Transitions

| 状态 | 描述 | 触发 |
|------|------|------|
| **Uninitialized** | ChangeTracker 尚未就绪。所有查询返回 null | Game 场景加载前 |
| **Ready** | 叠加层和 Flag 字典已初始化。接受查询和 ApplyChanges | Game 场景加载完成、存档恢复完成 |
| **Applying** | 正在执行 ApplyChanges。极短暂 (<1ms)。此期间查询被排队 | ApplyChanges 调用 |
| **Error** | 碎片注册表加载失败或数据损坏 | 数据管理加载失败 |

Ready → Applying → Ready (同步，在同一帧内完成)
Error 不可恢复——显示错误提示并返回主菜单。

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **交互系统 (#11)** | `ApplyChanges(fragmentId, choiceId, ContentChange[])` | 玩家选择后调用——触发内容变化 |
| 上游 | **场景管理 (#6)** | `OnFragmentTransitioned(chapterKey, fragmentId)` → 更新 `_visitedFragments` | 碎片切换时记录访问 |
| 上游 | **数据管理 (#2)** | `GetFragment(fragmentId)` → `MemoryFragment` | 状态合并时获取基础 SO |
| 上游 | **存档系统 (#7)** | `OnSaveRequested` → `(overlay, flags, visitedFragments, completedChapters)` / `OnLoadRestore` | 存档和读档 |
| 上游 | **情感标签系统 (#10)** | `GetTagCatalog()` → `EmotionalTagCatalog` | 验证 ModifyTagWeight 的 TagId 有效性 |
| 下游 | **微动画 (#9)** | `OnOverlayChanged(fragmentId)` 事件 | 视觉变化触发动画播放 |
| 下游 | **多结局 (#14)** | `EvaluateCondition(conditionGroup)` / `GetFlag(flagId)` | 结局条件判定 |
| 下游 | **跨章节状态追踪 (#16)** | `_flags`, `_visitedFragments`, `_completedChapters` | 跨章节持久化状态 |
| 下游 | **HUD (#17)** | `OnOverlayChanged` → 画面内容刷新提示 | 选择后 HUD 可能更新文本或指示器 |

## Formulas

**ModifyTagWeight 公式**

`ModifyTagWeight` 定义了三种操作来调整情感标签的运行时权重:

| Operation | 公式 | 说明 |
|-----------|------|------|
| `Add` | `newWeight = Clamp(baseWeight + delta, 0.0, 1.0)` | 线性增减。delta 可为负值 |
| `Multiply` | `newWeight = Clamp(baseWeight × delta, 0.0, 1.0)` | 比例缩放。delta=1.0 = 不变, delta=0.5 = 减半 |
| `Set` | `newWeight = Clamp(delta, 0.0, 1.0)` | 直接设定。delta 即目标值 |

**Variables:**

| 变量 | 符号 | 类型 | 范围 | 描述 |
|------|------|------|------|------|
| 基础权重 | baseWeight | float | [0.0, 1.0] | SO 中的 BaseWeight 或已叠加的运行时权重 |
| 变化量 | delta | float | [-1.0, 1.0] | ContentChange 中定义的调整量 |
| 最终权重 | newWeight | float | [0.0, 1.0] | 夹紧后的运行时权重 |

**Output Range:** 0.0 到 1.0 之间。Clamp 确保任何操作都不会将权重推出有效范围。

**叠加顺序 (多个 ModifyTagWeight 作用于同一标签):**

当同一碎片的同一标签被多次 ModifyTagWeight 修改时 (例如碎片 A 的选择 1 和碎片 B 的选择 2 都修改了碎片 C 的 `nostalgia` 权重)，按变更的 OrderIndex 升序依次应用:

```
weight_after_N = Clamp(ApplyOperation(weight_after_N-1, operation_N, delta_N), 0.0, 1.0)
其中 weight_after_0 = SO 中的 BaseWeight
```

**示例:**
- 碎片 C 的 `nostalgia` BaseWeight = 0.5
- 碎片 A 的选择 1: ModifyTagWeight(C, nostalgia, +0.2, Add) — OrderIndex=1
- 碎片 B 的选择 2: ModifyTagWeight(C, nostalgia, ×1.5, Multiply) — OrderIndex=2
- 结果: weight_1 = Clamp(0.5 + 0.2) = 0.7 → weight_2 = Clamp(0.7 × 1.5) = 1.0 (夹紧)

## Edge Cases

- **如果 ApplyChanges 的 targetFragmentId 指向不存在的碎片**: 验证失败——LogWarning + 整个 ApplyChanges 调用被跳过。_changeLog 不记录失败的调用。不阻塞游戏。

- **如果 ContentChange 中的 LayerId 在目标碎片中不存在**: 该条 ToggleVisualLayer 被跳过——LogWarning。同批次中的其他有效变更仍然应用。不阻塞游戏。

- **如果 ContentChange 尝试修改 IsMutable = false 的图层**: 该条被拒绝——LogWarning "尝试修改不可变图层 [LayerId]"。不抛出异常。该批次中的其他变更仍然应用。

- **如果两个不同选择 (不同 choiceId) 都修改了同一碎片的同一字段**: 按 OrderIndex 升序依次应用——后发生的覆盖先发生的。确定性结果。例如碎片 A 的选择 1 将 layer_X 设为 visible=true，碎片 B 的选择 3 将 layer_X 设为 visible=false → 最终状态取决于哪个 OrderIndex 更大。

- **如果同一选择被重复触发 (IsRepeatable = true)**: 同一 (targetFragmentId, choiceId) Key → 叠加层中该条目被覆盖为最新结果。_changeLog 中每次触发都有一条独立日志。最后一次触发的内容生效。

- **如果 ApplyChanges 传入空的 ContentChange 数组**: 合法——不修改叠加层，但 _changeLog 中仍记录一条日志条目 (OrderIndex 递增, OverlayVersion 递增, OnOverlayChanged 触发)。此行为支持"无内容变化但需要记录的选择"场景。

- **如果 ModifyTagWeight 的 delta 和 operation 组合将权重推出 [0.0, 1.0]**: Clamp 强制夹紧。例如 weight=0.9, delta=+0.3, Add → Clamp(1.2, 0.0, 1.0) = 1.0。不报错——夹紧是预期行为。

- **如果 ModifyTagWeight 使用 Multiply 且 delta 为负值**: Multiply 操作下 delta 被解释为缩放因子。delta < 0 无实际意义——夹紧到 [0.0, 1.0] 后效果等价于 Set(0)。LogWarning "Multiply operation with negative delta [value] — clamped to 0"。

- **如果 UnlockAssociation 的目标关联已经解锁**: 幂等操作——跳过。不报错，不重复记录。

- **如果 SetFlag 设置一个已存在的 Flag 为相同值**: 幂等操作——跳过，不触发任何事件。若值为不同值 → 覆盖旧值 + OverlayVersion++。

- **如果 GetCurrentState 在碎片 SO 仍在异步加载期间被调用**: 返回 null 或抛出 `InvalidOperationException`。调用方 (如交互系统) 应在调用前确认碎片已加载。OnFragmentTransitioned 事件保证调用时碎片 SO 已就绪。

- **如果存档恢复时叠加层中的 fragmentId 在当前碎片注册表中不存在 (存档版本不匹配)**: 该叠加层条目被标记为孤儿——LogWarning "存档中的碎片 [fragId] 不存在于当前版本——跳过其叠加层"。其他有效条目正常恢复。不阻塞加载。

- **如果存档恢复时 _flags 中的 flagId 对应的 SetFlag 来自已删除的碎片**: Flag 仍然有效——Flag 的生命周期独立于碎片。已删除的碎片设置的 Flag 保留在字典中。若设计师需要清除 — 由跨章节状态追踪 (#16) 提供 Flag 管理工具。

- **如果 _changeLog 在长时间游戏中条目数超过 10,000**: _changeLog 仅追加，不截断。10,000 条目 × ~200 bytes/条目 ≈ 2MB——内存可接受。若达到 100,000 条目 (不可能在正常游戏中)，记录 Warning 并继续追加。

- **如果同一个 ContentChange 的 TargetFragmentId 等于当前碎片 (自身修改)**: 正常流程——立即触发 OnOverlayChanged，场景管理器刷新当前画面。这是最常见的变更模式。

- **如果跨碎片变更的目标碎片在同一章节内但 ChapterId 不匹配**: 编辑器验证应在构建阶段拦截。运行时防御——ApplyChanges 接受变更，不验证 ChapterId (信任构建时验证)。

- **如果玩家在 ApplyChanges 执行期间 (Applying 状态) 触发了场景过渡**: Applying 状态 <1ms 同步完成——场景过渡在此之后才开始。不需要显式防护。`OnOverlayChanged` 事件在过渡前已触发。

- **如果 OverlayVersion 溢出 (int.MaxValue)**: 每次 ApplyChanges 递增一次。正常游戏最多几千次选择——永远不会溢出。不需要防护。

- **如果 FlagSet 条件查询的 Flag 从未被设置过**: `_flags.TryGetValue` 返回 false (默认值)。未设置的 Flag 视为 false。这与 Flag 的默认语义一致。

## Dependencies

**硬依赖:**

| 系统 | 性质 | 接口 |
|------|------|------|
| **记忆碎片数据模型 (#8)** | 硬依赖 | MemoryFragment SO, ContentChange 定义 (6 种类型), ConditionGroup 系统 |
| **交互系统 (#11)** | 硬依赖 | ApplyChanges(fragmentId, choiceId, ContentChange[]) — 变更的唯一入口 |
| **数据管理 (#2)** | 硬依赖 | GetFragment(fragmentId) → MemoryFragment — 状态合并时获取基础 SO |
| **存档系统 (#7)** | 硬依赖 | OnSaveRequested / OnLoadRestore — 叠加层和 Flag 的持久化 |

**软依赖:**

| 系统 | 性质 | 接口 |
|------|------|------|
| **场景管理 (#6)** | 软依赖 | OnFragmentTransitioned(chapterKey, fragmentId) — 更新 _visitedFragments。若 #6 未就绪，VisitedFragment 条件始终返回 false |
| **情感标签系统 (#10)** | 软依赖 | GetTagCatalog() — 验证 ModifyTagWeight 的 TagId。若 #10 未就绪，跳过 TagId 验证 |
| **微动画 (#9)** | 软依赖 | OnOverlayChanged 事件的订阅者。若 #9 未就绪，变更无视觉动画但仍正确应用 |

**下游系统 (依赖本系统):**

| 系统 | 性质 | 接口 |
|------|------|------|
| **多结局 (#14)** | 硬依赖 | EvaluateCondition(ConditionGroup), GetFlag(flagId) |
| **跨章节状态追踪 (#16)** | 硬依赖 | _flags, _visitedFragments, _completedChapters |
| **HUD (#17)** | 软依赖 | OnOverlayChanged 事件 — 画面刷新提示 |
| **画廊/回忆录 (#24, Full Vision)** | 软依赖 | _changeLog — 重构玩家选择时间线 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| _changeLog 最大条目数 (Warning 阈值) | 10,000 | 5,000–50,000 | 超过此值记录 Warning。仅追加不截断 |
| OverlayVersion 类型 | int | int / long | 当前 int 足够 (正常游戏 < 10,000 次选择) |
| Flag 命名约定 | `"ch{id}_{event}"` | — | 建议但非强制。影响跨章节调试可读性 |
| GetCurrentState 缓存策略 | 每次查询重新合并 | 缓存 / 每次合并 | 当前设计每次合并——若性能分析发现瓶颈，可改为 OverlayVersion 驱动的缓存失效 |

本系统不含需要设计师频繁调整的游戏数值参数。叠加层的合并行为由规则定义，不由参数驱动。

## Visual/Audio Requirements

本系统自身不产生视觉或音频输出。`OnOverlayChanged(fragmentId)` 事件是其他系统触发视觉/音频反馈的信号：

- **微动画 (#9)** 订阅 OnOverlayChanged → 播放图层切换动画、物件状态变化特效
- **音频系统 (#3)** 通过交互反馈系统 (#18) 间接获知变化——选择确认音效由 #18 管理

本系统仅提供"变化发生"的信号——视觉和音频的具体呈现由下游系统负责。

## UI Requirements

本系统不包含玩家可见 UI。以下 UI 行为由其他系统通过本系统的接口驱动：

- **HUD (#17)** 订阅 OnOverlayChanged → 刷新画面上的文本内容 (当 SetTextContent 变更生效时)
- **画廊/回忆录 (#24, Full Vision)** 读取 _changeLog → 重构"玩家选择时间线"UI

本系统不定义 UI 布局、样式或交互——仅提供数据。

## Acceptance Criteria

- **GIVEN** 碎片 A 的一个 ChoiceOption 包含 2 个 ContentChange (ToggleVisualLayer + SetObjectState)，**WHEN** 玩家选择该选项，**THEN** ChangeTracker.ApplyChanges 被调用，_overlay 中新增 Key (targetFragmentId, choiceId)，ContentOverrides 包含 2 条变更。OverlayVersion 递增 1。_changeLog 新增 1 条日志。

- **GIVEN** 碎片 B 的 VisualLayer "layer_rain" 的 SO 默认值为 Visible=false，且叠加层中有一条 ToggleVisualLayer 将其设为 true，**WHEN** 调用 `GetCurrentState("frag_B")`，**THEN** 返回的 ResolvedFragmentState 中 "layer_rain" 的 Visible = true。其他未被修改的图层保持 SO 默认值。

- **GIVEN** 碎片 C 的 EmotionalTag "nostalgia" BaseWeight = 0.5，叠加层中有 ModifyTagWeight (delta=+0.3, Add) 和 ModifyTagWeight (delta=×0.8, Multiply)，**WHEN** GetCurrentState 合并，**THEN** nostalgia 的运行时权重 = Clamp(Clamp(0.5 + 0.3) × 0.8) = Clamp(0.8 × 0.8) = 0.64。

- **GIVEN** ContentChange 的 TargetFragmentId 指向不存在的碎片，**WHEN** ApplyChanges 被调用，**THEN** LogWarning 输出，整个调用被跳过，_overlay 和 _changeLog 均无变化。

- **GIVEN** ContentChange 尝试修改 IsMutable = false 的图层，**WHEN** GetCurrentState 合并到该变更，**THEN** 该条 ToggleVisualLayer 被跳过，LogWarning 输出，同批次其他有效变更正常应用。

- **GIVEN** 玩家在碎片 A 选择了选项 X (触发 SetFlag("ch1_letter_kept", true))，**WHEN** 后续碎片 B 的物件条件为 `FlagSet("ch1_letter_kept", true)`，**THEN** EvaluateCondition 返回 true——物件变为可交互。

- **GIVEN** 存档包含 3 条叠加层条目和 2 个 Flag，**WHEN** 游戏从存档恢复，**THEN** _overlay 包含 3 条条目，_flags 包含 2 个 Flag，GetCurrentState 返回合并后的状态。OverlayVersion 重置为已恢复条目数。

- **GIVEN** 存档中有一条叠加层条目引用了已在新版本中删除的碎片 ID，**WHEN** 加载存档，**THEN** 该条目被标记为孤儿——LogWarning——其他 2 条正常恢复。加载不阻塞。

- **GIVEN** 碎片 D 的一个物件需要 `VisitedFragment("frag_E")` 条件，且玩家之前已访问过 frag_E，**WHEN** 场景管理触发 OnFragmentTransitioned(chapterKey, "frag_D") → 交互系统查询物件条件，**THEN** EvaluateCondition 返回 true——物件可交互。

- **GIVEN** 叠加层中已有 Key (frag_A, choice_1)，**WHEN** 同一选择被再次触发 (IsRepeatable = true) 并传入不同的 ContentChanges，**THEN** 叠加层中该 Key 的 ContentOverrides 被覆盖为新的变更内容。_changeLog 中两条日志条目各自独立保留。

- **GIVEN** UnlockAssociation 的目标关联已在之前的选择中被解锁，**WHEN** 第二个选择也触发 UnlockAssociation 同一目标，**THEN** 幂等跳过——ResolvedFragmentState 中该关联仍为已解锁状态，不重复添加。

- **GIVEN** 玩家在章节 1 完成时触发了章节完成事件，**WHEN** 章节 3 的某结局触发条件包含 `ChapterCompleted("ch1")`，**THEN** EvaluateCondition 返回 true。

## Open Questions

- **GetCurrentState 缓存策略**: 当前设计为每次查询重新合并。若性能分析发现频繁查询导致 CPU 开销 (尤其是在每帧渲染循环中查询碎片状态)，应改为 OverlayVersion 驱动的缓存失效——版本号变化时重建缓存。此优化在 MVP 阶段不需要，但架构上应预留缓存接口。(Owner: gameplay-programmer, 架构阶段 ADR)

- **ContentOverrides 的 IL2CPP 序列化**: gameplay-programmer 审查指出 System.Text.Json 在 IL2CPP 构建中需要源生成器。建议 ContentOverrides 使用 Unity JsonUtility (仅需 [Serializable])，外层 SaveData 继续使用 System.Text.Json。此决策在存档系统对接时确认。(Owner: gameplay-programmer + 存档系统 #7)

- **Flag 字典的所有权**: 当前设计中 _flags 由 ChangeTracker 写入，但跨章节状态追踪 (#16) 是 Flag 的"持久化所有者"。若 #16 设计时有不同的 Flag 管理需求 (如 Flag 分组、Flag 过期、Flag 版本化)，本系统的 Flag 写入接口可能需要调整。(Owner: 跨章节状态追踪 #16 设计者)

- **_changeLog 的持久化**: 当前设计中 _changeLog 不随存档持久化——存档系统只序列化 _overlay 和 _flags。若画廊系统 (#24) 需要跨会话的完整选择历史，_changeLog 需要纳入存档范围或由 #24 自行重建。(Owner: 画廊系统 #24 设计者, Full Vision 阶段)
