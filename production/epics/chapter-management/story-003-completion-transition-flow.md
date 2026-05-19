# Story 003: 章节完成过渡流程

> **Epic**: 章节管理 (ChapterManager)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/chapter-management.md`
**Requirement**: `TR-chapter-management-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0010 (多结局判定), ADR-0001 (事件总线)
**ADR Decision Summary**: 5-step completion flow: ResolveEnding → update tracking (completedChapters, unlockedChapters) → auto_save → TransitionToChapterAsync → OnChapterStarted. If no next chapter → OnAllChaptersCompleted → Ending Presentation (#20).

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: async/await flow; SceneManager handles actual transitions; auto_save must complete before transition

**Control Manifest Rules (Feature Layer)**:
- Required: OnChapterCompleted/OnAllChaptersCompleted static events — source: ADR-0001
- Forbidden: Never skip auto_save before transition — progress loss on crash is unacceptable

---

## Acceptance Criteria

*From GDD `design/gdd/chapter-management.md`, scoped to this story:*

- [ ] GIVEN 章节完成流程启动，WHEN ResolveEnding("ch01") 返回 ending_A，THEN _completedChapters 包含 "ch01"，_unlockedChapters 包含 "ch02"，auto_save 写入，TransitionToChapterAsync("ch02") 被调用。

- [ ] GIVEN 玩家完成 Ch02（MVP 最终章），WHEN 章节完成流程中 GetNextChapter 返回 null，THEN OnAllChaptersCompleted 触发 → 结局呈现 (#20) 接收事件。

- [ ] GIVEN 章节完成检测触发，WHEN 完成流程执行到 Step 3 auto_save，THEN auto_save 必须 await 完成（同步等待）后才进入 Step 4 场景过渡。若 auto_save 失败 → LogError，仍继续过渡（不阻塞玩家）。

- [ ] GIVEN 同一章节被完成两次（重玩后再次完成），WHEN _completedChapters 已有该章节 Key，THEN HashSet Add 操作幂等——不重复触发解锁或 auto_save。新结局通过 UnlockedEndingIds 并集记录。

---

## Implementation Notes

*Derived from GDD rule 7:*

### 5-Step Completion Flow

```csharp
async Task ExecuteChapterCompletion(string chapterKey)
{
    // Step 1: Resolve ending
    ResolvedEnding ending = _multiEndingSystem.ResolveEnding(chapterKey);
    
    // Step 2: Update progression state
    _completedChapters.Add(chapterKey); // HashSet — idempotent
    
    string nextChapterKey = GetNextChapter(chapterKey);
    if (nextChapterKey != null)
        _unlockedChapters.Add(nextChapterKey); // Union semantics
    
    // Step 3: Auto-save (MUST complete before transition)
    try
    {
        await _saveManager.SaveAsync("auto_save");
    }
    catch (Exception e)
    {
        Debug.LogError($"Auto-save failed during chapter completion: {e.Message}");
        // Continue — don't block the player
    }
    
    // Fire OnChapterCompleted event
    OnChapterCompleted?.Invoke(chapterKey);
    
    // Step 4: Chapter transition (if next chapter exists)
    if (nextChapterKey != null)
    {
        CurrentState = ChapterState.Transitioning;
        await _sceneManager.TransitionToChapterAsync(nextChapterKey);
        CurrentChapterKey = nextChapterKey;
        CurrentFragmentId = GetChapterDefinition(nextChapterKey).EntryFragmentId;
        ClearChapterSessionState();
        CurrentState = ChapterState.InChapter;
        OnChapterStarted?.Invoke(nextChapterKey);
    }
    else
    {
        // Step 5: All chapters completed
        CurrentState = ChapterState.Transitioning;
        OnAllChaptersCompleted?.Invoke();
        // Ending Presentation (#20) picks up this event
    }
}

void ClearChapterSessionState()
{
    _chapterVisitedFragments.Clear();
    _recentHistory.Clear();
    _preloadNotYetTriggered = false;
}
```

### Static Events

```csharp
public static event Action<string> OnChapterStarted;
public static event Action<string> OnChapterCompleted;
public static event Action<string, string> OnFragmentChanged;
public static event Action OnAllChaptersCompleted;
```

### SaveManager auto-save call

Auto-save uses `SaveAsync("auto_save")` — fire-and-forget is NOT acceptable here. Must await before transition because if crash occurs during transition, progress is already persisted (saved after Step 3).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: ChapterManager skeleton + fragment navigation
- Story 002: Chapter completion detection (the trigger for this flow)
- Story 004: Chapter replay + linear unlock setup
- 多结局 (#14): ResolveEnding implementation
- 场景管理 (#6): TransitionToChapterAsync physical execution
- 存档 (#7): SaveAsync implementation
- 结局呈现 (#20): Consumes OnAllChaptersCompleted

---

## QA Test Cases

- **AC-1**: Normal completion flow (Ch01 → Ch02)
  - Given: CheckChapterCompletion returns true for Ch01; ResolveEnding returns ending_A; Ch02 exists
  - When: ExecuteChapterCompletion("ch01") called
  - Then: _completedChapters contains "ch01"; _unlockedChapters contains "ch02"; auto_save("auto_save") called and awaited; TransitionToChapterAsync("ch02") called; OnChapterStarted("ch02") fired
  - Edge cases: ResolveEnding throws → exception propagates (don't silently swallow)

- **AC-2**: Final chapter completion (Ch02, no next chapter)
  - Given: CheckChapterCompletion returns true for Ch02; GetNextChapter returns null
  - When: ExecuteChapterCompletion("ch02") called
  - Then: OnAllChaptersCompleted fired; no TransitionToChapterAsync called; CurrentState = TRANSITIONING
  - Edge cases: Only 1 chapter in build (MVP minimum has 2)

- **AC-3**: Auto-save failure during completion
  - Given: auto_save throws IOException (disk full)
  - When: ExecuteChapterCompletion step 3
  - Then: LogError emitted; completion continues to step 4 (doesn't block player)
  - Edge cases: SaveAsync succeeds but takes 5s (large save) → player sees fade-out transition longer

- **AC-4**: Idempotent re-completion
  - Given: _completedChapters already contains "ch01"; player replayed and re-completed
  - When: ExecuteChapterCompletion("ch01") called again
  - Then: HashSet Add is idempotent; _unlockedChapters Add for "ch02" is idempotent; auto_save still runs; OnChapterCompleted fires again
  - Edge cases: Third re-completion → all operations idempotent

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/chapter-management/completion_transition_flow_test.cs` — must exist and pass

**Status**: [x] Created — `tests/integration/chapter-management/completion_transition_flow_test.cs` (6 tests)

---

## Dependencies

- Depends on: Story 001 (state machine + save/load bridge)；Story 002 (completion detection)；多结局 Story 003 (ResolveEnding returns ResolvedEnding)；存档 Story 003 (SaveAsync)；场景管理 Story 003 (TransitionToChapterAsync)
- Unlocks: HUD Story 001 (OnChapterStarted → HUD update)；结局呈现 (#20) (OnAllChaptersCompleted)

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 4/4 passing
**Deviations**: None
**Implementation**: `src/core/ChapterManager.cs` (ExecuteChapterCompletion method)
**Test Evidence**: `tests/integration/chapter-management/completion_transition_flow_test.cs` (6 tests)
**Code Review**: Pending
