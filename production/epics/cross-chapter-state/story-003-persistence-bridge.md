# Story 003: Flag 持久化桥梁

> **Epic**: 跨章节状态追踪 (CrossChapterTracker)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/cross-chapter-state-tracking.md`
**Requirement**: `TR-cross-chapter-state-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0011: 跨章节状态追踪
**ADR Decision Summary**: GetPersistableFlags returns shallow copy of ChangeTracker._flags filtered to registry entries; RestoreFlags batch-sets via SetFlagRaw. CrossChapterTracker does NOT own flag storage — ChangeTracker._flags is the sole source of truth.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Dictionary copy + iteration; SaveData.CrossChapterFlags is Dictionary<string, bool>

**Control Manifest Rules (Feature Layer)**:
- Required: Save/load bridge methods are synchronous — called by SaveManager during its async flow — source: ADR-0003

---

## Acceptance Criteria

*From GDD `design/gdd/cross-chapter-state-tracking.md`, scoped to this story:*

- [ ] GIVEN 游戏中有 Flag "ch1_letter_kept"=true，WHEN SaveManager 调用 GetPersistableFlags，THEN 返回的字典包含 {"ch1_letter_kept": true}，被序列化到 SaveData.CrossChapterFlags。

- [ ] GIVEN 存档中 CrossChapterFlags = {"ch1_letter_kept": true}，WHEN 加载存档并调用 RestoreFlags，THEN ChangeTracker._flags["ch1_letter_kept"] = true。Ch03 中依赖此 Flag 的隐藏结局条件可评估为 true。

- [ ] GIVEN 存档中的 Flag 在注册表中不存在（旧存档，Flag 被重构），WHEN RestoreFlags 执行，THEN 注册表中存在的 Flag 正常恢复；存档中多余的 Flag 记录 Warning 但不阻塞加载。新增的注册表 Flag（存档中无）使用 DefaultValue。

- [ ] GIVEN GetPersistableFlags 返回的字典，WHEN 存档后加载回来，THEN Flag 值完全一致（round-trip 测试 —— 10 个随机 Flag → 保存 → 恢复 → 所有值匹配）。

---

## Implementation Notes

*Derived from ADR-0011 Implementation Guidelines + GDD rule 3:*

### GetPersistableFlags (Save)

```csharp
public Dictionary<string, bool> GetPersistableFlags()
{
    var allFlags = _changeTracker.GetAllFlags(); // Shallow copy of _flags
    
    var result = new Dictionary<string, bool>();
    foreach (var def in _registry.Flags)
    {
        if (allFlags.TryGetValue(def.FlagId, out bool value))
            result[def.FlagId] = value;
        else
            result[def.FlagId] = def.DefaultValue; // Not yet set — use default
    }
    
    return result;
}
```

Only persists flags registered in CrossChapterFlagRegistry — not chapter-local temporary flags.

### RestoreFlags (Load)

```csharp
public void RestoreFlags(Dictionary<string, bool> savedFlags)
{
    if (savedFlags == null) return;
    
    // Restore all flags present in save
    foreach (var kv in savedFlags)
    {
        if (_registry.Flags.Any(f => f.FlagId == kv.Key))
        {
            _changeTracker.SetFlagRaw(kv.Key, kv.Value);
        }
        else
        {
            // Orphan flag from old save (flag was renamed/removed in newer build)
            Debug.LogWarning(
                $"Saved flag '{kv.Key}' not found in CrossChapterFlagRegistry — " +
                $"value preserved in _flags but not tracked by registry");
            // Still restore it — it may be consumed by conditions even without registry entry
            _changeTracker.SetFlagRaw(kv.Key, kv.Value);
        }
    }
    
    // Flags in registry but NOT in save → set to DefaultValue (new flags added after save)
    foreach (var def in _registry.Flags)
    {
        if (!savedFlags.ContainsKey(def.FlagId))
        {
            _changeTracker.SetFlagRaw(def.FlagId, def.DefaultValue);
        }
    }
}
```

### SaveData Integration

```csharp
// In SaveData (save-load-system #7):
public Dictionary<string, bool> CrossChapterFlags;

// SaveManager.CollectSaveData:
saveData.CrossChapterFlags = _crossChapterTracker.GetPersistableFlags();

// SaveManager.RestoreFromSave:
_crossChapterTracker.RestoreFlags(saveData.CrossChapterFlags);
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: SetFlagRaw internal interface implementation
- Story 002: IsImmutable protection (RestoreFlags bypasses IsImmutable checks — save data loading is authoritative)
- 存档系统 (#7): SaveData structure, serialization, file I/O, checksum — this story only provides the bridge methods
- 变化追踪 (#12): GetAllFlags implementation

---

## QA Test Cases

- **AC-1**: Save collects current flag values
  - Given: _flags["ch1_letter_kept"]=true, _flags["ch2_secret"]=false; registry defines both
  - When: GetPersistableFlags() called
  - Then: Returns {"ch1_letter_kept": true, "ch2_secret": false}
  - Edge cases: Flag in registry not yet in _flags → returns DefaultValue

- **AC-2**: Load restores flags correctly
  - Given: SaveData.CrossChapterFlags = {"ch1_letter_kept": true}
  - When: RestoreFlags(savedFlags) called
  - Then: _flags["ch1_letter_kept"] = true; ConditionGroup referencing FlagSet("ch1_letter_kept", true) evaluates true
  - Edge cases: Empty savedFlags → all registry flags set to DefaultValue

- **AC-3**: Orphan flag handling
  - Given: SaveData.CrossChapterFlags contains "old_flag" (removed from registry in current build); registry now has "new_flag" instead
  - When: RestoreFlags(savedFlags) called
  - Then: "old_flag" still set in _flags (preserved, Warn logged); "new_flag" set to DefaultValue (not in save)
  - Edge cases: All flags in save are orphans → all restored with warnings, all current registry flags get defaults

- **AC-4**: Round-trip fidelity
  - Given: 10 flags with random boolean values set in _flags
  - When: GetPersistableFlags() → RestoreFlags(result) round-trip
  - Then: All 10 flag values match original
  - Edge cases: Zero flags → empty dictionary round-trips correctly

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/cross-chapter-state/persistence_bridge_test.cs` — must exist and pass

**Status**: [x] Created — tests/integration/cross-chapter-state/persistence_bridge_test.cs (11 tests, all passing)

---

## Dependencies

- Depends on: Story 001 (registry + SetFlagRaw + GetAllFlags)；Story 002 (IsImmutable — restore bypasses it but implementation should be aware)；存档系统 Story 003 (SaveData.CrossChapterFields collection)
- Unlocks: 多结局 Story 004 (hidden ending cross-chapter conditions depend on restored flags)

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 4/4 passing
**Deviations**: None
**Test Evidence**: tests/integration/cross-chapter-state/persistence_bridge_test.cs (11 tests)
**Code Review**: Skipped (Lean mode)
**Files**:
- src/core/CrossChapterTracker.cs — GetPersistableFlags() + RestoreFlags() methods
