# Story 004: 隐藏结局跨章节支持 + Path Bonus Hook

> **Epic**: 多结局系统 (MultiEndingSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 2h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/multi-ending-system.md`
**Requirement**: `TR-multi-ending-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0010: 多结局判定算法
**ADR Decision Summary**: 隐藏结局通过 FlagSet + ChapterCompleted ConditionGroup 实现跨章节条件——无需特殊判定路径；Path Bonus Hook（EmotionalAffinity 匹配 dominantPathEmotion）MVP 默认 weight=0.0，已实现但禁用

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: ConditionGroup 跨章节条件依赖于 ChangeTracker._flags 和 _completedChapters 跨会话持久化

**Control Manifest Rules (Feature Layer)**:
- Required: Condition evaluation triggered on query — EvaluateCondition() called during ResolveEnding — source: ADR-0008

---

## Acceptance Criteria

*From GDD `design/gdd/multi-ending-system.md`, scoped to this story:*

- [ ] GIVEN 隐藏结局 hidden_reunion 的 EndingTrigger 定义在 Ch03 的碎片中，条件为 `{ All: [FlagSet("ch1_letter", true), ChapterCompleted("ch1"), FlagSet("ch2_secret", true)] }`，且这些 Flag 均已设置、章节均已完成，WHEN Ch03 的 ResolveEnding("ch03") 执行，THEN hidden_reunion 的必要门通过，进入累加分数计算。

- [ ] GIVEN hidden_reunion 的跨章节条件中 FlagSet("ch1_letter", true) 未满足，WHEN ResolveEnding("ch03") 执行，THEN hidden_reunion 在必要门被取消资格。

- [ ] GIVEN 章节有结局设置 EmotionalAffinity = "Sadness" 且 dominantPathEmotion = "Sadness"，_pathBonusWeight = 0.0（默认），WHEN ResolveEnding 执行，THEN 无加成（Path Bonus 已实现但以 weight=0.0 禁用）。

---

## Implementation Notes

*Derived from GDD rules 7-8 + ADR-0010:*

### Hidden Ending — Cross-Chapter Conditions

隐藏结局不引入新的判定路径——它复用现有的 ConditionGroup 系统：

- EndingTrigger.TriggerCondition 包含 `FlagSet("ch1_letter", true)` 和 `ChapterCompleted("ch1")` 等跨章节条件
- ChangeTracker.EvaluateCondition() 评估这些条件时不区分"跨章"和"章内"
- Flag 和 ChapterCompleted 数据的跨章持久化由 ChangeTracker (#12) 和跨章节状态追踪 (#16) 负责

```csharp
// No special code path needed — ConditionGroup handles cross-chapter conditions natively:
// EndingTrigger with condition: { All: [FlagSet("ch1_letter", true), ChapterCompleted("ch1")] }
// → EvaluateCondition returns true only if all sub-conditions pass
```

### Path Bonus Hook (MVP Disabled)

```csharp
private float _pathBonusWeight = 0.0f; // MVP default — disabled

private string ComputeDominantPathEmotion(string chapterId)
{
    var visited = _changeTracker.GetVisitedFragments(chapterId);
    var categoryCounts = new Dictionary<string, int>();
    
    foreach (var fragId in visited)
    {
        var tags = _tagSystem.GetTagsForFragment(fragId);
        var dominant = GetDominantCategory(tags);
        if (!string.IsNullOrEmpty(dominant))
        {
            categoryCounts.TryGetValue(dominant, out int count);
            categoryCounts[dominant] = count + 1;
        }
    }
    
    return categoryCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
}
```

Path Bonus 应用于 Stage 3b（Story 002）——此处仅为 Hook 实现和数据流验证。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 数据结构
- Story 002: 判定算法核心
- Story 003: Tie-Breaking + 持久化
- Path Bonus 启用（需 playtest 验证 _pathBonusWeight > 0 的效果）
- 跨章节状态追踪 (#16): Flag 持久化和恢复——本故事仅消费 ChangeTracker 提供的 Flag 数据

---

## QA Test Cases

- **AC-1**: 隐藏结局条件满足
  - Given: Ch03 碎片中有 hidden_reunion 的 EndingTrigger，条件 = All[FlagSet("ch1_letter",true), ChapterCompleted("ch1"), FlagSet("ch2_secret",true)]；所有条件满足
  - When: ResolveEnding("ch03")
  - Then: hidden_reunion 的 Essential Gate 通过；进入分数累积
  - Edge cases: 部分条件满足 → Essential Gate 失败（若条件被标记为 IsEssential）

- **AC-2**: 隐藏结局条件未满足
  - Given: hidden_reunion 的 IsEssential 条件包含 FlagSet("ch1_letter", true)；Flag 为 false
  - When: ResolveEnding("ch03")
  - Then: hidden_reunion 在 Stage 2 被取消资格
  - Edge cases: 非 IsEssential 条件未满足 → 不影响 Essential Gate，仅不贡献分数

- **AC-3**: Path Bonus disabled
  - Given: EmotionalAffinity="Sadness", dominantPathEmotion="Sadness", _pathBonusWeight=0.0
  - When: ResolveEnding
  - Then: score 无加成（×1.0 而非 ×(1.0 + 0.0)）
  - Edge cases: _pathBonusWeight > 0 → 加成生效（需在 Tuning Knobs 中设置）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/multi-ending/hidden_ending_path_bonus_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (ResolveEnding algorithm)；ChangeTracker Story 003 (_flags, _completedChapters, EvaluateCondition)
- Unlocks: 完整的多结局系统 — 与其他系统集成

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 3/3 passing
**Deviations**: None
**Test Evidence**: `tests/integration/multi-ending/hidden_ending_path_bonus_test.cs` — 10 test functions
**Code Review**: Pending
