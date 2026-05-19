# Story 003: Flag 系统 + 条件求值集成

> **Epic**: 记忆变化追踪 (ChangeTracker)
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/memory-change-tracking.md`
**Requirement**: `TR-memory-change-tracking-004`, `TR-memory-change-tracking-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: SO 不可变 + Overlay; ADR-0008: ConditionGroup 求值引擎
**ADR Decision Summary**: Flag 系统通过 _flags Dictionary 实现（SetFlag/GetFlag）；ConditionGroup 求值引擎使用复合模式（6 种 Leaf Condition + 3 种 Combinator），短路径求值，最大深度 3；EvaluateCondition 作为 ChangeTracker 的统一条件入口

**Engine**: Unity 6.3 LTS | **Risk**: LOW (纯 C# 逻辑)
**Engine Notes**: Condition 求值零 GC（纯值类型运算）；`[SerializeReference]` 多态序列化需 link.xml 保留 9 个 Condition 子类

**Control Manifest Rules (Feature Layer)**:
- Required: Composite ConditionGroup pattern — 6 leaf conditions + 3 combinators, max depth 3, short-circuit — source: ADR-0008
- Required: Condition evaluation triggered on query, not on state change — source: ADR-0008
- Forbidden: Never exceed ConditionGroup depth 3 — source: ADR-0008

---

## Acceptance Criteria

*From GDD `design/gdd/memory-change-tracking.md`, scoped to this story:*

- [ ] GIVEN 玩家在碎片 A 选择了选项 X (触发 SetFlag("ch1_letter_kept", true))，WHEN 后续碎片 B 的物件条件为 `FlagSet("ch1_letter_kept", true)`，THEN EvaluateCondition 返回 true——物件变为可交互。

- [ ] GIVEN 碎片 D 的一个物件需要 `VisitedFragment("frag_E")` 条件，且玩家之前已访问过 frag_E，WHEN 场景管理触发 OnFragmentTransitioned("frag_D") → 交互系统查询物件条件，THEN EvaluateCondition 返回 true——物件可交互。

- [ ] GIVEN 玩家在章节 1 完成时触发了章节完成事件，WHEN 章节 3 的某结局触发条件包含 `ChapterCompleted("ch1")`，THEN EvaluateCondition 返回 true。

- [ ] GIVEN FlagSet 条件查询的 Flag 从未被设置过，WHEN EvaluateCondition(FlagSet("never_set_flag", true))，THEN 返回 false（未设置的 Flag 视为 false）。

- [ ] GIVEN ConditionGroup 深度为 4（超出最大深度 3），WHEN 编辑器验证运行，THEN 报错标记该 ConditionGroup 深度超限。

---

## Implementation Notes

*Derived from ADR-0007 + ADR-0008 Implementation Guidelines:*

### Flag 系统

```csharp
// In ChangeTracker:
private Dictionary<string, bool> _flags;

public void SetFlag(string flagId, bool value)
{
    // If flag already has same value → skip (idempotent)
    if (_flags.TryGetValue(flagId, out var existing) && existing == value)
        return;
    _flags[flagId] = value;
    OverlayVersion++;
}

public bool GetFlag(string flagId)
{
    return _flags.TryGetValue(flagId, out var value) && value;
}
```

### 跟踪集合

```csharp
private HashSet<string> _visitedFragments;
private HashSet<string> _completedChapters;

// Updated via SceneManager events:
// OnFragmentTransitioned → _visitedFragments.Add(fragmentId)
// OnChapterCompleted → _completedChapters.Add(chapterId)

public bool HasVisited(string fragmentId) => _visitedFragments.Contains(fragmentId);
public bool IsChapterCompleted(string chapterId) => _completedChapters.Contains(chapterId);
```

### 条件求值入口

```csharp
public bool EvaluateCondition(ConditionGroup group)
{
    if (group == null) return true; // null condition = always passes
    
    return group.Combinator switch
    {
        Combinator.All => group.Children.All(c => EvaluateSingle(c)),
        Combinator.Any => group.Children.Any(c => EvaluateSingle(c)),
        Combinator.Not => !EvaluateSingle(group.Children[0]),
        _ => false
    };
}

private bool EvaluateSingle(Condition c) => c switch
{
    ConditionAlways _ => true,
    ConditionChoiceMade cm => _overlay.ContainsKey((cm.FragmentId, cm.ChoiceId)),
    ConditionFlagSet fs => GetFlag(fs.FlagId) == fs.ExpectedValue,
    ConditionObjectStateIs os => EvaluateObjectState(os),
    ConditionVisitedFragment vf => HasVisited(vf.FragmentId),
    ConditionChapterCompleted cc => IsChapterCompleted(cc.ChapterId),
    _ => false
};
```

深度验证在 Editor 工具中执行（`[MenuItem("回响/Validate/Condition Depth")]`）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: _overlay 数据结构、ApplyChanges（SetFlag 在 ApplyChanges 中被调用——本故事实现 SetFlag 本身）
- Story 002: GetCurrentState 合并（ObjectStateIs 条件使用 GetCurrentState 查询对象状态——本故事消费它）
- Story 004: 存档序列化（_flags 和集合的持久化）
- memory-fragment-data-model: ConditionGroup SO 定义、Condition 子类的 [SerializeReference] 序列化

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: SetFlag + FlagSet 条件
  - Given: _flags 为空；调用 SetFlag("ch1_letter_kept", true)
  - When: EvaluateCondition(FlagSet("ch1_letter_kept", true))
  - Then: 返回 true
  - Edge cases: FlagSet 查询未设置的 Flag → false；SetFlag 同值 → 幂等跳过；SetFlag 不同值 → 覆盖旧值

- **AC-2**: VisitedFragment 条件
  - Given: _visitedFragments 包含 "frag_E"（通过 OnFragmentTransitioned 事件添加）
  - When: EvaluateCondition(VisitedFragment("frag_E"))
  - Then: 返回 true
  - Edge cases: 未访问的碎片 → false；空字符串 fragmentId → false

- **AC-3**: ChapterCompleted 条件
  - Given: _completedChapters 包含 "ch1"；ConditionGroup 为 ChapterCompleted("ch1")
  - When: EvaluateCondition
  - Then: 返回 true
  - Edge cases: 未完成的章节 → false

- **AC-4**: 未设置 Flag 的默认行为
  - Given: _flags 中无 "never_set_flag"
  - When: EvaluateCondition(FlagSet("never_set_flag", true))
  - Then: 返回 false
  - Edge cases: FlagSet("x", false) 且 x 未设置 → TryGetValue 返回 false，但 ExpectedValue 是 false → 应返回 false（语义：未设置的 Flag 为 false，查询 false 时应返回 true？）

> **注意**: FlagSet("x", false) + x 未设置 → 应返回 true（因为未设置 = false，且查询 false）。实现: `GetFlag(fs.FlagId) == fs.ExpectedValue` 其中 GetFlag 返回 false (default) → false == false → true。

- **AC-5**: 深度超限验证
  - Given: ConditionGroup 深度为 4（All → Any → Not → FlagSet）
  - When: 编辑器验证运行
  - Then: 报错信息包含深度超限警告
  - Edge cases: 深度恰好为 3 → 不报错；单层条件深度为 1 → 不报错

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/memory-change-tracking/flag_condition_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (_overlay, SetFlag 入口), Story 002 (GetCurrentState — ObjectStateIs 条件查询对象状态)
- Depends on: memory-fragment-data-model Story (ConditionGroup + Condition 子类 SO 定义)
- Unlocks: 下游系统（多结局 #14、跨章节状态追踪 #16）可通过 EvaluateCondition 和 GetFlag 查询
