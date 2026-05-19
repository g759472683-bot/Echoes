# Story 002: ConditionGroup 条件求值引擎

> **Epic**: 记忆碎片数据模型 (MemoryFragment)
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/memory-fragment-data-model.md`
**Requirement**: `TR-memory-fragment-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0008: ConditionGroup 求值引擎设计
**ADR Decision Summary**: 复合 ConditionGroup 模式——6 种 Leaf Condition + 3 种 Combinator (All/Any/Not) + 最大嵌套深度 3 + 短路求值 + `[SerializeReference]` 多态序列化

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯 C# 逻辑，不依赖 Unity API；`[SerializeReference]` 在 IL2CPP 中需 link.xml 保留 9 个 Condition 子类

**Control Manifest Rules (Feature Layer)**:
- Required: Composite ConditionGroup pattern — 6 leaf conditions + 3 combinators, max depth 3, short-circuit evaluation — source: ADR-0008
- Required: Condition evaluation triggered on query, not on state change — source: ADR-0008
- Forbidden: Never exceed ConditionGroup depth 3 — source: ADR-0008

---

## Acceptance Criteria

*From GDD `design/gdd/memory-fragment-data-model.md`, scoped to this story:*

- [ ] GIVEN 一个 ConditionGroup 嵌套深度为 3 层，WHEN 编辑器验证运行，THEN 验证通过。若嵌套深度为 4 层——验证报错"条件嵌套超过最大深度 3"
- [ ] GIVEN 一个 EndingTrigger 的 TriggerCondition 为 `ChoiceMade("ch1_frag_07", "keep_letter") AND ChapterCompleted("ch1")`，WHEN 多结局系统评估该触发条件，THEN 仅当玩家在 frag_07 中选了 keep_letter 且第一章已完成时条件为 true
- [ ] 短路求值：All 遇第一个 false 不再求值后续条件；Any 遇第一个 true 不再求值后续条件；Not 求值子条件后取反
- [ ] Evaluate() 是纯函数——多次调用相同输入返回相同输出，不修改 IChangeTracker 状态

---

## Implementation Notes

*Derived from ADR-0008:*

6 种 Leaf Condition:
```csharp
[Serializable] public abstract class Condition
{
    public abstract bool Evaluate(IChangeTracker ctx);
}

// 1. Always — 始终满足
[Serializable] public class AlwaysCondition : Condition { ... }

// 2. ChoiceMade — 检查选择是否已做出
[Serializable] public class ChoiceMadeCondition : Condition
{
    public string FragmentId, ChoiceId;
    public override bool Evaluate(IChangeTracker ctx) => ctx.HasChoiceMade(FragmentId, ChoiceId);
}

// 3. FlagSet — 检查全局标记
[Serializable] public class FlagSetCondition : Condition
{
    public string FlagId;
    public bool Value;
    public override bool Evaluate(IChangeTracker ctx) => ctx.GetFlag(FlagId) == Value;
}

// 4. ObjectStateIs — 检查物件状态
[Serializable] public class ObjectStateIsCondition : Condition
{
    public string FragmentId, ObjectId;
    public ObjectState State;
    public override bool Evaluate(IChangeTracker ctx) => ctx.GetObjectState(FragmentId, ObjectId) == State;
}

// 5. VisitedFragment — 是否访问过碎片
[Serializable] public class VisitedFragmentCondition : Condition { ... }

// 6. TagWeight — 标签权重比较
[Serializable] public class TagWeightCondition : Condition
{
    public string TagId;
    public ComparisonOp Op; // GreaterThan, LessThan, Equal
    public float Threshold; // 0.0-1.0
    public override bool Evaluate(IChangeTracker ctx) { ... }
}
```

3 种 Combinator:
```csharp
[Serializable] public class AllCondition : Condition
{
    [SerializeReference] public Condition[] Children;
    public override bool Evaluate(IChangeTracker ctx)
    {
        foreach (var c in Children)
            if (!c.Evaluate(ctx)) return false; // 短路
        return true;
    }
}

[Serializable] public class AnyCondition : Condition
{
    [SerializeReference] public Condition[] Children;
    public override bool Evaluate(IChangeTracker ctx)
    {
        foreach (var c in Children)
            if (c.Evaluate(ctx)) return true; // 短路
        return false;
    }
}

[Serializable] public class NotCondition : Condition
{
    [SerializeReference] public Condition Child;
    public override bool Evaluate(IChangeTracker ctx) => !Child.Evaluate(ctx);
}
```

