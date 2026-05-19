# Story 002: 章节完成检测

> **Epic**: 章节管理 (ChapterManager)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/chapter-management.md`
**Requirement**: `TR-chapter-management-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0010 (多结局判定)
**ADR Decision Summary**: Chapter completion detection uses dual condition: (A) all fragments visited OR (B) visitedRatio ≥ CompletionRatio AND best candidate score < COMPLETION_ASSOCIATION_THRESHOLD (0.30)

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Pure C# logic — no engine APIs in completion formula

**Control Manifest Rules (Feature Layer)**:
- Required: Completion detection runs after every TransitionToFragment — no polling — source: ADR-0010

---

## Acceptance Criteria

*From GDD `design/gdd/chapter-management.md`, scoped to this story:*

- [ ] GIVEN 玩家在 Ch01 中访问了 8/10 碎片 (CompletionRatio=0.6, 已满足 ≥6)，且关联引擎返回的最佳候选 compositeScore = 0.15 (<0.30)，WHEN TransitionToFragment 完成，THEN CheckChapterCompletion 返回 true → 章节完成流程启动。

- [ ] GIVEN 玩家访问了 10/10 碎片（全部），WHEN TransitionToFragment 完成，THEN CheckChapterCompletion 返回 true（条件 A：全部访问）。

- [ ] GIVEN 玩家访问了 6/10 碎片（满足 CompletionRatio=0.6），但最佳候选 compositeScore = 0.45 (≥0.30)，WHEN TransitionToFragment 完成，THEN 章节未完成——玩家仍需继续探索。

- [ ] GIVEN 章节中碎片总数为 0（配置错误），WHEN EnterChapter 执行，THEN Error 记录，立即触发章节完成（直接过渡到下一章或结束）。

---

## Implementation Notes

*Derived from GDD rule 6 + Formulas section:*

### Completion Detection Formula

```csharp
bool CheckChapterCompletion(string chapterKey)
{
    int visitedCount = _chapterVisitedFragments.Count;
    int totalFragments = _dataManager.GetFragmentsByChapter(chapterKey).Count;
    
    if (totalFragments == 0)
    {
        Debug.LogError($"Chapter {chapterKey} has 0 fragments — auto-completing");
        return true;
    }
    
    // Condition A: All fragments visited
    if (visitedCount >= totalFragments)
        return true;
    
    // Condition B: Ratio met + association threshold
    float visitedRatio = (float)visitedCount / totalFragments;
    var chapterDef = GetChapterDefinition(chapterKey);
    
    if (visitedRatio >= chapterDef.CompletionRatio)
    {
        var candidates = _associationEngine.ComputeAssociations(
            CurrentFragmentId, chapterKey, _recentHistory, _sessionVisitedFragments);
        
        if (candidates.Count == 0)
            return true; // No candidates — chapter naturally exhausted
        
        float bestScore = candidates[0].CompositeScore;
        if (bestScore < COMPLETION_ASSOCIATION_THRESHOLD)
            return true;
    }
    
    return false;
}
```

Constants:
- `COMPLETION_ASSOCIATION_THRESHOLD = 0.30` (Tuning Knob: 0.20–0.50)
- `CompletionRatio` ∈ [0.0, 1.0], per-chapter default 0.6

### Edge Cases

- `CompletionRatio = 1.0` and one fragment permanently unreachable (ConditionGroup never satisfied): Condition B.2 acts as safety valve — when best candidate < 0.30, completion still triggers. If association engine returns 0 candidates → auto-complete.
- `totalFragments = 0`: configuration error → log Error, trigger chapter completion immediately.
- Detection runs automatically after every `TransitionToFragment` — no menu prompt, no "Are you sure?" dialog.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: 章节完成过渡流程（ResolveEnding → auto_save → transition → OnChapterStarted）
- Story 004: 章节重玩 + 线性解锁
- 关联引擎 (#13): ComputeAssociations returns AssociationCandidate[] with CompositeScore
- `CompletionRatio` per-chapter override via ChapterDefinition SO Inspector

---

## QA Test Cases

- **AC-1**: Completion via ratio + threshold
  - Given: Ch01 has 10 fragments, CompletionRatio=0.6, visited=8, bestCandidateScore=0.15
  - When: CheckChapterCompletion("ch01") called
  - Then: Returns true (condition B satisfied)
  - Edge cases: visited=6 exactly at ratio boundary → passing; visited=6 but score=0.30 exactly → NOT passing (threshold is strict less-than)

- **AC-2**: Completion via all-visited
  - Given: Ch01 has 10 fragments, visited=10
  - When: CheckChapterCompletion("ch01") called
  - Then: Returns true (condition A satisfied) regardless of association score
  - Edge cases: CompletionRatio=1.0 but all visited → still passes via condition A

- **AC-3**: Not complete — score above threshold
  - Given: Ch01 has 10 fragments, CompletionRatio=0.6, visited=6 (ratio met), bestCandidateScore=0.45
  - When: CheckChapterCompletion("ch01") called
  - Then: Returns false — player must explore more
  - Edge cases: visited=5 (ratio=0.5 < 0.6) → returns false regardless of score

- **AC-4**: Empty chapter
  - Given: Ch01 has 0 fragments (misconfiguration)
  - When: EnterChapter("ch01") called
  - Then: Debug.LogError emitted; chapter completion triggered immediately
  - Edge cases: Empty chapter with no next chapter → OnAllChaptersCompleted fires

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/chapter-management/completion_detection_test.cs` — must exist and pass

**Status**: [x] Created — `tests/unit/chapter-management/completion_detection_test.cs` (8 tests)

---

## Dependencies

- Depends on: Story 001 (state machine + navigation must exist)；关联引擎 Story 004 (ComputeAssociations returns CompositeScore)；数据管理 Story 001 (GetFragmentsByChapter)
- Unlocks: Story 003 (completion transition flow triggers from this detection)

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 4/4 passing
**Deviations**: None
**Implementation**: `src/core/ChapterManager.cs` (CheckChapterCompletion method)
**Test Evidence**: `tests/unit/chapter-management/completion_detection_test.cs` (8 tests)
**Code Review**: Pending
