# Story 001: Flag 注册表 + 新游戏初始化 + SetFlagRaw

> **Epic**: 跨章节状态追踪 (CrossChapterTracker)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/cross-chapter-state-tracking.md`
**Requirement**: `TR-cross-chapter-state-001`, `TR-cross-chapter-state-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0011: 跨章节状态追踪
**ADR Decision Summary**: CrossChapterFlagRegistry ScriptableObject (FlagId/SetInChapter/SetInFragmentId/SetByChoiceId/IsImmutable/DefaultValue/ConsumedBy[]); InitializeAllFlags → ChangeTracker.SetFlagRaw 批量设置 DefaultValue; SetFlagRaw 是内部接口——直接写入 _flags[key]=value，不经过 ApplyChanges，不触发 OnOverlayChanged

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: ScriptableObject + Dictionary 操作；SetFlagRaw 内部接口需在 ChangeTracker 中暴露

**Control Manifest Rules (Feature Layer)**:
- Required: CrossChapterFlagRegistry SO 运行时只读 — 遵循 ADR-0007 — source: ADR-0011
- Required: SetFlagRaw 不触发 OnOverlayChanged — Flag 是全局状态，不在 overlay 中

---

## Acceptance Criteria

*From GDD `design/gdd/cross-chapter-state-tracking.md`, scoped to this story:*

- [ ] GIVEN 新游戏启动，WHEN InitializeAllFlags 执行，THEN ChangeTracker._flags 包含注册表中所有 Flag，每个 Flag 的值为其 DefaultValue（通常 false）。

- [ ] GIVEN 玩家在 Ch01 frag_07 中选择 "keep_letter"，该选择的 ContentChange 包含 SetFlag("ch1_letter_kept", true)，WHEN ChangeTracker.ApplyChanges 执行，THEN ChangeTracker._flags["ch1_letter_kept"] = true。

- [ ] GIVEN CrossChapterFlagRegistry 定义了 10 个 Flag，WHEN 游戏运行中 ChangeTracker._flags 包含注册表之外的额外 Flag（如章节内临时 Flag），THEN 系统正常运行——注册表是目录，不是约束。

---

## Implementation Notes

*Derived from ADR-0011 Implementation Guidelines:*

### CrossChapterFlagRegistry SO

```csharp
[CreateAssetMenu(menuName = "Echoes/CrossChapterFlagRegistry")]
public class CrossChapterFlagRegistry : ScriptableObject
{
    public CrossChapterFlagDef[] Flags;
}

[Serializable]
public struct CrossChapterFlagDef
{
    public string FlagId;            // Globally unique, e.g. "ch1_letter_kept"
    public string SetInChapter;      // Which chapter sets it, e.g. "ch01"
    public string SetInFragmentId;   // Which fragment sets it
    public string SetByChoiceId;     // Which choice sets it
    public bool IsImmutable;         // true = once set, never revert
    public bool DefaultValue;        // New game default (usually false)
    public string[] ConsumedBy;      // Which EndingIds / ConditionGroups consume this flag
}
```

Flag naming: `snake_case`, globally unique prefix (e.g., `ch1_`, `ch2_`).

### CrossChapterTracker Class

```csharp
public class CrossChapterTracker
{
    private CrossChapterFlagRegistry _registry;
    private IChangeTrackerInternal _changeTracker; // SetFlagRaw internal interface

    public void InitializeAllFlags()
    {
        foreach (var def in _registry.Flags)
        {
            _changeTracker.SetFlagRaw(def.FlagId, def.DefaultValue);
        }
    }
}
```

### SetFlagRaw Internal Interface

```csharp
// In ChangeTracker (#12) — internal interface, NOT public
internal interface IChangeTrackerInternal
{
    void SetFlagRaw(string flagId, bool value);
}

// Implementation (in ChangeTracker):
void IChangeTrackerInternal.SetFlagRaw(string flagId, bool value)
{
    _flags[flagId] = value;
    // NO OnOverlayChanged event — flags are global state, not overlay
}
```