深度验证:
```csharp
public static class ConditionValidator
{
    public static bool ValidateDepth(Condition condition, int maxDepth = 3)
    {
        return GetDepth(condition, 0) <= maxDepth;
    }
    
    private static int GetDepth(Condition c, int current) => c switch
    {
        AllCondition all => all.Children.Max(child => GetDepth(child, current + 1)),
        AnyCondition any => any.Children.Max(child => GetDepth(child, current + 1)),
        NotCondition not => GetDepth(not.Child, current + 1),
        _ => current
    };
}
```

IChangeTracker 接口（条件数据源）:
```csharp
public interface IChangeTracker
{
    bool EvaluateCondition(ConditionGroup condition);
    bool GetFlag(string flagId);
    bool HasChoiceMade(string fragmentId, string choiceId);
    ObjectState GetObjectState(string fragmentId, string objectId);
    bool HasVisited(string fragmentId);
    float GetTagWeight(string tagId);
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: MemoryFragment SO Schema 定义 — ConditionGroup 在哪里被引用
- Story 003: 编辑器验证工具 — Inspector 深度警告 UI
- 记忆变化追踪 (#12): IChangeTracker 的具体实现（_flag Dictionary 等）——本 Story 仅定义接口和 Evaluate 逻辑
- 条件树的编辑器可视化 Inspector — 由 Tools Programmer 后续实现

---

## QA Test Cases

- **AC-1**: All 组合器
  - Given: AllCondition 包含 [FlagSet("met_mentor", true), TagWeightCondition("trust", GT, 0.7)]
  - When: ctx.GetFlag("met_mentor")=true, ctx.GetTagWeight("trust")=0.8
  - Then: Evaluate() 返回 true
  - Edge cases: 第一个为 false → 返回 false，不调用 GetTagWeight（短路验证）

- **AC-2**: Any 组合器
  - Given: AnyCondition 包含 [FlagSet("a", true), FlagSet("b", true)]
  - When: ctx.GetFlag("a")=true, ctx.GetFlag("b")=false
  - Then: Evaluate() 返回 true，不调用 GetFlag("b")（短路验证）
  - Edge cases: 所有 false → 返回 false

- **AC-3**: Not 组合器
  - Given: NotCondition(FlagSet("hide_letter", true))
  - When: ctx.GetFlag("hide_letter")=false
  - Then: Evaluate() 返回 true
  - Edge cases: Flag=true → Evaluate() 返回 false

- **AC-4**: 嵌套深度验证
  - Given: All(Any(HasFlag("a"), Not(HasFlag("b")))) — 深度 3
  - When: ConditionValidator.ValidateDepth(condition, 3)
  - Then: 返回 true
  - Edge cases: 深度 4 → ValidateDepth 返回 false

- **AC-5**: 纯函数验证
  - Given: 同一个 ConditionGroup + 同一个 IChangeTracker 状态
  - When: 调用 Evaluate() 两次
  - Then: 两次返回相同结果；IChangeTracker 内部状态不变
  - Edge cases: 调用间 ctx 状态发生变化 → 结果可以不同（因为 ctx 是不同的输入）

- **AC-6**: AC-2 from GDD — 组合条件
  - Given: ConditionGroup = All(ChoiceMade("ch1_frag_07", "keep_letter"), ChapterCompleted("ch1"))
  - When: 玩家在 frag_07 选了 keep_letter 且 ch1 已完成
  - Then: Evaluate() 返回 true
  - Edge cases: 仅满足一个条件 → false；两个都不满足 → false

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/memory-fragment/condition-group_test.cs` — must exist and pass

**Status**: [x] Created (37 test functions, all 6 ACs covered)

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 6/6 passing (37 unit tests)
**Deviations**: None — All/Any/Not combinators + short-circuit per ADR-0008. ConditionGroup.Evaluate() delegates to combinator. 7 leaf conditions (Always, ChoiceMade, FlagSet, ObjectStateIs, VisitedFragment, ChapterCompleted, TagWeight) all implement Evaluate(IChangeTracker). Depth validation at 3 levels max.
**Test Evidence**: Logic — `tests/unit/memory-fragment/condition-group_test.cs` (37 test functions)
**Code Review**: Skipped (lean mode)

---

## Dependencies

- Depends on: memory-fragment Story 001 (ConditionGroup + Condition 类型 Schema 定义)
- Unlocks: ChangeTracker (#12 — 条件求值), WebAssociation (#13 — 候选池过滤), MultiEnding (#14 — 结局触发判定)
