# Story 002: IsImmutable 保护 + 章节重玩 Flag 生命周期

> **Epic**: 跨章节状态追踪 (CrossChapterTracker)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/cross-chapter-state-tracking.md`
**Requirement**: `TR-cross-chapter-state-002`, `TR-cross-chapter-state-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0011: 跨章节状态追踪
**ADR Decision Summary**: IsImmutable=true flags reject SetFlag(false) with Warning log — permanently locked once set to true. Non-IsImmutable flags reset to DefaultValue on OnChapterReplayStarted. Protection activated when ChapterManager fires OnChapterReplayStarted event.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Pure C# logic — flag value comparison + conditional SetFlagRaw; OnChapterReplayStarted static event subscription

**Control Manifest Rules (Feature Layer)**:
- Required: Subscribe to OnChapterReplayStarted via static event (ADR-0001) in OnEnable, unsubscribe in OnDisable
- Forbidden: Never silently reset an IsImmutable flag — must LogWarning

---

## Acceptance Criteria

*From GDD `design/gdd/cross-chapter-state-tracking.md`, scoped to this story:*

- [ ] GIVEN Flag "ch1_letter_kept" 的 IsImmutable=true 且当前值为 true，WHEN 玩家重玩 Ch01 并做出不同选择（触发 SetFlag("ch1_letter_kept", false)），THEN SetFlag(false) 被拒绝。Flag 保持 true。LogWarning 记录。

- [ ] GIVEN 玩家完成 Ch01 并重玩 Ch01（通过章节选择），WHEN 重玩入口 OnChapterReplayStarted 触发，THEN IsImmutable Flag 的保护逻辑激活。非 IsImmutable Flag 可在重玩中被自由修改。

- [ ] GIVEN IsImmutable=false 的 Flag "ch1_window_opened"=true，WHEN OnChapterReplayStarted("ch01") 触发，THEN 该 Flag 被重置为 DefaultValue（false）。章节重玩中的新选择可重新设置为 true。

- [ ] GIVEN 同一章节的 IsImmutable Flag 在重玩中，WHEN 玩家做出相同选择再次 SetFlag(id, true)，THEN 幂等操作——Flag 已经是 true，再次设置 true 是安全的（无操作或 LogDebug）。

---

## Implementation Notes

*Derived from ADR-0011 Implementation Guidelines + GDD rules 2, 4:*

### IsImmutable Protection at SetFlag Level

```csharp
// In ChangeTracker or CrossChapterTracker — intercept SetFlag calls for registered flags
public void SetFlag(string flagId, bool value)
{
    var def = _registry.Flags.FirstOrDefault(f => f.FlagId == flagId);
    
    if (def != null && def.IsImmutable)
    {
        bool currentValue = _changeTracker.GetFlag(flagId);
        if (currentValue && !value)
        {
            Debug.LogWarning(
                $"Immutable flag '{flagId}' is already true — SetFlag(false) rejected. " +
                $"This flag was set in {def.SetInChapter}/{def.SetInFragmentId}");
            return;
        }
    }
    
    // Pass through to ChangeTracker
    _changeTracker.SetFlagRaw(flagId, value);
}
```

Alternative approach — intercept at ChangeTracker level: ChangeTracker's `SetFlag` checks IsImmutable before writing. CrossChapterTracker provides the registry lookup. Whichever is cleaner for the specific implementation.

### OnChapterReplayStarted Handler

```csharp
void OnEnable()
{
    ChapterManager.OnChapterReplayStarted += HandleChapterReplayStarted;
}

void OnDisable()
{
    ChapterManager.OnChapterReplayStarted -= HandleChapterReplayStarted;
}

void HandleChapterReplayStarted(string chapterKey)
{
    var flagsInChapter = _registry.Flags
        .Where(f => f.SetInChapter == chapterKey);
    
    foreach (var def in flagsInChapter)
    {
        if (def.IsImmutable)
        {
            // Protection: immutable flags are NOT reset
            // Their current value persists into the replay
            continue;
        }
        
        // Non-immutable: reset to default for fresh replay
        _changeTracker.SetFlagRaw(def.FlagId, def.DefaultValue);
    }
}
```