Key constraints:
- `SetFlagRaw` does NOT trigger `OnOverlayChanged` — flag reads/writes should not cause HUD refreshes
- `SetFlagRaw` writes directly to `_flags[key] = value` — bypasses ApplyChanges flow
- `IChangeTrackerInternal` is an `internal` interface — only CrossChapterTracker calls it

### Architecture

```
CrossChapterFlagRegistry (SO, assets/data/)
        │
        ▼
CrossChapterTracker
  ├── InitializeAllFlags() → SetFlagRaw(id, defaultValue) for ALL flags
  └── _changeTracker (IChangeTrackerInternal)
        │
        ▼
ChangeTracker._flags (shared Dictionary)
  ├── SetFlagRaw(flagId, value) — internal
  ├── GetAllFlags() → Dictionary
  └── GetFlag(flagId) → bool

Same _flags Dictionary contains:
  ├── Chapter-local flags (managed by ChapterManager)
  └── Cross-chapter flags (managed by CrossChapterTracker)
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: IsImmutable Flag 保护 + 章节重玩时的 Flag 生命周期
- Story 003: GetPersistableFlags / RestoreFlags 持久化桥梁
- ChangeTracker (#12) core: _flags Dictionary, ApplyChanges, EvaluateCondition — only SetFlagRaw internal interface is added here
- Editor reference validation (Vertical Slice)
- Flag dependency graph visualization (Full Vision)

---

## QA Test Cases

- **AC-1**: InitializeAllFlags sets default values
  - Given: Registry defines 3 flags: "ch1_letter" (DefaultValue=false), "ch2_secret" (DefaultValue=false), "mentor_alive" (DefaultValue=true)
  - When: InitializeAllFlags() called
  - Then: _flags["ch1_letter"]=false; _flags["ch2_secret"]=false; _flags["mentor_alive"]=true
  - Edge cases: Registry has 0 flags → no-op, no exception; Registry is null → ArgumentNullException

- **AC-2**: SetFlag via ContentChange
  - Given: ContentChange with SetFlag("ch1_letter_kept", true) applied via ApplyChanges
  - When: ApplyChanges executes
  - Then: _flags["ch1_letter_kept"] = true (through normal ChangeTracker flow, NOT SetFlagRaw)
  - Edge cases: FlagId not in registry → still set (registry is directory, not constraint)

- **AC-3**: SetFlagRaw does not fire OnOverlayChanged
  - Given: OnOverlayChanged subscriber is listening
  - When: SetFlagRaw("test_flag", true) called
  - Then: _flags["test_flag"] = true; OnOverlayChanged NOT fired
  - Edge cases: SetFlagRaw called during fragment transition → no interference

- **AC-4**: Registry-independent flag storage
  - Given: _flags contains "ch1_temp" (set by chapter-local logic, NOT in registry)
  - When: InitializeAllFlags() called (new game)
  - Then: "ch1_temp" also initialized if in registry; existing extra flags preserved
  - Edge cases: Flag exists in _flags from previous session → overwritten by InitializeAllFlags

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/cross-chapter-state/flag_registry_initialization_test.cs` — must exist and pass

**Status**: [x] Created — tests/integration/cross-chapter-state/flag_registry_initialization_test.cs (11 tests, all passing)

---

## Dependencies

- Depends on: 记忆变化追踪 Story 001 (ChangeTracker._flags Dictionary)；ADR-0011 accepted
- Unlocks: Story 002 (IsImmutable protection needs initialized flags)；Story 003 (save/load bridge needs SetFlagRaw)

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 4/4 passing
**Deviations**: None
**Test Evidence**: tests/integration/cross-chapter-state/flag_registry_initialization_test.cs (11 tests)
**Code Review**: Skipped (Lean mode)
**Files**:
- src/core/CrossChapterFlagRegistry.cs — ScriptableObject + CrossChapterFlagDef struct
- src/core/IChangeTrackerInternal.cs — internal interface (SetFlagRaw, GetAllFlags, SetImmutableFlagCheck)
- src/core/CrossChapterTracker.cs — InitializeAllFlags() method
- src/core/ChangeTrackerCore.cs — +SetFlagRaw(), +GetAllFlags(), +IsFlagImmutable callback property
- src/core/ChangeTracker.cs — +IChangeTrackerInternal explicit implementation
