# ADR-0009: 网状关联引擎 — 四因子加权算法与候选池构建

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

关联引擎为当前碎片推荐相关记忆碎片。决定使用四因子加权公式：`Score = (A × 0.6 + B × 0.4) × C × D` + 候选池过滤（同章、已解锁、条件满足、排除自身）+ 视觉分级（Strong/Medium/Faint/Trace）。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — 纯 C# 数学，无 Unity API 依赖 |
| **References Consulted** | `VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (IDataManager — 碎片数据查询), ADR-0008 (ConditionGroup — 候选池条件过滤), 情感标签系统 GDD #10 (标签数据与权重 — 无独立 ADR) |
| **Enables** | ADR-0017 (InGameHUD — 关联路径展示) |
| **Blocks** | WebAssociationEngine Epic |
| **Ordering Note** | 在 ADR-0002, ADR-0008 之后实现；情感标签数据需从 GDD #10 获取 |

## Context

### Problem Statement

回响 (Echoes) 的特色是揭示碎片之间的隐藏关联。每个碎片可以与其他碎片有关联——基于情感标签相似度、显式链接、节奏变化。关联引擎需要在当前碎片展示时实时计算推荐关联，展示在 HUD 上供玩家探索。

### Constraints

- 每碎片推荐 3-5 个关联（避免信息过载）
- 计算必须在 < 5ms 内完成（转场动画期间计算）
- 候选池 ≤ 当前章节碎片数（~20-40 个碎片）
- 算法必须是纯函数（无状态、可单元测试）

### Requirements

- 四因子加权公式
- 候选池筛选规则（同章、已解锁、条件满足、排除自身）
- 去重（同一碎片只出现一次）
- 分数排序（DESC）
- 视觉分级（Strong/Medium/Faint/Trace 4 档）

## Decision

**四因子加权公式 + 候选池过滤 + 分数排序 + 视觉分级。**

### 四因子加权公式

```
Score = (A × 0.6 + B × 0.4) × C × D

A = TagSimilarity(tags_current, tags_candidate)  [0.0-1.0]
    → 情感标签余弦相似度 (查询 TagSimilarityMatrix SO)

B = ExplicitWeight(candidate)                     [0.0-1.0]
    → 设计师配置的显式关联权重 (MemoryFragment.ExplicitAssociations[])

C = RhythmPenalty(recentHistory)                  [0.5-1.0]
    → 节奏惩罚：最近 4 个已访问碎片中的候选降低权重
    → C = 0.5 如果候选在 recentHistory[0..1] 中
    → C = 0.75 如果候选在 recentHistory[2..3] 中
    → C = 1.0 否则

D = DiscoveryBoost(candidate)                     [1.0-1.5]
    → 探索奖励：未访问过的候选获得权重加成
    → D = 1.5 如果候选未访问
    → D = 1.2 如果候选访问但未完整探索 (visited but choices remaining)
    → D = 1.0 如果候选已完整探索