### Lifecycle

```
New Game:
  InitializeAllFlags() → all flags = DefaultValue

During Chapter (player makes choice):
  ContentChange triggers → SetFlag("ch1_letter_kept", true)
  → _flags["ch1_letter_kept"] = true

IsImmutable guard at SetFlag:
  IF def.IsImmutable AND currentValue == true AND newValue == false:
    → Reject + LogWarning

Chapter Replay (OnChapterReplayStarted):
  IsImmutable=true flags → skip (preserved)
  IsImmutable=false flags → reset to DefaultValue

New Game+ (full restart):
  InitializeAllFlags() → ALL flags re-initialized (including IsImmutable)
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: CrossChapterFlagRegistry SO definition + InitializeAllFlags + SetFlagRaw
- Story 003: GetPersistableFlags / RestoreFlags save/load bridge
- 章节管理 (#15): OnChapterReplayStarted event declaration and firing
- 变化追踪 (#12): SetFlag implementation (only the IsImmutable guard is added here)

---

## QA Test Cases

- **AC-1**: IsImmutable flag rejects SetFlag(false)
  - Given: "ch1_letter_kept" IsImmutable=true, current value=true
  - When: SetFlag("ch1_letter_kept", false) called (player makes different choice in replay)
  - Then: Flag stays true; LogWarning emitted; SetFlag(false) rejected
  - Edge cases: SetFlag("ch1_letter_kept", true) when already true → idempotent, no warning; IsImmutable=false → SetFlag(false) succeeds normally

- **AC-2**: OnChapterReplayStarted activates protection
  - Given: Ch01 completed; "ch1_letter_kept" (IsImmutable=true, value=true); "ch1_window_opened" (IsImmutable=false, value=true)
  - When: OnChapterReplayStarted("ch01") called
  - Then: "ch1_letter_kept" = true (preserved); "ch1_window_opened" reset to DefaultValue (false)
  - Edge cases: Chapter with 0 registered flags → no-op

- **AC-3**: Non-immutable flag resets on replay
  - Given: "ch1_window_opened" IsImmutable=false, DefaultValue=false, current value=true
  - When: OnChapterReplayStarted("ch01") called
  - Then: "ch1_window_opened" = false (reset to DefaultValue)
  - Edge cases: DefaultValue=true → reset to true

- **AC-4**: Idempotent SetFlag(true) on immutable flag
  - Given: "ch1_letter_kept" IsImmutable=true, current value=true
  - When: SetFlag("ch1_letter_kept", true) called again (player makes same choice in replay)
  - Then: No-op or LogDebug; no warning; no exception
  - Edge cases: Rapid double-call → both succeed (idempotent)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/cross-chapter-state/immutable_protection_replay_test.cs` — must exist and pass

**Status**: [x] Created — tests/unit/cross-chapter-state/immutable_protection_replay_test.cs (12 tests, all passing)

---

## Dependencies

- Depends on: Story 001 (registry + SetFlagRaw + InitializeAllFlags)；章节管理 Story 004 (OnChapterReplayStarted event)；变化追踪 Story 003 (GetFlag reads _flags)
- Unlocks: Story 003 (save/load needs to respect IsImmutable after restore)

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 4/4 passing
**Deviations**: None
**Test Evidence**: tests/unit/cross-chapter-state/immutable_protection_replay_test.cs (12 tests)
**Code Review**: Skipped (Lean mode)
**Files**:
- src/core/CrossChapterTracker.cs — IsImmutable guard in IsFlagImmutable callback, HandleChapterReplayStarted handler
- src/core/ChangeTrackerCore.cs — SetFlag() modified to check IsFlagImmutable before true→false transition
