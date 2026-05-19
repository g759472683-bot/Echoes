# Story 003: Tie-Breaking + 结局持久化 + 重判

> **Epic**: 多结局系统 (MultiEndingSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/multi-ending-system.md`
**Requirement**: `TR-multi-ending-003`, `TR-multi-ending-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0010: 多结局判定算法
**ADR Decision Summary**: Tie-breaking 三级优先级——必要条件数 DESC → 新颖性偏向（不在 UnlockedEndingIds 中胜出）→ 定义顺序 ASC（确定性）；UnlockedEndingIds 并集语义（新结局添加，旧保留）；每次 ResolveEnding 重新评估（不缓存）

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯 C# 排序 + HashSet 操作

**Control Manifest Rules (Feature Layer)**:
- Required: Deterministic tie-breaking — Score DESC → TriggerCount DESC → EndingId ASC — source: ADR-0010

---

## Acceptance Criteria

*From GDD `design/gdd/multi-ending-system.md`, scoped to this story:*

- [ ] GIVEN ending_A score=0.6, ending_B score=0.6（平局），ending_A 有 3 个 IsEssential 满足，ending_B 有 2 个，WHEN Tie-Breaking 执行，THEN ending_A 胜出（必要条件数更多）。

- [ ] GIVEN 平局且必要条件数相同，ending_A 已在 UnlockedEndingIds 中，ending_B 不在，WHEN Tie-Breaking 执行，THEN ending_B 胜出（新颖性偏向——优先展示新内容）。

- [ ] GIVEN 玩家第一次玩 Ch01 抵达 ending_A，WHEN ResolveEnding 返回，THEN UnlockedEndingIds 包含 "ending_A"。玩家重玩 Ch01 抵达 ending_B → UnlockedEndingIds 现在包含 {"ending_A", "ending_B"}（并集——两个都保留）。

- [ ] GIVEN 玩家抵达 ending_A → 回到 frag_03 改变选择 → 再次调用 ResolveEnding("ch01")，WHEN 新选择改变了 Flag 和 ChoiceMade 状态，THEN 重新评估产生不同的结局（重判成功）。不缓存旧结果。

---

## Implementation Notes

*Derived from ADR-0010 Implementation Guidelines:*

### Tie-Breaking

```csharp
// After Stage 3c (threshold check), before returning winner:
var ordered = qualified
    .OrderByDescending(q => q.Score)
    .ThenByDescending(q => CountEssentialSatisfied(q.Def.EndingId, triggerGroups))
    .ThenByDescending(q => !_unlockedEndingIds.Contains(q.Def.EndingId)) // novelty
    .ThenBy(q => endingDefs.IndexOf(q.Def)); // definition order — deterministic

var winner = ordered.First();
```

### UnlockedEndingIds Union

```csharp
public HashSet<string> GetUnlockedEndingIds() => _unlockedEndingIds;

// Called after ResolveEnding returns:
private void RecordUnlock(string endingId)
{
    if (_unlockedEndingIds.Add(endingId))
    {
        // New unlock — fire event for achievements/gallery
        OnEndingUnlocked?.Invoke(endingId);
    }
}

// ADR-0001 static event
public static event Action<string> OnEndingUnlocked;
```

### Re-evaluation (No Caching)

- `ResolveEnding(chapterId)` is a pure function — always re-evaluates
- No internal cache of resolution results
- `_unlockedEndingIds` is the ONLY persistent state
- OnChapterStart resets nothing (idempotent — just a lifecycle marker)

### Save/Load Bridge

```csharp
public MultiEndingSaveData GetSaveData()
{
    return new MultiEndingSaveData { UnlockedEndingIds = _unlockedEndingIds.ToArray() };
}

public void Restore(MultiEndingSaveData data)
{
    _unlockedEndingIds = new HashSet<string>(data.UnlockedEndingIds ?? Array.Empty<string>());
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 数据结构
- Story 002: 判定算法核心（Essential gate + score + threshold + fallback）
- Story 004: 隐藏结局跨章节支持
- 存档系统 (#7): SaveData 聚合、文件 I/O——本故事仅提供序列化/恢复 bridge

---

## QA Test Cases

- **AC-1**: Tie-breaking — essential count
  - Given: ending_A score=0.6, 3 essential satisfied；ending_B score=0.6, 2 essential satisfied
  - When: Tie-Breaking
  - Then: ending_A 胜出
  - Edge cases: same essential count → fall through to next tie-breaker

- **AC-2**: Tie-breaking — novelty
  - Given: ending_A 和 ending_B 同分、同 essential count；ending_A 已解锁，ending_B 未解锁
  - When: Tie-Breaking
  - Then: ending_B 胜出
  - Edge cases: 两者都未解锁 → 定义顺序决胜

- **AC-3**: UnlockedEndingIds union
  - Given: _unlockedEndingIds = {"ending_A"}；ResolveEnding 返回 "ending_B"
  - When: RecordUnlock("ending_B")
  - Then: _unlockedEndingIds = {"ending_A", "ending_B"}；OnEndingUnlocked 事件触发
  - Edge cases: 重复解锁同一 ending → 幂等，不触发事件

- **AC-4**: Re-evaluation
  - Given: 第一次 ResolveEnding 返回 ending_A；玩家改变选择（不同 Flag）；_unlockedEndingIds 已包含 ending_A
  - When: 第二次 ResolveEnding
  - Then: 重新评估所有条件；可能返回 ending_B；不使用缓存
  - Edge cases: 相同选择 → 相同结局（确定性）

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/multi-ending/tiebreaking_persistence_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (ResolveEnding algorithm — tie-breaking extends it)
- Unlocks: 存档系统 #7 integration；结局呈现 #20 (consumes UnlockedEndingIds)

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 4/4 passing
**Deviations**: None
**Test Evidence**: `tests/unit/multi-ending/tiebreaking_persistence_test.cs` — 14 test functions
**Code Review**: Pending
