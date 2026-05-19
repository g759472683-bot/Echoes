# Story 004: 章节重玩 + 线性解锁

> **Epic**: 章节管理 (ChapterManager)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/chapter-management.md`
**Requirement**: `TR-chapter-management-004`, `TR-chapter-management-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0011 (跨章节状态追踪)
**ADR Decision Summary**: Chapter replay preserves overlay + flags (persistent) but resets visit records, recent history, and preload trigger (session state). Linear chapter unlock: new game = OrderIndex=0 only; completing N → permanently unlock N+1 (union semantics).

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Pure C# HashSet operations; OnChapterReplayStarted event triggers CrossChapterTracker IsImmutable protection

**Control Manifest Rules (Feature Layer)**:
- Required: Static event OnChapterReplayStarted — consumed by CrossChapterTracker — source: ADR-0011

---

## Acceptance Criteria

*From GDD `design/gdd/chapter-management.md`, scoped to this story:*

- [ ] GIVEN 玩家已完成 Ch01，WHEN 从章节选择 ReplayChapter("ch01")，THEN EntryFragmentId 被加载，_chapterVisitedFragments 清空，_recentHistory 清空，但 ChangeTracker 的 overlay 和 flags 保留。玩家可以走出不同的关联路径。

- [ ] GIVEN 新游戏启动，WHEN ChapterManager 初始化，THEN 仅 OrderIndex=0 的章节 (Ch01) 在 _unlockedChapters 中。Ch02 不在 _unlockedChapters 中。

- [ ] GIVEN 玩家完成 Ch01，WHEN ChapterManager 更新 _unlockedChapters，THEN Ch02 被添加（并集——永久解锁）。重玩 Ch01 并再次完成后，Ch02 保持解锁。

- [ ] GIVEN 章节重玩入口，WHEN ReplayChapter 被调用，THEN OnChapterReplayStarted 事件触发 → CrossChapterTracker 激活 IsImmutable Flag 保护。非 IsImmutable Flag 可被重玩中的新选择自由修改。

---

## Implementation Notes

*Derived from GDD rules 8-9 + ADR-0011:*

### Chapter Replay

```csharp
public async Task ReplayChapter(string chapterKey)
{
    var chapterDef = GetChapterDefinition(chapterKey);
    
    // Validate
    if (!_completedChapters.Contains(chapterKey))
    {
        Debug.LogWarning($"Cannot replay incomplete chapter: {chapterKey}");
        return;
    }
    if (!chapterDef.AllowReplay)
    {
        Debug.LogWarning($"Chapter {chapterKey} does not allow replay");
        return;
    }
    
    // Fire event BEFORE clearing state — CrossChapterTracker needs to see current flags
    OnChapterReplayStarted?.Invoke(chapterKey);
    
    // Reset session state (NOT overlay/flags — those persist)
    _chapterVisitedFragments.Clear();
    _sessionVisitedFragments = new HashSet<string>(); // Fresh set for discovery boost
    _recentHistory.Clear();
    _preloadNotYetTriggered = false; // No preload on replay
    
    // Load entry fragment
    CurrentState = ChapterState.Transitioning;
    await _sceneManager.TransitionToFragmentAsync(chapterKey, chapterDef.EntryFragmentId);
    CurrentChapterKey = chapterKey;
    CurrentFragmentId = chapterDef.EntryFragmentId;
    CurrentState = ChapterState.InChapter;
    
    OnChapterStarted?.Invoke(chapterKey);
}

public static event Action<string> OnChapterReplayStarted;
```

What's PRESERVED (persistent):
- ChangeTracker._overlay — visual changes from previous playthroughs retained
- ChangeTracker._flags — global narrative flags retained
- _completedChapters, _unlockedChapters
- UnlockedEndingIds (union)

What's RESET (this replay session):
- _chapterVisitedFragments: cleared — fresh completion tracking
- _sessionVisitedFragments: new HashSet — discovery boost D=1.30 still applies
- CurrentFragmentId: set to EntryFragmentId
- _recentHistory: cleared
- _preloadNotYetTriggered: false

