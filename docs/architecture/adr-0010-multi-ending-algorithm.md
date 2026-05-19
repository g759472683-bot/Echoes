# ADR-0010: 多结局判定算法 — 触发器 + 权重 + 情感亲和

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

每章完成后判定玩家获得的结局。决定使用三阶段判定算法：收集触发器 → IsEssential 门控 → 累积 ContributionWeight → EmotionalAffinity 路径加分 → 阈值检查 → Tie-breaking。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — 纯 C# 逻辑，无 Unity API 依赖 |
| **References Consulted** | `VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0008 (ConditionGroup — EndingTrigger 条件求值), 情感标签系统 GDD #10 (标签权重与 AffinityTag 数据 — 无独立 ADR) |
| **Enables** | None |
| **Blocks** | ChapterManager + MultiEnding Epic |
| **Ordering Note** | 在 ADR-0008 之后实现 |

## Context

### Problem Statement

每章有 2-4 个可能结局。结局取决于玩家在该章中的行为和情感累积——访问了哪些碎片、做了哪些选择、累积了哪些情感标签权重。判定需在章节完成时自动触发，输出单一 ResolvedEnding。

### Constraints

- 每章必有 1 个 Fallback Ending（无触发条件，权重 0 — 保底）
- 结局 ID 全局唯一
- 判定在章节完成的同一帧内执行（< 1ms）
- 已解锁结局永久记录（UnlockedEndingIds HashSet 并集）

### Requirements

- 三阶段判定算法
- IsEssential 门控（必要条件不满足则结局不可用）
- ContributionWeight 累积
- EmotionalAffinity 路径加分
- Tie-breaking（同分情况下的优先级规则）

## Decision

**三阶段判定算法：收集 → 门控+累积 → 阈值+决胜。**

### Stage 1: 收集触发器

```csharp
var triggers = endingDefs
    .SelectMany(def => def.EndingTriggers
        .Where(t => ChangeTracker.EvaluateCondition(t.Condition))
        .Select(t => (def.EndingId, t.ContributionWeight, t.IsEssential)));
```

### Stage 2: IsEssential 门控 + 权重累积

```csharp
var accumulated = new Dictionary<string, float>();

foreach (var (endingId, weight, isEssential) in triggers)
{
    if (!accumulated.ContainsKey(endingId))
        accumulated[endingId] = 0;

    accumulated[endingId] += weight;

    // IsEssential 标记 (后面在 Stage 3 检查)
    if (isEssential)
        _essentialFlags[endingId] = true;
}
```

### Stage 3: EmotionalAffinity + 阈值 + Tie-breaking

```csharp
public ResolvedEnding ResolveEnding(string chapterId)
{
    // 3a. 过滤：所有 IsEssential 触发器必须满足
    var eligible = endingDefs
        .Where(def => def.EndingTriggers
            .Where(t => t.IsEssential)
            .All(t => ChangeTracker.EvaluateCondition(t.Condition)));

    // 3b. 计算总分 = ContributionWeight Sum + EmotionalAffinity 加成
    var scored = eligible.Select(def =>
    {
        var baseScore = accumulated.GetValueOrDefault(def.EndingId, 0);
        var primaryTag = EmotionalTagSystem.GetPrimaryTag(currentFragmentId);
        var affinityBonus = def.AffinityTagId == primaryTag?.Id ? def.AffinityBonus : 0;
        return (def.EndingId, Score: baseScore + affinityBonus);
    });

    // 3c. 阈值过滤 (必须 ≥ 最低阈值)
    var aboveThreshold = scored
        .Where(s => s.Score >= GetMinThreshold(s.EndingId));

    // 3d. Tie-breaking
    var ordered = aboveThreshold
        .OrderByDescending(s => s.Score)
        .ThenByDescending(s => GetTriggerCount(s.EndingId))  // 触发数多优先
        .ThenBy(s => s.EndingId);                             // ID 字典序 (确定性)

    var winner = ordered.FirstOrDefault();

    // 3e. Fallback: 如果没有结局满足条件，返回默认结局
    return winner ?? GetFallbackEnding(chapterId);
}
```

### EmotionalAffinity 路径加分

```
如果玩家当前碎片的主情感标签匹配某结局的 AffinityTagId:
  Score += AffinityBonus (设计师在 ChapterDefinition.Endings[] 中配置的值)
