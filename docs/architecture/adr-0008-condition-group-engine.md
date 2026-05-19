# ADR-0008: ConditionGroup 求值引擎设计

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

游戏中的条件判断（对象可见性、选项可用性、结局触发条件等）需要灵活的布尔表达式求值。决定使用复合模式：6 种 Leaf Condition + 3 种 Combinator (All/Any/Not) + 最大嵌套深度 3。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — 纯 C# 逻辑，不依赖 Unity API |
| **References Consulted** | `VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0007 (base SO + overlay — Evaluator 输入来自 IChangeTracker) |
| **Enables** | ADR-0009 (关联引擎条件过滤), ADR-0010 (结局判定触发器), ADR-0011 (跨章标记条件) |
| **Blocks** | ChangeTracker + WebAssociation + MultiEnding Epic |
| **Ordering Note** | 在 ADR-0007 之后创建 |

## Context

### Problem Statement

MemoryFragment 系统中多处需要条件判断：

- **ContentChange** 触发的条件：`If (HasFlag("met_mentor") AND TagWeight("trust") > 0.7) → ReplaceText(...)`
- **InteractiveObject** 可见性：`If (Not(HasFlag("hide_letter"))) → Show`
- **ExplicitAssociation** 可用性：`If (All(HasFlag("ch2_complete"), Any(TagWeight("curious") > 0.5, HasFlag("explorer")))) → Enable`
- **EndingTrigger** 激活：`If (All(HasFlag("essential_path"), TagWeight("empathy") > 0.8)) → Trigger`

需要统一的求值引擎支持这些嵌套布尔表达式。

### Constraints

- 最大嵌套深度 3（避免设计师构建不可调试的条件树）
- 必须在 < 0.1ms 内求值
- 条件节点在 ScriptableObject 中定义（设计师可编辑）
- 条件不产生副作用（纯函数）

### Requirements

- 6 种 Leaf Condition 类型
- 3 种 Combinator (All/Any/Not)
- 运行时合并 base SO condition + overlay 修改后的条件
- 短路求值（Any 遇 true 即返回, All 遇 false 即返回）

## Decision

**复合模式 ConditionGroup: 6 Leaf + 3 Combinator + 深度限制 3 + 短路求值。**

### 6 种 Leaf Condition

```csharp
[Serializable]
public abstract class Condition
{
    public abstract bool Evaluate(IChangeTracker ctx);
}

// 1. HasFlag — 检查布尔标记
[Serializable]
public class HasFlagCondition : Condition
{
    public string FlagId;
    public override bool Evaluate(IChangeTracker ctx) => ctx.GetFlag(FlagId);
}

// 2. VisitedFragment — 是否访问过某碎片
[Serializable]
public class VisitedFragmentCondition : Condition
{
    public string FragmentId;
    public override bool Evaluate(IChangeTracker ctx) => ctx.HasVisited(FragmentId);
}

// 3. ChapterCompleted — 某章是否完成
[Serializable]
public class ChapterCompletedCondition : Condition
{
    public string ChapterKey;
    public override bool Evaluate(IChangeTracker ctx) => ctx.IsChapterCompleted(ChapterKey);
}

// 4. TagWeight — 某情感标签当前权重与阈值比较
[Serializable]
public class TagWeightCondition : Condition
{
    public string TagId;
    public ComparisonOp Op;  // GreaterThan, LessThan, Equal
    public float Threshold;  // 0.0-1.0
    public override bool Evaluate(IChangeTracker ctx)
    {
        var weight = ctx.GetTagWeight(TagId);
        return Op switch
        {
            ComparisonOp.GreaterThan => weight > Threshold,
            ComparisonOp.LessThan => weight < Threshold,
            _ => Math.Abs(weight - Threshold) < 0.001f
        };
    }
}

// 5. ChoiceCount — 某选项被选择次数
[Serializable]
public class ChoiceCountCondition : Condition
{
    public string ChoiceId;
    public int MinCount;
    public override bool Evaluate(IChangeTracker ctx) => ctx.GetChoiceCount(ChoiceId) >= MinCount;
}

// 6. EndingUnlocked — 某结局是否已解锁
[Serializable]
public class EndingUnlockedCondition : Condition
{
    public string EndingId;
    public override bool Evaluate(IChangeTracker ctx) => ctx.IsEndingUnlocked(EndingId);
}
```

### 3 种 Combinator

```csharp
// All — 所有子条件为 true
[Serializable]
public class AllCondition : Condition
{
    [SerializeReference] public Condition[] Children; // max depth 3
    public override bool Evaluate(IChangeTracker ctx)
    {
        foreach (var child in Children)
            if (!child.Evaluate(ctx)) return false; // 短路
        return true;
    }
}

// Any — 任一子条件为 true
[Serializable]
public class AnyCondition : Condition
{
    [SerializeReference] public Condition[] Children;
    public override bool Evaluate(IChangeTracker ctx)
    {
        foreach (var child in Children)
            if (child.Evaluate(ctx)) return true; // 短路
        return false;
    }
}

// Not — 取反
[Serializable]
public class NotCondition : Condition
{
    [SerializeReference] public Condition Child;
    public override bool Evaluate(IChangeTracker ctx) => !Child.Evaluate(ctx);
}
```

### 深度限制

```csharp
public static class ConditionValidator
{
    public static bool ValidateDepth(Condition condition, int maxDepth = 3)
    {
        return GetDepth(condition, 0) <= maxDepth;
    }