### Linear Chapter Unlock

```csharp
// New Game
void StartNewGame()
{
    var chapters = _dataManager.GetAllChapters().OrderBy(c => c.OrderIndex).ToList();
    _unlockedChapters = new HashSet<string> { chapters[0].ChapterKey };
    // ...
}

// On chapter completion (called from Story 003 flow)
string GetNextChapter(string currentChapterKey)
{
    var current = GetChapterDefinition(currentChapterKey);
    return _dataManager.GetAllChapters()
        .Where(c => c.OrderIndex == current.OrderIndex + 1)
        .Select(c => c.ChapterKey)
        .FirstOrDefault();
}
```

Union semantics: `_unlockedChapters.Add(nextChapterKey)` — permanent unlock, never removed.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: ChapterManager skeleton, _unlockedChapters HashSet definition
- Story 003: Chapter completion flow where _unlockedChapters is updated
- 跨章节状态追踪 (#16): IsImmutable protection logic — triggered by OnChapterReplayStarted event this story fires
- 章节选择界面 (#21, Vertical Slice): ReplayChapter UI entry point
- AllowReplay=false enforcement (all chapters allow replay in MVP)

---

## QA Test Cases

- **AC-1**: Replay resets session, preserves overlay/flags
  - Given: Ch01 completed; overlay contains frag_03 changes; flag "ch1_letter_kept"=true
  - When: ReplayChapter("ch01") called
  - Then: _chapterVisitedFragments empty; _recentHistory empty; _preloadNotYetTriggered=false; EntryFragmentId loaded; ChangeTracker._overlay preserved; ChangeTracker._flags preserved; OnChapterReplayStarted fired
  - Edge cases: Replay incomplete chapter → LogWarning, no-op; AllowReplay=false → LogWarning, no-op

- **AC-2**: New game unlocks only first chapter
  - Given: 2 chapters exist (OrderIndex 0, 1)
  - When: StartNewGame() called
  - Then: _unlockedChapters = {"ch01"} only; Ch02 NOT unlocked
  - Edge cases: Only 1 chapter exists → _unlockedChapters = {single chapter}

- **AC-3**: Linear unlock on completion
  - Given: Ch01 completed
  - When: _unlockedChapters updated in completion flow
  - Then: _unlockedChapters = {"ch01"} (no change because ch01 was already unlocked when game started?) — wait, unlocked is about playing, not about whether you've completed it. The FIRST chapter is unlocked on new game. Completing Ch01 unlocks Ch02. So _unlockedChapters should contain both after completion.
  - Edge cases: Replay Ch01 and complete again → Ch02 already in _unlockedChapters, HashSet Add is idempotent

- **AC-4**: Replay fires OnChapterReplayStarted
  - Given: Ch01 completed; CrossChapterTracker subscribed to OnChapterReplayStarted
  - When: ReplayChapter("ch01") called
  - Then: OnChapterReplayStarted("ch01") invoked BEFORE state reset; CrossChapterTracker receives event and activates IsImmutable protection
  - Edge cases: No subscribers → event is null-checked, no exception

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/chapter-management/replay_unlock_test.cs` — must exist and pass

**Status**: [x] Created — `tests/unit/chapter-management/replay_unlock_test.cs` (9 tests)

---

## Dependencies

- Depends on: Story 001 (state machine + _unlockedChapters HashSet)；Story 003 (completion flow updates _unlockedChapters)；跨章节状态追踪 Story 002 (OnChapterReplayStarted subscriber)
- Unlocks: 章节选择界面 (#21, Vertical Slice) (ReplayChapter + GetUnlockedChapters)

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 4/4 passing
**Deviations**: None
**Implementation**: `src/core/ChapterManager.cs` (ReplayChapter + GetNextChapterKey methods)
**Test Evidence**: `tests/unit/chapter-management/replay_unlock_test.cs` (9 tests)
**Code Review**: Pending