```

### 完整流程

```
┌─────────────────────────────────────────────────────┐
│  ChapterManager.OnChapterCompleted(chapterKey)       │
│    │                                                 │
│    ▼                                                 │
│  MultiEndingSystem.ResolveEnding(chapterKey)         │
│    │                                                 │
│    ├─ Stage 1: 收集触发器                             │
│    │   遍历 ChapterDefinition.Endings[]               │
│    │   → 对每个 EndingTrigger:                        │
│    │     EvaluateCondition() → satisfied?             │
│    │     → 记录: EndingId, ContributionWeight,        │
│    │             IsEssential                         │
│    │                                                 │
│    ├─ Stage 2: IsEssential 门控                      │
│    │   淘汰: 任一 IsEssential 触发器未满足 → 结局不可用 │
│    │                                                 │
│    ├─ Stage 3: 评分 + 决胜                           │
│    │   ├─ TotalScore = ΣContributionWeight            │
│    │   │              + EmotionalAffinityBonus        │
│    │   ├─ 阈值检查 → 淘汰低于阈值的结局                │
│    │   └─ Tie-breaking: Score DESC → TriggerCount     │
│    │                    DESC → EndingId ASC          │
│    │                                                 │
│    └─ 输出: ResolvedEnding                           │
│         ├─ EndingId                                  │
│         ├─ Score                                     │
│         └─ DisplayName (本地化)                       │
└─────────────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public class MultiEndingSystem
{
    public ResolvedEnding ResolveEnding(string chapterId);
    public void OnChapterStart(string chapterId);
    public HashSet<string> GetUnlockedEndingIds();

    // 依赖注入
    public MultiEndingSystem(
        IDataManager dataManager,
        IChangeTracker changeTracker,
        IEmotionalTagSystem tagSystem);
}

public struct ResolvedEnding
{
    public string EndingId;
    public float Score;
    public string DisplayName;
    public bool IsFallback;
}
```

### Implementation Guidelines

1. `ResolveEnding` 是纯函数（相同状态 → 相同结局）
2. 每次章节完成时自动调用；结果记录到 `UnlockedEndingIds`（永久并集）
3. Fallback Ending 必须存在（每个 ChapterDefinition 至少有 1 个无条件结局）
4. Tie-breaking 规则保证确定性（不依赖随机）

## Alternatives Considered

### Alternative 1: 单一阈值触发（最高累积分直接获胜）

- **Description**: 去掉 IsEssential 门控和 EmotionalAffinity，只有 ContributionWeight 累积分
- **Pros**: 简单
- **Cons**: 无法表达"必要条件"（如玩家必须遇见某角色）——高权重碎片可能覆盖必要条件
- **Rejection Reason**: IsEssential 门控对叙事设计至关重要（"必须见到 mentor 才能触发 mentor 线结局"）

### Alternative 2: 流程图/状态机（手动定义结局转换路径）

- **Description**: 使用可视化流程图定义所有可能的结局路径（类似对话树）
- **Pros**: 可视化直观
- **Cons**: 不能响应情感标签累积（数据驱动）；碎片数量多时图不可维护
- **Rejection Reason**: 情感累积是回响 (Echoes) 的核心机制，必须在结局判定中使用。纯手动路径图无法利用标签系统数据

## Consequences

### Positive

- 设计师精细控制结局条件（IsEssential + ContributionWeight + AffinityTag）
- 确定性（相同 game state → 相同结局）
- 可测试（每阶段可独立单元测试）
- 支持 Fallback（保底结局）

### Negative

- 三阶段算法对设计师有认知负担（需理解 IsEssential vs ContributionWeight 差异）
- 若所有结局都设置高阈值可能导致 Fallback 频繁触发
- Tie-breaking 规则可能需要迭代（当前方案在实际游戏中可能不够）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 结局判定算法产生意外结果（非设计师意图） | Medium | Medium | 单元测试覆盖关键路径；Editor 结局模拟工具 |
| Fallback 触发频率过高 | Low | Medium | Playtest 数据监控 Fallback 频率；Tuning Knobs 暴露阈值 |
| 情感标签数据不足以驱动结局判定 | Low | High | 情感标签系统 + 结局系统集成测试 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (ResolveEnding, 4 结局 × 5 trigger) | < 1ms |
| Memory (UnlockedEndingIds, 全游戏) | ~200B (少量字符串) |
| GC Allocation | ~100B (LINQ enumerator) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] IsEssential 触发器未满足 → 结局不可用
- [ ] EmotionalAffinity 加成正确计算
- [ ] 同分时 Tie-breaking 确定性（多次运行相同结果）
- [ ] Fallback 在无结局满足条件时触发
- [ ] 已解锁结局 ID 永久记录（不随章节重玩丢失）

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `multi-ending-system.md` (#14) | 多结局 | 三阶段判定 | 收集 → 门控 → 评分+决胜 |
| `multi-ending-system.md` (#14) | 多结局 | IsEssential 必要条件 | Stage 2 门控 |
| `multi-ending-system.md` (#14) | 多结局 | EmotionalAffinity 路径加分 | Stage 3 加分计算 |
| `multi-ending-system.md` (#14) | 多结局 | Fallback 保底结局 | Stage 3e |
| `chapter-management.md` (#15) | 章节管理 | 章节完成自动判定结局 | OnChapterCompleted → ResolveEnding |
| `emotional-tag-system.md` (#10) | 情感标签 | 标签权重影响结局 | AffinityTag 匹配 + ContributionWeight 来源于标签权重 |

## Related

- ADR-0008 — ConditionGroup（EndingTrigger 条件求值）
- 情感标签系统 GDD #10 — EmotionalAffinity tag 数据（无独立 ADR）
- `docs/architecture/architecture.md` §2 — MultiEndingSystem 模块所有权