```

### 候选池构建

```csharp
public List<AssociationCandidate> ComputeAssociations(
    string currentFragmentId,
    string chapterKey,
    List<string> recentHistory,      // K=4
    HashSet<string> visitedFragmentIds)
{
    // Step 1: 获取候选池
    var allFragments = DataManager.GetFragmentsByChapter(chapterKey);
    var candidates = allFragments
        .Where(f =>
            f.FragmentId != currentFragmentId &&        // 排除自身
            ChangeTracker.EvaluateCondition(f.UnlockCondition) &&  // 已解锁
            f.ChapterId == chapterKey)                  // 同章
        .ToList();

    // Step 2: 计算分数
    var scored = candidates.Select(c => new AssociationCandidate
    {
        FragmentId = c.FragmentId,
        Score = ComputeScore(currentFragmentId, c, recentHistory, visitedFragmentIds),
        Grade = DetermineGrade(Score)
    });

    // Step 3: 排序 + Top-N
    return scored
        .OrderByDescending(c => c.Score)
        .Take(5)
        .ToList();
}
```

### 视觉分级

| 等级 | 分数区间 | 视觉样式 | 含义 |
|------|---------|---------|------|
| **Strong** | ≥ 0.8 | 暖色 + 粗笔触 | 强烈暗示关联 |
| **Medium** | 0.5-0.79 | 暖色 + 细笔触 | 中等关联 |
| **Faint** | 0.25-0.49 | 冷色 + 墨迹淡 | 微弱线索 |
| **Trace** | < 0.25 | 冷色 + 墨迹痕 | 几乎不可见 |

### Architecture Diagram

```
┌─────────────────────────────────────────────────┐
│         WebAssociationEngine                     │
│        (纯 C# 类, 无状态, 可单元测试)            │
│                                                 │
│  ComputeAssociations(currentId, chapter,         │
│     recentHistory, visitedIds)                   │
│    │                                            │
│    ├─ 1. 候选池过滤                              │
│    │    ├─ 同章                                  │
│    │    ├─ 已解锁 (ConditionGroup)               │
│    │    └─ 排除自身                              │
│    │                                            │
│    ├─ 2. 四因子计算                              │
│    │    ├─ A = TagSimilarity(tags_c, tags_t)     │
│    │    │     → TagSimilarityMatrix SO (N×N)     │
│    │    ├─ B = ExplicitWeight (SO 字段)          │
│    │    ├─ C = RhythmPenalty (recentHistory)     │
│    │    └─ D = DiscoveryBoost (visitedIds)       │
│    │                                            │
│    ├─ 3. 得分排序 (DESC)                         │
│    │                                            │
│    └─ 4. 视觉分级 (Strong/Medium/Faint/Trace)     │
│                                                 │
│  → List<AssociationCandidate> (Top 5)           │
└─────────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public class WebAssociationEngine
{
    // 纯函数 — 无状态, 无副作用
    public List<AssociationCandidate> ComputeAssociations(
        string currentFragmentId,
        string chapterKey,
        List<string> recentHistory,        // max 4
        HashSet<string> visitedFragmentIds);

    // 依赖注入
    public WebAssociationEngine(
        IDataManager dataManager,
        IEmotionalTagSystem tagSystem,
        IChangeTracker changeTracker);
}

public struct AssociationCandidate
{
    public string FragmentId;
    public float Score;         // 0.0-1.5
    public string Grade;        // Strong/Medium/Faint/Trace
    public float TagSimilarity;
    public float ExplicitWeight;
    public float RhythmPenalty;
    public float DiscoveryBoost;
}
```

### Implementation Guidelines

1. `ComputeAssociations` 是纯函数 — 相同输入 → 相同输出
2. TagSimilarityMatrix 预计算 N×N 矩阵存储在 ScriptableObject 中（避免运行时重复计算）
3. 候选池在 20-40 个碎片规模下性能 < 5ms
4. 分数缓存策略：同一碎片 ID + 同一 recentHistory = 同一结果，可缓存

## Alternatives Considered

### Alternative 1: 仅基于显式关联（设计师手动链接）

- **Description**: 所有关联由设计师在 ScriptableObject 中手动创建，运行时不做计算
- **Pros**: 实现简单（只是直接读取 SO 字段）
- **Cons**: 无法响应玩家行为变化；标签系统失去作用；随着碎片数量增长手动链接不可维护
- **Rejection Reason**: 动态关联是回响 (Echoes) 的核心 game feel，纯手动链接无法实现"玩家选择后关联变化"

### Alternative 2: 使用机器学习推荐模型

- **Description**: 训练嵌入模型，根据玩家行为向量预测关联偏好
- **Pros**: 个性化程度高
- **Cons**: 需要训练数据和 ML 基础设施；需要 on-device 推理；对于 20-40 碎片规模严重过度设计
- **Rejection Reason**: 过度设计 — 加权公式在 20-40 碎片规模下足够灵活

## Consequences

### Positive

- 纯函数（完全可单元测试 — 核心卖点）
- 响应玩家行为（历史记录影响节奏惩罚、访问记录影响探索奖励）
- 设计师可调节（TagSimilarityMatrix + ExplicitWeight 提供创作自由度）
- 视觉分级提供清晰的 UI 信息层级

### Negative

- 四因子之间的权重（0.6/0.4）可能需要长时间的 playtest 调优
- TagSimilarityMatrix 需要预计算和 Editor 工具
- 5 个候选可能不够或太多（需要实际体验验证）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 候选池大小导致性能超预算 | Low | Low | 20-40 碎片规模 < 5ms, profile 验证 |
| 因子权重需大量调优 | Medium | Medium | Tuning Knobs 暴露所有权重参数（GDD Tuning Knobs 节） |
| 冷启动（新游戏无历史） | Low | Low | recentHistory 为空时 C=1.0, visitedIds 为空时 D=1.5 (全探索奖励) |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (40 碎片候选池, 全计算) | < 5ms |
| CPU (缓存命中) | < 0.1ms |
| Memory (TagSimilarityMatrix 20×20) | ~4KB (float matrix) |
| Memory (AssociationCandidate × 5) | ~200B |
| GC Allocation | ~500B (LINQ + struct copy) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] 相同输入产生相同输出（纯函数验证）
- [ ] Score 范围在 [0.0, 1.5] 之间
- [ ] RhythmPenalty C 对最近访问碎片降低分数
- [ ] DiscoveryBoost D 对未访问碎片提升分数
- [ ] Top 5 无自身碎片
- [ ] 候选池只包含条件满足（已解锁）的碎片

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `web-association-engine.md` (#13) | 网状关联引擎 | 四因子公式 | A×0.6 + B×0.4 × C × D |
| `web-association-engine.md` (#13) | 网状关联引擎 | TagSimilarityMatrix | N×N 预计算矩阵 SO |
| `web-association-engine.md` (#13) | 网状关联引擎 | 候选池过滤规则 | 同章+已解锁+排除自身 |
| `web-association-engine.md` (#13) | 网状关联引擎 | 视觉分级 4 档 | Strong/Medium/Faint/Trace |
| `emotional-tag-system.md` (#10) | 情感标签 | 标签相似度计算 | 因子 A — 查询 TagSimilarityMatrix |
| `in-game-hud.md` (#17) | 游戏 HUD | 关联路径展示 | AssociationCandidate list → HUD |

## Related

- ADR-0002 — IDataManager 提供碎片数据查询
- ADR-0008 — ConditionGroup 过滤候选池
- 情感标签系统 GDD #10 — TagSimilarity 矩阵数据（无独立 ADR）
- `docs/architecture/architecture.md` §2 — WebAssociationEngine 模块所有权