    private static int GetDepth(Condition c, int current)
    {
        return c switch
        {
            AllCondition all => all.Children.Max(child => GetDepth(child, current + 1)),
            AnyCondition any => any.Children.Max(child => GetDepth(child, current + 1)),
            NotCondition not => GetDepth(not.Child, current + 1),
            _ => current
        };
    }
}
```

### Architecture Diagram

```
ConditionGroup (ScriptableObject 中)
  │
  ▼
AllCondition (root, depth=0)
  ├─ HasFlagCondition("met_mentor")           ← Leaf (depth=1)
  └─ AnyCondition (depth=1)
       ├─ TagWeightCondition("trust", GT, 0.7) ← Leaf (depth=2)
       └─ HasFlagCondition("explorer")         ← Leaf (depth=2)
  │
  ▼
IChangeTracker.EvaluateCondition(group)
  → 递归求值, 短路, 无副作用
  → 返回 bool
```

### Key Interfaces

```csharp
public interface IChangeTracker
{
    // 求值引擎入口
    bool EvaluateCondition(ConditionGroup condition);

    // Leaf Condition 数据源
    bool GetFlag(string flagId);
    bool HasVisited(string fragmentId);
    bool IsChapterCompleted(string chapterKey);
    float GetTagWeight(string tagId);
    int GetChoiceCount(string choiceId);
    bool IsEndingUnlocked(string endingId);
}
```

### Implementation Guidelines

1. 所有 Condition 子类用 `[Serializable]` + `[SerializeReference]` 标记（SO 中多态存储）
2. `Evaluate()` 是纯函数 — 不修改 IChangeTracker 状态
3. 深度验证在 Editor 自定义 Inspector 中执行（红色警告深度超限）
4. 短路求值减小平均开销（All 遇 false 立即返回）

## Alternatives Considered

### Alternative 1: 字符串表达式解析 (如 `"flag(met_mentor) AND tag(trust) > 0.7"`)

- **Description**: 条件以字符串存储，运行时解析
- **Pros**: 可读性好；可在文本文件编辑
- **Cons**: 无编译时类型检查；解析有 GC 分配；错误提示不友好（运行时才知道 typo）
- **Rejection Reason**: 无编译时安全，GC 分配不可接受

### Alternative 2: Unity Animator/StateMachine 做条件控制

- **Description**: 使用 Unity Animator Controller 的状态转换条件
- **Pros**: 可视化编辑器
- **Cons**: Animator 设计用于动画，做通用条件求值笨重；无法在纯 C# 类中使用
- **Rejection Reason**: Animator 不是条件求值引擎（用途不匹配）

## Consequences

### Positive

- 编译时类型安全（每个 Leaf Condition 是强类型 C# 类）
- 纯函数求值（可并⾏，可缓存，可单元测试）
- 短路求值减少开销
- 设计师在 SO Inspector 中可视化编辑条件树

### Negative

- `[SerializeReference]` 在 IL2CPP 中的多态序列化需验证
- 自定义 Editor Inspector 需要开发（显示条件树）
- 新增 Leaf Condition 类型需要新增 C# 类

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| `[SerializeReference]` IL2CPP AOT stripping | Medium | High | link.xml 保留所有 Condition 子类型；Pre-Production IL2CPP 验证 |
| 深度限制在 Editor 中被绕过 | Low | Medium | 自定义 Inspector 强制验证 |
| Leaf Condition 类型不足 | Medium | Low | 可扩展架构（继承 Condition 即可） |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (单 Leaf 求值) | ~0.001ms (Dictionary lookup) |
| CPU (深度 3 条件树求值) | ~0.01ms (最坏情况) |
| CPU (短路优化后平均值) | ~0.003ms |
| GC Allocation | 0 (纯值类型运算) |
| Memory (ConditionGroup SO) | ~100-500B per condition |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] All/Any/Not 三种 Combinator 逻辑正确（单元测试）
- [ ] 深度 3+1 的条件树被 Inspector 标记为错误
- [ ] 短路求值：All 第一个 false 后不再求值后续子条件
- [ ] Evaluate 多次调用无副作用（相同输入 → 相同输出）
- [ ] 自定义 Editor 支持增删改条件节点

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `memory-fragment-data-model.md` (#8) | 数据模型 | 6 种 Condition 类型 | 6 种 Leaf Condition 类 |
| `memory-fragment-data-model.md` (#8) | 数据模型 | All/Any/Not 组合器 | 3 种 Combinator |
| `memory-fragment-data-model.md` (#8) | 数据模型 | 最大嵌套深度 3 | ConditionValidator |
| `memory-change-tracking.md` (#12) | 变化追踪 | ContentChange 条件求值 | EvaluateCondition 统一入口 |
| `web-association-engine.md` (#13) | 网状关联 | 候选池条件过滤 | 消费同一 IChangeTracker 接口 |
| `multi-ending-system.md` (#14) | 多结局 | EndingTrigger 条件 | 消费同一 IChangeTracker 接口 |

## Related

- ADR-0007 — base SO + overlay（Condition 定义在 SO 中，运行时合并 overlay 影响）
- ADR-0009 — 关联引擎条件过滤
- ADR-0010 — 结局判定触发器
