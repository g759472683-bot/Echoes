# 多结局系统 (Multi-Ending System)

> **Status**: Designed (pending review)
> **Author**: 用户 + game-designer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 2 (不完美才是力量) — 结局判定逻辑——"它不需要好，它需要真"

## Overview

多结局系统是《回响》中"选择的重量"的最终体现。它不是简单的"选A得结局1、选B得结局2"的分支树——而是像一个在记忆网络中缓慢沉淀的过程：玩家在每一幅画卷中触碰的物件、做出的选择、走过的关联路径，都在悄无声息地累积为某种"叙事重力"。当一章的最后一个碎片被穿过后，这些重力汇聚成一个确定的终点——一个结局。

在技术层面，它是一个条件评估与权重累加引擎：收集当前章节所有碎片中定义的 EndingTrigger，通过变化追踪系统 (#12) 评估每个触发条件是否满足，累加满足条件的 ContributionWeight，按章节结局定义中的阈值判定哪个结局被触发。跨章节隐藏结局在此基础上增加了跨章节条件检查——玩家在第一章保存的一封信、在第二章揭开的一个秘密，会在第三章的终局中汇聚。

这个系统不渲染结局画面（那是结局呈现 #20 的职责），不决定玩家"应该"看到什么——它只回答一个问题：**"以你走过的路来看，你应该抵达哪个终点？"**

## Player Fantasy

游戏的标题不是随便选的。

你在一幅画中触碰了一个物件。画面起了涟漪。你不一定看见了那圈涟漪——你只是继续往前走，穿过关联的网络，进入下一幅画，再下一幅。但涟漪没有停。它在你看不见的地方扩散、交叠、与其他的涟漪碰撞。你走了一整章的路，那些涟漪也跟了你一整章——它们越过碎片的边界，穿过情感的标签网络，在时间的褶皱里来回折射。

然后你走到终局。画面静止。那些散落的、飘荡的、你一路上激起的回声，此刻同时抵达你——它们叠加成一个完整的声音，一个只有用你走过的全部路径才能合成的声波。

这就是《回响》。**结局不是系统判给你的——结局是你自己的回响。** 是你每一次触碰发出的声波，穿越整个记忆网络之后，回到了你自己耳边。

你听着它。它可能不圆满，可能带着破碎的泛音——但它是真的。它听起来像你。

## Detailed Design

### Core Rules

**规则 1 — 结局类型 (Ending Types)**

| 类型 | 作用域 | MVP 数量 | 定义 |
|------|--------|----------|------|
| `ChapterEnding` | 单章结束时判定 | 每章 2-5 个 | 章节的标准叙事结论 |
| `HiddenEnding` | 跨章节条件 — 在最终把关章节结束时判定 | 1 (MVP), 3 (Full Vision) | 需要特定跨章触发链的稀有结局 |

无 `TrueEnding` 层级。Anti-pillar: NOT "唯一真结局"——隐藏结局不"更好"，只是更难发现的真相。

**规则 2 — EndingDefinition 数据结构**

每个章节的 `EndingDefinition[]` 存储在章节配置中（由章节管理 #15 拥有，多结局系统消费）：

| 字段 | C# 类型 | 必填 | 说明 |
|------|---------|------|------|
| `EndingId` | `string` | 是 | 唯一标识——匹配 MemoryFragment 中 EndingTrigger.EndingId |
| `EndingType` | `enum` | 是 | `ChapterEnding` 或 `HiddenEnding` |
| `ChapterId` | `string` | 是 | 此结局在哪个章节被判定 |
| `MinimumScore` | `float` | 是 | 最低累加分数阈值 [0.0, 1.0]。默认结局用 0.0 |
| `IsDefault` | `bool` | 是 | 若为 true，此结局是章节的兜底结局。每章恰好一个 |
| `EmotionalAffinity` | `string?` | 否 | 可选的主导情感类别。设置后，若与玩家路径主导情感匹配则触发路径加成 |
| `DisplayNameKey` | `TableReference` | 是 | 本地化结局名——用于画廊/制作人员名单 |

**规则 3 — 触发器收集 (Trigger Collection)**

当 `ResolveEnding(chapterId)` 被调用：

1. 加载 `chapterId` 对应的 `EndingDefinition[]`
2. 遍历 `chapterId` 中所有 MemoryFragment，收集全部 `EndingTrigger` 条目
3. 按 `EndingId` 分组触发器——每组是贡献给一个结局的条件集合
4. 仅保留 `EndingId` 匹配某个 `EndingDefinition` 的触发器
5. 引用未知 `EndingId` 的触发器被忽略 + LogWarning（设计师配置错误）

**规则 4 — 判定算法 (Evaluation Algorithm)**

```
ResolveEnding(chapterId) → ResolvedEnding:

  Step 1: 加载 chapterId 的 EndingDefinition[]
  Step 2: 收集该章节所有碎片中的 EndingTrigger[]
  Step 3: 按 EndingId 分组触发器

  Step 4: FOR EACH EndingDefinition def:
    a. 获取 def.EndingId 对应的触发器组
    b. 必要门 (ESSENTIAL GATE):
       FOR EACH 该组的 IsEssential == true 的触发器:
         IF ChangeTracker.EvaluateCondition(trigger.TriggerCondition) == false:
           → def 被取消资格 (DISQUALIFIED)
    c. 累加分数 (ADDITIVE SCORE):
       score = 0.0
       FOR EACH 该组的所有触发器 (含 essential 和 non-essential):
         IF ChangeTracker.EvaluateCondition(trigger.TriggerCondition) == true:
           score += trigger.ContributionWeight
       score = Clamp(score, 0.0, 1.0)
    d. 关联路径加成 (PATH BONUS):
       IF def.EmotionalAffinity 已设置 AND def.EmotionalAffinity == dominantPathEmotion:
         score *= (1.0 + pathBonusWeight)
    e. 阈值检查 (THRESHOLD):
       IF score >= def.MinimumScore:
         qualifiedEndings.Add((def, score))

  Step 5: IF qualifiedEndings 为空 → 返回章节的 IsDefault 结局
  Step 6: 按 score 降序排列 qualifiedEndings
  Step 7: 应用 Tie-Breaking (规则 5)
  Step 8: 返回胜出者
```

**规则 5 — 平局打破 (Tie-Breaking)**

1. **必要条件数**: 满足更多 `IsEssential` 触发器的结局胜出（更具体的路径优先）
2. **新颖性偏向**: 不在 `UnlockedEndingIds` 中的结局胜出（优先展示新内容）
3. **定义顺序**: `EndingDefinition[]` 数组中先出现的胜出（设计师可控制，确定性兜底）

**规则 6 — 默认结局 (Default Ending)**

每章必须恰好定义一个 `IsDefault = true`、`MinimumScore = 0.0` 的 EndingDefinition：
- 不需要 EndingTrigger（碎片可定义但非必要——默认结局无条件触发）
- 永远合格（Step 4e 始终通过）
- 保证游戏在章节完成时永远不会软锁

若章节配置中零个或多个 `IsDefault = true`，系统记录 Error 并选择第一个 EndingDefinition 作为兜底。

**规则 7 — 隐藏结局跨章节机制**

隐藏结局使用已有的 ConditionGroup 系统——无需特殊判定路径：
- 隐藏结局的 `EndingTrigger` 定义在**最终把关章节**的碎片中
- 条件通过 `FlagSet(flagId, value)` 和 `ChapterCompleted(chapterId)` 引用跨章状态
- Flags 跨章节持久化：ChangeTracker (#12) → 跨章节状态追踪 (#16)
- 隐藏结局的 `EndingDefinition.ChapterId` = 最终把关章节
- 当该章节的 `ResolveEnding()` 被调用时，所有跨章条件一起评估

**规则 8 — 关联路径加成 (Path Bonus Hook — MVP 默认关闭)**

系统计算章节的 `dominantPathEmotion`:
```
dominantPathEmotion = argmax(count per emotional category across all visited fragments in chapterId)
```
情感类别 = Joy, Sadness, Anger, Fear, Peace, Longing (6 类——由情感标签系统 #10 定义)。

`pathBonusWeight` 调参默认 = 0.0 (MVP 禁用)。若 > 0.0:
- `EmotionalAffinity` 匹配 `dominantPathEmotion` 的结局获得 `score *= (1.0 + pathBonusWeight)`
- 若 `EmotionalAffinity` 为空 → 无加成

保留 Hook + 默认关闭 = MVP 可测试但不受未经验证的算法影响。

**规则 9 — 结局可重判 (Re-evaluation Allowed)**

每次 `ResolveEnding(chapterId)` 被调用时，系统**重新**收集触发器、重新评估条件、重新计算分数。不缓存判定结果——玩家回到之前的碎片、做出不同选择后，可以触发不同的结局。

`UnlockedEndingIds` 使用**并集语义**: 新结局添加，旧结局保留。玩家可以通过重玩同一章节解锁多个结局变体。

**规则 10 — 已解锁结局持久化**

```
UnlockedEndingIds: HashSet<string>  // 玩家在所有周目中抵达过的所有结局 ID
```

- 持久化在存档配置中 (存档系统 #7)
- 提供给：成就追踪 (#23)、画廊 (#24)、章节选择 (#21)
- 使用并集语义——解锁后永不移除

**规则 11 — ResolvedEnding 输出结构**

```
ResolvedEnding {
  EndingId: string                // 胜出结局的 ID
  EndingType: enum                // ChapterEnding | HiddenEnding
  Score: float                    // 最终分数
  IsDefault: bool                 // 是否使用了兜底结局
  IsNewUnlock: bool               // 此 EndingId 是否不在 UnlockedEndingIds 中
  QualifiedEndings: List<         // 所有达到阈值的结局 (供调试/画廊)
    (EndingId, Score)
  >
  DominantPathEmotion: string     // 计算出的主导路径情感 (供结局呈现提示)
}
```

**规则 12 — MVP 范围边界**

**MVP 包含:**
- ChapterEnding 判定（必要门 + 累加分数 + 阈值）
- 默认结局兜底
- 跨章节 HiddenEnding 支持（通过 FlagSet + ChapterCompleted 条件）——1 个隐藏结局
- UnlockedEndingIds 并集持久化
- 关联路径加成 Hook（已实现，默认 weight=0.0）
- 结局可重判（无缓存锁定）

**MVP 不包含:**
- 关联路径加成启用（需 playtest 验证）
- 结局画面渲染（归结局呈现 #20）
- 画廊/回忆录重播 (#24, Full Vision)
- Steam 成就 (#23, Full Vision)

### States and Transitions

| 状态 | 描述 | 触发 | 退出 |
|------|------|------|------|
| **Idle** | 系统未激活。无章节进行中 | 游戏启动 / 返回主菜单 | Chapter Management 调用 `OnChapterStart(chapterId)` → Tracking |
| **Tracking** | 章节进行中。EndingTrigger 被动存在于碎片中——无主动计算。系统等待判定调用 | `OnChapterStart(chapterId)` | Chapter Management 调用 `ResolveEnding(chapterId)` → Resolving |
| **Resolving** | 判定算法执行中。同步调用，<1ms | `ResolveEnding(chapterId)` 被调用 | 算法完成 → 返回 ResolvedEnding |
| **Resolved** | 结局已判定。`UnlockedEndingIds` 若为新结局则更新。结果返回给调用方 | Resolving 完成 | 章节过渡或返回菜单。下次调用 `ResolveEnding(chapterId)` 重新进入 Resolving（允许重判） |

Tracking → Resolving → Resolved 可在同一章节内多次循环——玩家回到碎片、改变选择后，Chapter Management 可再次调用 ResolveEnding。

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 数据流 | 关键性 |
|------|------|------|--------|--------|
| 上游 | **记忆碎片数据模型 (#8)** | `fragment.EndingTriggers: List<EndingTrigger>` | 读取判定章节中所有碎片的 EndingTrigger 定义 | 硬依赖 |
| 上游 | **记忆变化追踪 (#12)** | `EvaluateCondition(ConditionGroup) → bool`, `GetFlag(flagId) → bool`, `GetVisitedFragments() → HashSet<string>`, `GetCompletedChapters() → HashSet<string>` | 评估每个触发条件并计算已访问碎片集合 | 硬依赖 |
| 上游 | **情感标签系统 (#10)** | 查询碎片的 EmotionalTags → 情感类别 | 计算 dominantPathEmotion（pathBonusWeight > 0 时需要） | 软依赖 |
| 上游 | **章节管理 (#15)** | 调用 `ResolveEnding(chapterId)` 和 `OnChapterStart(chapterId)` | 触发判定；提供章节生命周期事件；提供 EndingDefinition[] | 硬依赖 |
| 下游 | **结局呈现 (#20)** | 接收 `ResolvedEnding` (EndingId, EndingType, IsNewUnlock, DominantPathEmotion) | 渲染结局画面、文本和制作人员名单 | 硬依赖 |
| 下游 | **成就与收集追踪 (#23)** | `UnlockedEndingIds: HashSet<string>` | 每次判定后更新完成统计 | 软 (Full Vision) |
| 下游 | **存档系统 (#7)** | UnlockedEndingIds 纳入存档配置 | 持久化玩家见过的结局 | 硬依赖 |
| 下游 | **画廊/回忆录 (#24)** | `UnlockedEndingIds` + ResolvedEnding 历史 | 填充结局画廊供重播 | 软 (Full Vision) |

## Formulas

### 1. 累加分数 (Additive Score) — 结局判定核心

```
score(endingId) = Σ ContributionWeight(trigger)
  FOR EACH trigger IN triggers[endingId]
  WHERE ChangeTracker.EvaluateCondition(trigger.TriggerCondition) == true

score ∈ [0.0, 1.0]  (clamped)
```

每个 `ContributionWeight ∈ [0.0, 1.0]` —— 由设计师在 MemoryFragment.EndingTrigger 中设定。

### 2. 关联路径加成 (Path Bonus) — MVP 默认关闭

```
dominantPathEmotion(chapterId) = argmax( countPerCategory )
  WHERE countPerCategory[cat] = COUNT(fragment IN visitedFragments[chapterId]
    WHERE DominantCategory(fragment.EmotionalTags) == cat)

IF ending.EmotionalAffinity != null AND ending.EmotionalAffinity == dominantPathEmotion:
  adjustedScore = score × (1.0 + pathBonusWeight)
ELSE:
  adjustedScore = score
```

`pathBonusWeight` 默认值: 0.0 (MVP 禁用)。安全范围: [0.0, 0.15]。

### 3. 阈值判定 (Threshold Check)

```
ending 合格 IF:
  allEssentialTriggersPassed(ending) == true
  AND
  adjustedScore >= ending.MinimumScore
```

### 4. Tie-Breaking 优先级

```
比较函数 Compare(endingA, endingB):
  1. essentialCount[A] vs essentialCount[B]          → 多者胜
  2. IsNewUnlock[A] vs IsNewUnlock[B]                 → 新解锁者胜
  3. definitionOrder[A] vs definitionOrder[B]          → 先定义者胜
```

### 5. 已解锁并集

```
UnlockedEndingIds = UnlockedEndingIds ∪ { resolvedEnding.EndingId }
```

每次判定后执行。幂等——重复添加同一 ID 无影响。

## Edge Cases

- **所有结局的必要条件都不满足 (All Essential Gates Fail)**: 每个结局的 IsEssential 触发器中至少有一个未被满足 → 全部被取消资格。qualifiedEndings 为空 → 返回默认结局。玩家到达章节终点但未能触发任何特定结局——"你在这章中走了一条无人走过的路，抵达了一个无人命名的终点。"

- **碎片中定义了零个 EndingTrigger**: 合法——大多数碎片不定义结局触发条件。只有关键碎片（章节最后几个碎片）才定义触发器。若全章碎片都没有 EndingTrigger，只有默认结局可触发。

- **触发器指向不存在的 EndingId**: EndingTrigger.EndingId 与任何 EndingDefinition.EndingId 不匹配 → 触发器被忽略 + LogWarning。这不会阻塞判定——其他结局仍正常评估。这是设计师配置错误，应在 Editor 验证中捕获。

- **单碎片定义了多个结局的 EndingTrigger**: 合法且推荐——一个碎片中的不同选择可同时推进多个结局的累积分数。同一碎片可包含 EndingId="ending_sad" 和 EndingId="ending_peace" 的触发器——各自独立计分。

- **ContributionWeight 累加超过 1.0**: Clamp 到 1.0。设计师应调整单个 ContributionWeight 使总和不显著超过 1.0——否则多个结局都可能满分，仅靠 tie-breaking 区分。

- **章节中没有 IsDefault=true 的 EndingDefinition**: 配置错误。系统记录 Error，选 EndingDefinition[] 中第一个作为兜底。Editor 验证应阻止此配置进入构建。

- **多个 EndingDefinition 设置了 IsDefault=true**: 配置错误。系统使用第一个遇到的，其余视为普通结局。Editor 验证应阻止。

- **隐藏结局把关章节在触发前已完成 (Player completed gating chapter without meeting hidden ending conditions)**: 合法——隐藏结局是"稀有"的。若条件未满足，隐藏结局被取消资格（必要门未通过）。玩家需在新周目中重试——或回到早期章节改变选择。

- **跨章 Flag 在结局判定时尚未设置 (Flag referenced by hidden ending doesn't exist yet)**: FlagSet("some_flag", true) → EvaluateCondition 返回 false（Flag 不存在或为 false）。触发器不满足。不会崩溃——未设置的 Flag 默认为 false。

- **同一章节被多次 ResolveEnding (Re-evaluation)**: 合法——规则 9 明确允许。每次调用重新收集触发器并重新评估。若玩家在两次调用之间改变了选择（不同 Flag、不同 ChoiceMade），结局可能不同。

- **玩家在章节中途返回主菜单**: 无影响——多结局系统在 Tracking 状态中不持有任何暂态。下次进入章节时从存档恢复状态即可。

- **存档中的 UnlockedEndingIds 包含已被删除的结局 (Full Vision 更新后旧结局 ID 不再存在)**: 旧 ID 保留在 HashSet 中——不影响新结局判定。画廊 (#24) 遇到不存在的 EndingId 时显示为"已移除的内容"或跳过。

## Dependencies

### 硬依赖 (Hard Dependencies)

| 系统 | 性质 | 接口 |
|------|------|------|
| **记忆碎片数据模型 (#8)** | 硬依赖 — 无此系统则无 EndingTrigger 定义 | MemoryFragment.EndingTriggers: List\<EndingTrigger\> |
| **记忆变化追踪 (#12)** | 硬依赖 — 无此系统则无法评估触发条件 | EvaluateCondition(ConditionGroup): bool, GetFlag(flagId): bool, GetVisitedFragments(): HashSet\<string\>, GetCompletedChapters(): HashSet\<string\> |
| **章节管理 (#15)** | 硬依赖 — 无此系统则无 EndingDefinition[] 且无判定触发时机 | EndingDefinition[] 章节配置, OnChapterStart(chapterId), ResolveEnding(chapterId) 由 #15 调用 |
| **存档系统 (#7)** | 硬依赖 — 无此系统则 UnlockedEndingIds 无法跨会话持久化 | UnlockedEndingIds 序列化到存档配置 |

### 软依赖 (Soft Dependencies)

| 系统 | 性质 | 接口 |
|------|------|------|
| **情感标签系统 (#10)** | 软依赖 — 仅 pathBonusWeight > 0 时需要 | 查询碎片的 EmotionalTags → 情感类别 → 计算 dominantPathEmotion |
| **结局呈现 (#20)** | 软依赖 — 多结局系统不依赖结局呈现运行，但结局呈现是主要消费方 | 接收 ResolvedEnding → 渲染结局画面 |
| **成就与收集追踪 (#23)** | 软依赖 — Full Vision 功能 | 读取 UnlockedEndingIds → 计算完成百分比 |
| **画廊/回忆录 (#24)** | 软依赖 — Full Vision 功能 | 读取 UnlockedEndingIds + 结局历史 |

### 下游系统

| 系统 | 消费内容 |
|------|---------|
| **章节管理 (#15)** | ResolvedEnding → 章节完成流程 |
| **结局呈现 (#20)** | ResolvedEnding → 渲染 |
| **成就追踪 (#23)** | UnlockedEndingIds |
| **画廊 (#24)** | UnlockedEndingIds + 结局历史 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| pathBonusWeight | 0.0 | 0.0–0.15 | 关联路径对结局的加成强度。0.0 = 禁用。调高 → 玩家情绪路径更显著地影响结局 |
| MinimumScore (每结局) | 设计师设定 | 0.0–0.5 | 单个结局的合格阈值。默认结局固定为 0.0。调高 → 需更多触发条件满足才能触发该结局 |
| ContributionWeight (每触发器) | 设计师设定 | 0.0–1.0 | 单个触发器的贡献权重。建议每结局的触发器总 ContributionWeight 不超过 1.0 |
| 每章 ChapterEnding 数 (MVP) | 2–5 | 2–5 | 每章标准结局变体的数量范围 |
| HiddenEnding 数 (MVP) | 1 | 1–3 | MVP 固定 1 个隐藏结局 |

## Visual/Audio Requirements

纯判定逻辑——无自有视觉或音频。结局画面的渲染归结局呈现 (#20)。唯一间接视觉含义：UnlockedEndingIds 在章节选择 (#21) 中驱动已解锁结局的显示。

## UI Requirements

无自有 UI。ResolvedEnding 的数据由结局呈现 (#20) 渲染为结局画面。

## Acceptance Criteria

- **GIVEN** 玩家在 Ch01 中做出了选择——在 frag_03 中选了 "keep_letter"（触发 ContributionWeight=0.4 给 ending_A），在 frag_07 中选了 "open_window"（触发 ContributionWeight=0.3 给 ending_A），且 ending_A 的 IsEssential 触发器已满足、MinimumScore=0.5，**WHEN** Chapter Management 调用 ResolveEnding("ch01")，**THEN** ending_A 的 score = 0.7 ≥ 0.5 → ending_A 胜出。

- **GIVEN** ending_B 的 IsEssential 触发器引用 FlagSet("found_secret", true)，而该 Flag 为 false，**WHEN** ResolveEnding 执行，**THEN** ending_B 在必要门被取消资格——即使其非必要条件全部满足且 ContributionWeight 很高。

- **GIVEN** 玩家未满足任何结局的必要门条件，**WHEN** ResolveEnding 执行，**THEN** qualifiedEndings 为空 → 返回 IsDefault=true 的默认结局。IsDefault 标志在 ResolvedEnding 中为 true。

- **GIVEN** ending_A score=0.6, ending_B score=0.6（平局），ending_A 有 3 个 IsEssential 满足，ending_B 有 2 个，**WHEN** Tie-Breaking 执行，**THEN** ending_A 胜出（必要条件数更多）。

- **GIVEN** 平局且必要条件数相同，ending_A 已在 UnlockedEndingIds 中，ending_B 不在，**WHEN** Tie-Breaking 执行，**THEN** ending_B 胜出（新颖性偏向——优先展示新内容）。

- **GIVEN** 隐藏结局 hidden_reunion 的 EndingTrigger 定义在 Ch03 的碎片中，条件为 `{ All: [FlagSet("ch1_letter", true), ChapterCompleted("ch1"), FlagSet("ch2_secret", true)] }`，且这些 Flag 均已设置、章节均已完成，**WHEN** Ch03 的 ResolveEnding("ch03") 执行，**THEN** hidden_reunion 的必要门通过，进入累加分数计算。

- **GIVEN** 玩家第一次玩 Ch01 抵达 ending_A，**WHEN** ResolveEnding 返回，**THEN** UnlockedEndingIds 包含 "ending_A"。玩家重玩 Ch01 抵达 ending_B → UnlockedEndingIds 现在包含 {"ending_A", "ending_B"}（并集——两个都保留）。

- **GIVEN** 玩家抵达 ending_A → 回到 frag_03 改变选择 → 再次调用 ResolveEnding("ch01")，**WHEN** 新选择改变了 Flag 和 ChoiceMade 状态，**THEN** 重新评估产生不同的结局（重判成功）。不缓存旧结果。

- **GIVEN** 章节 EndingDefinition[] 中有 1 个 IsDefault=true、MinimumScore=0.0 的结局，**WHEN** 游戏启动，**THEN** 多结局系统验证通过。若零个或多个 IsDefault=true → 系统记录 Error。

- **GIVEN** EndingTrigger.EndingId 引用了一个不存在的 EndingId（与任何 EndingDefinition 不匹配），**WHEN** ResolveEnding 执行，**THEN** 该触发器被忽略 + LogWarning。其他结局仍正常评估。

## Open Questions

- **多结局 vs 多周目平衡**: 每章 2-5 个结局变体——玩家需要重玩多少次才能看到全部？若每章平均 3 个结局，全部解锁需至少 3 次完整流程。这是否对玩家要求过高？建议 MVP 阶段追踪解锁率数据——若大部分玩家只看到 1 个结局就放弃，考虑减少结局数或添加"章节重玩提示"。(Owner: game-designer)

- **隐藏结局的"隐藏"程度**: 是否应在 UI 中提示隐藏结局的存在（如"??? "占位符），还是完全不在 UI 中提及？前者激励探索但降低惊喜感；后者保持神秘但玩家可能永远不知道隐藏结局存在。建议 MVP 做 A/B 测试。(Owner: ux-designer, game-designer)
