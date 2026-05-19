# 网状关联引擎 (Web Association Engine)

> **Status**: Designed (pending review)
> **Author**: 用户 + game-designer + ai-programmer + systems-designer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 3 (关联的网络) — 核心实现——使记忆碎片通过情感共鸣连接成网

## Overview

网状关联引擎是《回响》中"关联的网络"的计算心脏。当玩家站在一个记忆碎片中——一幅画面、一段情绪、一圈未干的墨迹——这个系统在背后回答一个问题：**接下来，哪些记忆与此刻相关？** 它不是按时间顺序翻页（先童年后青年），而是按情感的共鸣和叙事的张力来编织路径——一封信的气味引出另一封信，一种遗憾的重量引出另一种遗憾。

在技术层面，它是一个多因子关联评分引擎：对当前碎片，收集所有候选目标碎片，通过标签相似度、显式关联权重、情感类别节奏约束三个维度计算综合关联分数，按分数排序后输出推荐列表。场景管理系统 (#6) 将这个列表呈现为玩家可以选择的下一个目的地——但关联引擎不渲染 UI，不执行过渡——它只计算**"什么与什么相连，以及相连的引力有多大"**。

## Player Fantasy

你不是在看一张地图——你是在**闻着记忆的气味走路**。

每一幅画卷都有它自己的气味：雨后泥土、旧信纸上的霉、栀子花香。情感标签系统为你标记了这些气味——但你不需要"读"标签。你只需要闭上眼睛，让那些闻起来相似的东西牵引你。网状关联引擎是你的鼻子：它嗅过千百片记忆，从中找到与你此刻最相关的那些，将它们推到你的面前——有些浓烈，有些若有若无，有些藏在某种条件背后，只在某条路径上才能闻到。

你睁开眼睛，选择一缕香气——然后跟着它走进下一幅画。这香气不按时间顺序排列，不按章节编号索引。它沿着记忆的纹理、沿着情感纤维的毛细通路向前洇散。你跟着它，不是因为你"知道"这条路通向哪里——而是因为这条路**闻起来是对的**。

有时候，最动人的不是那条最浓烈的嗅觉线索，而是那条最淡的——你说不清它让你想起了什么，但你的手已经伸向了它。

## Detailed Design

### Core Rules

**规则 1 — 架构：纯 C# 关联引擎**

```
WebAssociationEngine (纯 C# 类，非 MonoBehaviour)
│
├── 构造函数注入:
│   ├── TagSimilarityMatrix _matrix (ScriptableObject, 预计算)
│   ├── EmotionalTagSystem _tagSystem (#10)
│   ├── ChangeTracker _changeTracker (#12)
│   └── MemoryFragmentDataModel _dataModel (#8)
│
├── 公开方法:
│   └── List<AssociationCandidate> ComputeAssociations(
│           string currentFragmentId,
│           string chapterKey,
│           List<string> recentHistory,      // 最近 K 个碎片 ID
│           HashSet<string> visitedFragmentIds
│       )
│
└── 内部状态: 无。每次调用为纯函数——相同输入产生相同输出。
```

- 不是 MonoBehaviour——不依赖 Unity 场景。可在单元测试中独立实例化。
- 依赖通过构造函数注入——调用方（SceneManager #6）负责传入。
- ComputeAssociations 是唯一公开方法——输入当前碎片 + 上下文，输出排序后的关联候选列表。

**规则 2 — 候选池：同章节碎片**

候选池 = 当前章节内所有已解锁的记忆碎片，排除：
- 当前碎片自身（不关联到自己）
- DefaultState = Locked 的碎片（章节管理中未解锁）
- 被 ConditionGroup 判定为不可达的碎片（变化追踪 #12 中 allConditionsMet = false）

对于 MVP，候选池仅限当前章节（章节内通常有 8-15 个碎片）。跨章节关联留给 Vertical Slice。

**规则 3 — 因子 A：余弦标签相似度**

```
A = CosineSimilarity(currentFragment.EmotionalTags, candidate.EmotionalTags)

其中 CosineSimilarity 使用 TagSimilarityMatrix 获取标签对之间的相似度权重：
  CosineSimilarity(T1, T2) = 
    sum_over_i,j( T1[i].weight × T2[j].weight × Matrix[i][j] )
    /
    ( ||T1|| × ||T2|| )

  其中 ||T|| = sqrt( sum_over_i(T[i].weight²) )
```

- TagSimilarityMatrix[i][j] ∈ [0.0, 1.0]：标签 i 与标签 j 之间的语义相似度
- 矩阵默认规则（可被设计师逐对覆盖）:
  - 相同标签: 1.0
  - 同一父标签下: 0.6
  - 同一情感类别: 0.4
  - 无关标签: 0.0
- 结果 A ∈ [0.0, 1.0]
- 因子 A 权重: 0.6

**规则 4 — 因子 B：显式关联权重**

每个碎片在数据模型中有一组 ExplicitAssociation 条目：

```
ExplicitAssociation:
  TargetFragmentId: string       # 关联目标碎片
  Weight: float [0.0, 1.0]       # 关联强度
  Direction: OneWay | Bidirectional
  Condition: ConditionGroup?     # 可选——只有条件满足时此关联才生效
```

```
B = 该候选碎片在当前碎片 ExplicitAssociation 中的 Weight 值
   + 双向加成: 若候选碎片的 ExplicitAssociation 中也有指向当前碎片的条目
     → B += 0.15 (max 1.0)
   
   若候选碎片不在当前碎片的 ExplicitAssociation 中:
   B = 0.0 (无显式关联，仅靠标签相似度)
```

特殊值: B = -1.0 → "设计师排除"——该候选从结果中移除。用于叙事上"不应该从这里到达那里"的情况。

因子 B 权重: 0.4

**规则 5 — 因子 C：情感节奏控制（Rhythm Penalty）**

滑动窗口 K=4——查看最近的 4 个已访问碎片的情感标注，对候选碎片的情绪类别施加软惩罚：

```
C = 1.0
FOR EACH recent fragment F in window (最近 4 个):
  IF F 的主导情感类别 == 候选的主导情感类别:
    C *= penaltyFactor

penaltyFactor 取值 (按重复次数递增):
  第 1 次重复 (最近第 1 个同类别):  ×0.70
  第 2 次重复 (最近第 2 个同类别):  ×0.55
  第 3 次重复 (最近第 3 个同类别):  ×0.40
  第 4 次重复 (最近第 4 个同类别):  ×0.25

C 下限: 0.1 (确保即使 4 次重复也有最低可达性)
C 上限: 1.3 (Peace 类别作为"调色板清洁剂": ×1.3)
```

- 惩罚作用于情感**类别**层面（6 类: Joy, Sadness, Anger, Fear, Peace, Longing），非标签层面
- Peace 类别特殊规则: 始终 ×1.05–1.30——作为情绪调色板清洁剂，自然穿插在其他情绪之间
- 候选池 ≤ 5 时（章节碎片少）: penaltyFactor 自动减半（×0.85 代替 ×0.70 等），确保有足够候选

**规则 6 — 因子 D：发现偏向（Discovery Boost）**

```
IF candidate 未被访问过 (candidate.Id ∉ visitedFragmentIds):
  D = 1.30   # +30% 探索推动

ELSE 已访问过:
  D = max(0.30, 1.0 - (visitCount × 0.30))
  # 第 1 次重访: D = 0.70
  # 第 2 次重访: D = 0.40
  # 第 3 次重访: D = 0.30 (floor)
```

重访激励 (Revisit Incentive):
IF candidate 已访问 AND ChangeTracker 报告该碎片有 pending content changes (新选择选项或变化的内容):
  D = max(D, 0.70)  # 即使已访问，有内容变化时提升至 0.70

**规则 7 — 综合关联分数**

```
tagScore      = A × 0.6
explicitScore = B × 0.4   (若 B = -1.0 → 候选排除)
compositeScore = (tagScore + explicitScore) × C × D
```

分数范围: [≈0.0, 1.30]。A 和 B 各贡献最多 0.6 和 0.4 → 和最多 1.0。C 调节 [0.1, 1.3]，D 调节 [0.30, 1.30]。最大可能分数：1.0 × 1.3 × 1.3 = 1.69（极少见——需要 Peace 类别 × 未访问）。

排除规则（不进入排序）:
- B = -1.0（设计师排除）
- compositeScore < 0.05（关联过弱——"没有感觉"）

**规则 8 — 候选排名与强度分级**

```
Top-5 候选按 compositeScore 降序排列
最少返回 3 个——候选池不足时，即使分数低于 0.05 也保留
(但 B = -1.0 的始终排除)

Strength 分级 (基于 compositeScore):
  Strong  (≥0.60): 強い牵绊——推荐展示为"浓烈的香气"
  Medium  (≥0.30): 中等关联——"若有若无的气味"
  Faint   (≥0.10): 弱い关联——"淡い痕跡"
  Trace   (<0.10): 极弱——"几乎闻不到，但某处有一样东西..."

每个候选附带 DominantFactor: TagSimilarity | ExplicitAssociation | RhythmBoost | DiscoveryBoost
→ 标记对该候选分数贡献最大的因子，供 UI/HUD 展示关联类型
```

**规则 9 — 冷启动处理**

章节入口碎片（玩家刚进入章节，history 为空）:
- recentHistory 为空 → 所有 rhythm penalties = 1.0（无重复情绪可惩罚）
- visitedFragmentIds 为空 → 所有候选获得 ×1.30 发现推动
- 此时综合排序完全由标签相似度 (A) 和显式关联 (B) 决定
- 章节入口碎片的 ExplicitAssociations 由设计师精心配置——作为"章节开局推荐路径"

**规则 10 — 状态变化触发重新计算**

当以下事件发生，调用方（SceneManager #6）重新调用 ComputeAssociations:
- 玩家做出选择 → ContentChanges 应用 → 候选碎片的标签权重可能改变 (ModifyTagWeight)
- 玩家进入新碎片 → history 和 visited 更新
- ConditionGroup 状态变化 → 某些 ExplicitAssociation 的 Condition 从 false 变 true（或反）

关联引擎自身不监听事件——它是纯计算函数。SceneManager 持有引擎实例，在合适时机调用。

**规则 11 — TagSimilarityMatrix 预计算**

```
TagSimilarityMatrix (ScriptableObject, 位于 assets/data/)

  维度: N×N (N = 情感标签词汇表大小, 当前约 20 个标签)
  存储: float[标签数, 标签数] ——扁平化为二维数组
  生成: Editor 工具在标签词汇表更新时自动计算默认值
  覆盖: 设计师可在 Inspector 中修改特定标签对之间的值

  预计算默认值规则:
    FOR each pair (tag_i, tag_j):
      IF tag_i == tag_j → 1.0
      ELSE IF parentTag[tag_i] == parentTag[tag_j] → 0.6
      ELSE IF category[tag_i] == category[tag_j] → 0.4
      ELSE → 0.0
```

矩阵在 Editor 时预计算——运行时只读取，不动态构建。标签词汇表更新时（情感标签系统 #10），Editor 工具自动重新生成默认矩阵。

### States and Transitions

关联引擎自身无状态——它是纯函数计算。以下状态描述调用方（SceneManager）使用引擎的流程：

| 状态 | 描述 | 触发 |
|------|------|------|
| **Idle** | 无活跃关联计算。玩家不在任何碎片中 | Game 场景加载完成 / 章节过渡中 |
| **Computing** | 正在计算关联候选。同步调用，耗时 <1ms | 玩家进入碎片 |
| **Presenting** | 候选列表已计算，展示给玩家 | ComputeAssociations 返回 |
| **Updating** | 玩家选择导致状态变化，重新计算中 | ContentChanges 应用后 |

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **情感标签系统 (#10)** | 标签词汇表、TagCategory 枚举、标签分配查询 | 提供标签相似度计算的词汇表 |
| 上游 | **记忆碎片数据模型 (#8)** | MemoryFragment.EmotionalTags[], MemoryFragment.ExplicitAssociations[] | 每个碎片的标签权重和显式关联定义 |
| 上游 | **记忆变化追踪 (#12)** | ConditionGroup 评估、Flag 查询、ModifyTagWeight 状态 | 提供当前变化状态下的有效标签权重和关联条件 |
| 下游 | **场景管理系统 (#6)** | ComputeAssociations() → AssociationCandidate[] | 调用引擎并将结果传递给 HUD 展示 |
| 下游 | **游戏内HUD (#17)** | AssociationCandidate[] → 关联路径可视化 | HUD 将候选列表渲染为可选择的下一目的地 |
| 下游 | **多结局系统 (#14)** | 关联路径历史 → 结局判定输入 | 玩家穿过的关联路径类型作为结局因子之一 |
| 横切 | **存档系统 (#7)** | visitedFragmentIds, recentHistory → 存档/读档 | 存档时保存访问历史和路径，读档后恢复计算状态 |
| 横切 | **UI框架 (#5)** | 关联强度 → UI 颜色/字体映射 | Strong/Medium/Faint/Trace 的视觉区分 |

## Formulas

### 1. 余弦标签相似度 (Cosine Tag Similarity) — 因子 A

```
A(current, candidate) = 
    Σᵢ Σⱼ ( w_current[i] × w_candidate[j] × M[i][j] )
    /
    ( ||w_current|| × ||w_candidate|| )

其中:
  w_current[i]     = 当前碎片中标签 i 的权重, ∈ [0.0, 1.0]
  w_candidate[j]   = 候选碎片中标签 j 的权重, ∈ [0.0, 1.0]
  M[i][j]          = TagSimilarityMatrix 中标签对 (i,j) 的相似度, ∈ [0.0, 1.0]
  ||w||            = sqrt( Σᵢ w[i]² )  —— 权重向量的 L2 范数

结果: A ∈ [0.0, 1.0]
```

**示例计算:**

当前碎片标签: { Nostalgia:0.9, Rain:0.7, Loneliness:0.5 }
候选碎片标签: { Rain:0.8, Solitude:0.6 }

```
M[Rain][Rain]          = 1.0  (相同标签)
M[Rain][Solitude]      = 0.4  (同情感类别 Sadness)
M[Nostalgia][Rain]     = 0.0  (无关)
M[Nostalgia][Solitude] = 0.4  (同类别)
M[Loneliness][Rain]    = 0.4  (同类别)
M[Loneliness][Solitude] = 0.6 (同父标签)

分子 = 0.9×0.8×0.0 + 0.9×0.6×0.4 + 0.7×0.8×1.0 + 0.7×0.6×0.4 + 0.5×0.8×0.4 + 0.5×0.6×0.6
    = 0 + 0.216 + 0.56 + 0.168 + 0.16 + 0.18
    = 1.284

||w_current||  = sqrt(0.81 + 0.49 + 0.25) = sqrt(1.55) = 1.245
||w_candidate|| = sqrt(0.64 + 0.36) = sqrt(1.00) = 1.000

A = 1.284 / (1.245 × 1.000) = 1.284 / 1.245 = 1.031 → clamp → 1.000
```

### 2. 显式关联分数 (Explicit Association Score) — 因子 B

```
B(current, candidate) =
  IF candidate 在 current.ExplicitAssociations 中 (Weight = w):
    base = w   # w ∈ [0.0, 1.0]
    IF current 在 candidate.ExplicitAssociations 中 (双向):
      base = min(base + 0.15, 1.0)
    B = base
  ELSE:
    B = 0.0

特殊值:
  IF w == -1.0 (设计师标记):
    candidate 从排序中移除 (不计算 compositeScore)
```

### 3. 情感节奏惩罚 (Rhythm Penalty) — 因子 C

```
C = 1.0
FOR EACH fragment F in recentHistory (最多 K=4 个):
  category_F = DominantCategory(F.EmotionalTags)
  category_candidate = DominantCategory(candidate.EmotionalTags)
  
  IF category_F == category_candidate:
    C = C × penaltyForPosition(position_of_F_in_window)

penaltyForPosition (从最近到最远):
  pos=1 (最近): 0.70
  pos=2:        0.55
  pos=3:        0.40
  pos=4:        0.25

候选池 ≤ 5 时的自适应减半:
  penaltyForPosition_adaptive = 1.0 - (1.0 - penaltyForPosition) × 0.5

Peace 类别加成:
  IF DominantCategory(candidate) == Peace:
    C = C × 1.30  (与惩罚叠加)

最终 C 钳制: C ∈ [0.10, 1.30]
```

**示例:**

recentHistory = [碎片A(Sadness), 碎片B(Joy), 碎片C(Sadness), 碎片D(Fear)]
候选主导类别 = Sadness

```
C = 1.0
pos=1 碎片D: Fear ≠ Sadness → 无惩罚
pos=2 碎片C: Sadness == Sadness → C = 1.0 × 0.70 = 0.70
pos=3 碎片B: Joy ≠ Sadness → 无惩罚
pos=4 碎片A: Sadness == Sadness → C = 0.70 × 0.40 = 0.28

结果: C = 0.28 (两个 Sadness 碎片在窗口内 → 中等惩罚)
```

### 4. 发现偏向 (Discovery Boost) — 因子 D

```
IF candidate.Id ∉ visitedFragmentIds:
  D = 1.30   # 未访问 — 探索推动
ELSE:
  visitCount = ChangeTracker.GetVisitCount(candidate.Id)
  D = max(0.30, 1.0 - (visitCount × 0.30))

重访激励:
  IF candidate.Id ∈ visitedFragmentIds 
     AND ChangeTracker.HasPendingChanges(candidate.Id):
    D = max(D, 0.70)

D ∈ [0.30, 1.30]
```

| visitCount | D (无 pending changes) | D (有 pending changes) |
|------------|----------------------|------------------------|
| 0 (未访问) | 1.30 | 1.30 |
| 1 | 0.70 | 0.70 (already ≥0.70) |
| 2 | 0.40 | 0.70 |
| 3+ | 0.30 | 0.70 |

### 5. 综合关联分数 (Composite Score)

```
tagScore      = A × 0.6
explicitScore = B × 0.4

compositeScore = (tagScore + explicitScore) × C × D

排除规则:
  - B == -1.0 → 候选排除，不计算
  - compositeScore < 0.05 → 候选排除（候选池 < 3 时此规则放宽——保留至少 3 个候选）
```

范围: `compositeScore ∈ [≈0.0, 1.69]`

理论最大值路径: A=1.0, B=1.0 → tagScore+explicitScore=1.0, C=1.3 (Peace), D=1.3 (unvisited) → 1.0×1.3×1.3 = 1.69

### 6. 强度分级阈值

| Strength | compositeScore 范围 | 叙事隐喻 |
|----------|-------------------|---------|
| Strong | ≥ 0.60 | 浓烈的香气 — 这条路径在呼唤你 |
| Medium | ≥ 0.30 | 若有若无的气味 — 你能闻到，但不确定方向 |
| Faint | ≥ 0.10 | 淡い痕跡 — 几乎注意不到，但它在 |
| Trace | < 0.10 | 与えられた沈黙 — 只有闭上眼睛才能察觉 |

### 7. 主导因子判定 (DominantFactor)

```
contributions:
  tagContrib      = A × 0.6
  explicitContrib = B × 0.4
  rhythmContrib   = (tagScore + explicitScore) × (C - 1.0)   # C 偏离 1.0 的部分
  discoveryContrib = (tagScore + explicitScore) × C × (D - 1.0)  # D 偏离 1.0 的部分

DominantFactor = argmax { tagContrib, explicitContrib, |rhythmContrib|, |discoveryContrib| }
  → TagSimilarity | ExplicitAssociation | RhythmBoost | DiscoveryBoost
```

## Edge Cases

- **章节内所有碎片主导情感相同 (All-Same-Category Chapter)**: 若整个章节的碎片都具有相同主导情感类别（如全为 Sadness），rhythm penalty 将在连续访问中累积至最大值——第 4 步后 C = 0.25 × 0.40 × 0.55 × 0.70 ≈ 0.04，随后钳制至 0.10 下限。结果：所有候选分数被压缩至极窄区间，区分度降低。此时显式关联 (B) 和发现偏向 (D) 成为主要区分因子。**建议设计师为至少 50% 的碎片添加 ExplicitAssociation 条目**，为纯 Sadness 章节提供导航结构。

- **碎片无标签 (Empty Tag List)**: 若 currentFragment.EmotionalTags 为空，A = 0（无标签向量可比较）。综合分数完全由 B（显式关联）驱动。这是设计上的合法状态——"无标签"碎片只能通过设计师指定的显式关联到达和离开。若候选碎片也无标签，A = 0（0/0 → 定义为 0）。

- **微小章节 (Tiny Chapter, ≤3 候选)**: 候选池 ≤ 5 时 rhythm penalty 自适应减半。候选池 ≤ 3 时，0.05 排除阈值放宽——所有未被 B=-1.0 排除的候选均保留并返回。此时不适用"最少 3 个候选"规则（候选不足 3 个时返回实际数量）。

- **冷启动 (Cold Start — 章节入口)**: recentHistory 为空 → rhythm penalty 对第一个碎片的候选计算无影响（C = 1.0 对所有候选）。visitedFragmentIds 为空 → 所有候选 D = 1.30。**章节入口碎片必须由设计师配置 ExplicitAssociation**，否则排序仅靠标签相似度——而入口碎片可能全部候选的 A 值接近（标签相似），导致排序呈现"平局"。

- **TagSimilarityMatrix 未配置**: 若矩阵未生成（Editor 遗漏），默认所有 M[i][j] = (i==j ? 1.0 : 0.0)。这退化为"仅相同标签有相似度"。引擎在 Awake 时检查矩阵维度是否匹配当前标签词汇表大小——不匹配时记录警告并退化到默认规则。

- **所有候选分数 < 0.05**: 在正常情况下不常见（因 D=1.30 推动未访问候选）。但在极端情况下——所有候选已访问 3+ 次 (D=0.30) + rhythm penalty 极低 (C=0.10) + 无显式关联 (B=0) + 标签相似度低 (A=0.1) ——可能所有候选 < 0.05。此情况下返回 Top-3（按原始分数排序），Strength 全部标记为 Trace。UI 应显示"极淡的痕迹"而非空列表。

- **ConditionGroup 永远不可满足**: 若 ExplicitAssociation 附带一个永远为 false 的 ConditionGroup（逻辑错误——如 `{ All: [Flag("X"), Flag("NOT_X")] }`），该关联在运行时永远不生效。引擎在 ComputeAssociations 中跳过条件未满足的 ExplicitAssociation。**建议 Editor 工具检测不可满足的 ConditionGroup 并警告设计师。**

- **排除后候选池为空**: 若所有候选被 B=-1.0 排除或 ConditionGroup 不可达，返回空列表。SceneManager 应当处理此情况——展示"无路可走"的叙事状态（如画面暗下来，游魂悬停），并自动触发展示章节结束过渡或解锁新碎片。

- **大标签集 (20+ tags on a single fragment)**: 余弦相似度计算复杂度 O(N²)，N 为标签词汇表总大小（不是碎片标签数）。理论上用全矩阵计算——矩阵已预计算。20 个标签 → 20×20 = 400 对，毫秒内完成。但设计上限建议每个碎片 ≤ 8 个标签——过多标签会稀释向量方向性，导致所有候选的 A 值趋近均匀。

- **重访次数上限 (visitCount Overflow)**: visitCount 在 D 计算中的影响已通过 floor(0.30) 钳制。第 3 次及以后的访问 D 恒为 0.30（无 pending changes 时）。无需额外上限。

- **运行时标签权重变化 (Tag Weight Changes Mid-Session)**: ChangeTracker (#12) 的 ModifyTagWeight 可改变碎片标签权重。ChangeTracker 的 ApplyChanges 触发后，SceneManager 应调用 ComputeAssociations 重新计算。引擎自身不监听变化——它是纯函数。详见规则 10。

- **双向关联形成强连接簇**: 若碎片 A 和 B 互相设置双向 ExplicitAssociation (Weight=0.9)，双方从对方获得 B=1.0（0.9 + 0.15 双向加成）。再加上高标签相似度，A↔B 的 compositeScore 可能远超其他候选。这并非 bug——这是设计师意图的"强连接"——但可能导致玩家在 A 和 B 之间来回跳转而忽略其他路径。**节奏惩罚 (C) 作为自然抑制**：在 A→B 后返回 A 时，若 A 主导类别与 B 相同，C 将惩罚重复类别。

## Dependencies

### 硬依赖 (Hard Dependencies)

| 系统 | 性质 | 接口 |
|------|------|------|
| **情感标签系统 (#10)** | 硬依赖 — 无此系统则 A 因子不可计算 | 标签词汇表 (List\<EmotionalTag\>)、GetTagCategory(tagId)、TagSimilarityMatrix (ScriptableObject) |
| **记忆碎片数据模型 (#8)** | 硬依赖 — 无此系统则候选池不可建立 | MemoryFragment.EmotionalTags[], MemoryFragment.ExplicitAssociations[], MemoryFragment.ChapterKey |
| **记忆变化追踪 (#12)** | 硬依赖 — 无此系统则 ConditionGroup 和发现偏向不可计算 | EvaluateConditionGroup(ConditionGroup): bool, GetFlagValue(flagId): bool, GetVisitCount(fragmentId): int, HasPendingChanges(fragmentId): bool, GetEffectiveTagWeights(fragmentId): TagWeight[] |

### 软依赖 (Soft Dependencies)

| 系统 | 性质 | 接口 |
|------|------|------|
| **场景管理系统 (#6)** | 软依赖 — 调用方。关联引擎自身不依赖场景管理运行，但 SceneManager 是引擎的实例持有者和调用方 | 持有 WebAssociationEngine 实例，在碎片切换和状态变化时调用 ComputeAssociations() |
| **游戏内HUD (#17)** | 软依赖 — 展示方。引擎不依赖 HUD 运行，但 HUD 是关联结果的消费方 | 接收 AssociationCandidate[] 并渲染为可选择的路径可视化 |
| **多结局系统 (#14)** | 软依赖 — 数据消费方。引擎不依赖结局系统运行，但结局系统消费关联路径历史 | 接收 selectedAssociationPath 序列作为结局判定的输入之一 |
| **存档系统 (#7)** | 软依赖 — 状态持久化。引擎自身无状态，但调用方 (SceneManager) 需要持久化 recentHistory 和 visitedFragmentIds | visitedFragmentIds, recentHistory 需序列化到存档 |

### 下游系统 (Depended On By)

| 系统 | 消费内容 |
|------|---------|
| **场景管理 (#6)** | 持有引擎实例，调用 ComputeAssociations() |
| **多结局系统 (#14)** | 关联路径历史 → 结局因子 |
| **章节管理 (#15)** | 章节入口碎片的冷启动配置 (ExplicitAssociation) |
| **游戏内HUD (#17)** | AssociationCandidate[] → 渲染路径选择 UI |

## Tuning Knobs

### 核心权重 (Core Weights)

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 标签相似度权重 (A weight) | 0.6 | 0.3–0.8 | 调高 → 情感纹理驱动；调低 → 设计师显式引导驱动 |
| 显式关联权重 (B weight) | 0.4 | 0.2–0.7 | 与 A weight 互补——两者之和应接近 1.0 |

### 情感节奏 (Rhythm Control)

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 滑动窗口大小 K | 4 | 2–6 | 越大 → 情绪惩罚记忆越长，越难连续同情绪；越小 → 情绪切换更自由 |
| 最近位惩罚 (pos=1) | 0.70 | 0.50–0.85 | 刚看过的同情绪类别的惩罚 |
| 第2位惩罚 (pos=2) | 0.55 | 0.40–0.70 | |
| 第3位惩罚 (pos=3) | 0.40 | 0.30–0.55 | |
| 第4位惩罚 (pos=4) | 0.25 | 0.15–0.40 | |
| C 下限 | 0.10 | 0.05–0.20 | 确保即使全窗口同类别也有最低可达路径 |
| C 上限 | 1.30 | 1.10–1.50 | Peace 类别的最大加成 |
| Peace 类别加成 | 1.30 | 1.05–1.50 | "调色板清洁剂"的强度 |
| 自适应减半系数 | 0.5 | 0.3–0.7 | 候选池 ≤5 时 penalty 减半程度 |

### 发现偏向 (Discovery Bias)

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 未访问推动 | 1.30 | 1.10–1.50 | 未访问碎片的新鲜感加成 |
| 每访问衰减 | 0.30 | 0.15–0.40 | 每次重访的分数衰减量 |
| 衰减下限 | 0.30 | 0.10–0.40 | 重访分数的最低锚点 |
| 重访激励阈值 | 0.70 | 0.50–0.85 | 有 pending changes 时 D 的最低值 |

### 显式关联 (Explicit Associations)

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 双向加成 | 0.15 | 0.05–0.25 | 互相指向的关联额外加分 |
| 设计师排除值 | -1.0 | 固定 | 不要改——用此 magic number 排除候选 |

### 排序与展示 (Ranking & Display)

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 返回候选数 | 5 | 3–8 | Top-N 候选，多于 5 个会压垮选择界面 |
| 最少候选数 | 3 | 2–4 | 候选池不足时放宽排除阈值 |
| 排除阈值 | 0.05 | 0.02–0.10 | compositeScore 低于此值的候选被切除 |
| Strong 阈值 | 0.60 | 0.50–0.70 | "浓烈香气"的下界 |
| Medium 阈值 | 0.30 | 0.25–0.40 | "若有若无"的下界 |
| Faint 阈值 | 0.10 | 0.05–0.15 | "淡い痕跡"的下界 |

### TagSimilarityMatrix 默认值

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 相同标签 | 1.0 | 固定 | 同一标签的相似度不应改变 |
| 同一父标签 | 0.6 | 0.4–0.8 | 兄弟姐妹标签的相似度 |
| 同一情感类别 | 0.4 | 0.2–0.6 | 同类别但不同父标签 |
| 无关标签 | 0.0 | 固定 | 不同类别的标签相似度为 0 |
| 矩阵维度 | 20×20 | 随标签词汇表 | 由情感标签系统 (#10) 的词汇表大小决定 |

## Visual/Audio Requirements

本系统是纯 C# 计算类——自身无视觉或音频输出。所有呈现委托给下游系统：

- **TagSimilarityMatrix Editor 可视化**: Unity Inspector 中矩阵以 heatmap 网格展示（行=标签 i, 列=标签 j, 颜色从白 0.0 → 朱砂红 1.0）。可点击单元格覆盖默认值。
- **候选列表的叙事呈现**: HUD (#17) 使用 Strength 分级的隐喻语言（"浓烈的香气"等）——不展示原始分数。
- **无音频**: 关联计算不产生声音。候选展示的音效由交互反馈系统 (#18) 管理。

## UI Requirements

纯计算系统——无自有 UI。所有 UI 由下游系统提供：

- **关联路径选择 UI**: 游戏内HUD (#17) — 以"气味/墨迹拖痕"为隐喻，将 Top-5 候选渲染为可选择的目的地标记
- **Strength 视觉映射**: UI 框架 (#5) — Strong/Medium/Faint/Trace 映射到不同的墨色浓淡和朱砂墨点大小
- **DominantFactor 展示**: HUD 可选择性展示主导因子（如"标签共鸣"或"叙事线索"）——非必须

## Acceptance Criteria

- **GIVEN** 当前碎片在章节 Ch01 中有 10 个候选碎片（均有标签权重），**WHEN** SceneManager 调用 ComputeAssociations(currentFragmentId, "Ch01", recentHistory, visitedIds)，**THEN** 返回包含 5 个 AssociationCandidate 的列表，按 compositeScore 降序排列。第 1 个候选的 compositeScore ≥ 第 5 个。

- **GIVEN** 当前碎片的 EmotionalTags = {Nostalgia:0.9, Rain:0.7}，候选 A 的标签 = {Rain:0.8, Solitude:0.6}，候选 B 的标签 = {Joy:1.0}，且无显式关联，**WHEN** 计算 A 因子，**THEN** A(current, 候选A) > A(current, 候选B) —— Rain 标签相似度高于 Joy（无关标签）。

- **GIVEN** 当前碎片的 ExplicitAssociation 中候选 X 的 Weight = -1.0，**WHEN** ComputeAssociations 执行，**THEN** 候选 X 不出现在返回列表中——即使其标签相似度很高。

- **GIVEN** recentHistory = [碎片A(Sadness), 碎片B(Sadness), 碎片C(Sadness)]，候选的主导类别 = Sadness，**WHEN** 计算 C 因子，**THEN** C < 1.0（有惩罚）。具体值: 最近位(pos=1 碎片C) ×0.70, 第2位(pos=2 碎片B) ×0.55 → C = 0.70 × 0.55 = 0.385。

- **GIVEN** 候选 X 在 visitedFragmentIds 中（已访问 1 次，无 pending changes），候选 Y 不在 visitedFragmentIds 中，且 A、B、C 因子相同，**WHEN** 计算 D 因子，**THEN** D(Y) = 1.30, D(X) = 0.70。Y 的 compositeScore > X。

- **GIVEN** recentHistory 为空（冷启动），visitedFragmentIds 为空，**WHEN** ComputeAssociations 执行，**THEN** 所有候选的 C = 1.0, D = 1.30。仅 A 和 B 产生区分。

- **GIVEN** 当前碎片有 ExplicitAssociation 指向候选 Z (Weight=0.8, Direction=Bidirectional)，且候选 Z 也有 ExplicitAssociation 指向当前碎片 (Weight=0.9)，**WHEN** 计算 B 因子，**THEN** B = min(0.8 + 0.15, 1.0) = 0.95。

- **GIVEN** 某个候选的 compositeScore = 0.72，**WHEN** 确定 Strength 分级，**THEN** Strength = Strong (≥0.60)。DominantFactor 标记为对 (tagScore+explicitScore)×C×D 贡献最大的单项因子。

- **GIVEN** 当前碎片的 EmotionalTags 为空，**WHEN** ComputeAssociations 执行，**THEN** A = 0 对所有候选。排序仅由 B（显式关联）和 C×D 驱动。不抛异常。

- **GIVEN** 章节仅有 2 个候选碎片（微小章节），且两者的 compositeScore 都低于 0.05，**WHEN** ComputeAssociations 执行，**THEN** 返回 2 个候选（最低候选数放宽——返回实际可用数量），Strength 标记为 Trace。

- **GIVEN** 玩家在碎片 A 做出选择（触发 ModifyTagWeight——将候选 B 的 Nostalgia 从 0.5 变为 0.9），**WHEN** SceneManager 在 ApplyChanges 后重新调用 ComputeAssociations，**THEN** A(A, B) 的新值能够反映变化后的标签权重（Nostalgia:0.9 替代 0.5）。

## Open Questions

- **跨章节关联 (Cross-Chapter Associations)**: MVP 限定候选池为当前章节。跨章节关联（如 Chapter 1 的碎片连接到 Chapter 3 的碎片）需要章节解锁状态追踪 (#16) 配合。推迟到 Vertical Slice 评估——MVP 先验证章内关联质量。（Owner: game-designer）

- **TagSimilarityMatrix 导入/导出**: Editor 工具是否支持 CSV 导入/导出？对于 20×20 矩阵，Inspector 手动调整可能繁琐。建议 MVP 阶段先用默认规则 + Inspector 逐对覆盖——如设计师反馈矩阵调整耗时过多，Tools Programmer 在 Vertical Slice 阶段添加 CSV 导入。（Owner: tools-programmer, Full Vision）

- **关联引擎性能基准**: 当前设计假设 8-15 候选碎片、20 标签、<1ms 计算。需要在 50+ 碎片章节的极端条件下做性能测试——虽然 MVP 章节规模小，但应在引擎实现后添加 benchmark 测试验证 O(N×T²) 复杂度在最大值下的表现。（Owner: gameplay-programmer）

- **玩家可见的分数/强度**: 是否应该向玩家展示关联强度的视觉差异（如 Strong 关联的墨迹更浓），还是所有候选视觉上看起来平等、让玩家凭直觉选择？前者增强透明度但降低神秘感；后者更符合"闻香寻路"的隐喻但不给玩家任何"攻略"。建议 MVP 阶段做 A/B 测试。（Owner: ux-designer, game-designer）
