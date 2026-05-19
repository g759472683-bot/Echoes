# Story 004: 存档序列化与恢复

> **Epic**: 记忆变化追踪 (ChangeTracker)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/memory-change-tracking.md`
**Requirement**: `TR-memory-change-tracking-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: SO 不可变 + Overlay
**ADR Decision Summary**: 存档只序列化 overlay（不序列化 base SO）；恢复时 ChangeTracker.Restore(overlay) 重建 _overlay + _flags + 跟踪集合；base SO 通过 Addressables 重新加载

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ContentOverrides 使用 Unity JsonUtility（仅需 [Serializable]）；外层 SaveData 使用 System.Text.Json；_overlay 以 Dictionary 形式序列化

**Control Manifest Rules (Feature Layer)**:
- Required: SaveData only serializes overlay, not base SO — source: ADR-0007
- Forbidden: Never serialize base SO data into save files — source: ADR-0007

---

## Acceptance Criteria

*From GDD `design/gdd/memory-change-tracking.md`, scoped to this story:*

- [ ] GIVEN 存档包含 3 条叠加层条目和 2 个 Flag，WHEN 游戏从存档恢复，THEN _overlay 包含 3 条条目，_flags 包含 2 个 Flag，GetCurrentState 返回合并后的状态。OverlayVersion 重置为已恢复条目数。

- [ ] GIVEN 存档中有一条叠加层条目引用了已在新版本中删除的碎片 ID，WHEN 加载存档，THEN 该条目被标记为孤儿——LogWarning——其他 2 条正常恢复。加载不阻塞。

- [ ] GIVEN ChangeTracker 状态已恢复，WHEN 新选择触发 ApplyChanges，THEN OverlayVersion 从恢复后的值继续递增。_changeLog 从空开始（仅记录新会话的选择）。

---

## Implementation Notes

*Derived from ADR-0007 Implementation Guidelines:*

### 序列化结构

```csharp
[Serializable]
public struct ChangeTrackerSaveData
{
    // _overlay serialized as list of key-value pairs
    public List<OverlayEntry> OverlayEntries;
    
    // _flags serialized as list of key-value pairs
    public List<FlagEntry> Flags;
    
    // Tracking sets
    public string[] VisitedFragments;
    public string[] CompletedChapters;
    
    public int OverlayVersion;
}

[Serializable]
public struct OverlayEntry
{
    public string TargetFragmentId;
    public string ChoiceId;
    public ContentOverrides Overrides;
    public int OrderIndex;
}

[Serializable]
public struct FlagEntry
{
    public string FlagId;
    public bool Value;
}
```

### 保存流程

```csharp
public ChangeTrackerSaveData GetSaveData()
{
    return new ChangeTrackerSaveData
    {
        OverlayEntries = _overlay.Select(kv => new OverlayEntry
        {
            TargetFragmentId = kv.Key.targetFragmentId,
            ChoiceId = kv.Key.choiceId,
            Overrides = kv.Value,
            OrderIndex = /* from ChangeLogEntry */
        }).ToList(),
        Flags = _flags.Select(kv => new FlagEntry { FlagId = kv.Key, Value = kv.Value }).ToList(),
        VisitedFragments = _visitedFragments.ToArray(),
        CompletedChapters = _completedChapters.ToArray(),
        OverlayVersion = OverlayVersion
    };
}
```

### 恢复流程

```csharp
public void Restore(ChangeTrackerSaveData data)
{
    _overlay.Clear();
    _flags.Clear();
    _visitedFragments.Clear();
    _completedChapters.Clear();
    _changeLog.Clear();
    
    foreach (var entry in data.OverlayEntries)
    {
        // Validate fragmentId exists in current registry
        if (!_fragmentRegistry.Contains(entry.TargetFragmentId))
        {
            Debug.LogWarning($"Orphan overlay entry: fragment '{entry.TargetFragmentId}' not found — skipping");
            continue;
        }
        _overlay[(entry.TargetFragmentId, entry.ChoiceId)] = entry.Overrides;
    }
    
    foreach (var flag in data.Flags)
        _flags[flag.FlagId] = flag.Value;
    
    foreach (var fragId in data.VisitedFragments)
        _visitedFragments.Add(fragId);
    
    foreach (var chId in data.CompletedChapters)
        _completedChapters.Add(chId);
    
    OverlayVersion = data.OverlayVersion;
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: _overlay 数据结构定义
- Story 003: _flags 读写接口
- 存档系统 (#7): SaveData 整体聚合、文件 I/O、SHA-256 校验、原子写入——本故事仅提供 ChangeTracker 的序列化/恢复方法
- 跨章节状态追踪 (#16): CrossChapterFlag 的持久化协调

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: 存档恢复基础流程
  - Given: ChangeTrackerSaveData 包含 3 条 OverlayEntries、2 个 Flags、5 个 VisitedFragments、1 个 CompletedChapter、OverlayVersion=3
  - When: 调用 ChangeTracker.Restore(data)
  - Then: _overlay.Count = 3；_flags.Count = 2；_visitedFragments.Count = 5；_completedChapters.Count = 1；OverlayVersion = 3；GetCurrentState 返回合并后状态
  - Edge cases: 空存档数据（新游戏）→ 所有集合为空，OverlayVersion=0，不抛异常

- **AC-2**: 孤儿叠加层条目
  - Given: SaveData 有 3 条 OverlayEntries，其中 1 条的 TargetFragmentId="deleted_frag" 不在当前碎片注册表中
  - When: ChangeTracker.Restore(data)
  - Then: LogWarning 输出 "Orphan overlay entry: fragment 'deleted_frag' not found"；其他 2 条正常恢复；_overlay.Count = 2
  - Edge cases: 所有条目都是孤儿 → _overlay 为空，不阻塞；存档版本完全不兼容 → 在存档系统层处理

- **AC-3**: 恢复后继续游戏
  - Given: ChangeTracker 从存档恢复，OverlayVersion = 5
  - When: 新选择触发 ApplyChanges
  - Then: OverlayVersion 递增到 6；_changeLog 有 1 条新日志（恢复后 _changeLog 被清空）
  - Edge cases: 恢复后立即保存 → GetSaveData 返回 OverlayVersion = 6

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/memory-change-tracking/save_restore_test.cs` — must exist and pass

**Status**: [x] Created — `tests/integration/memory-change-tracking/save_restore_test.cs` (20 tests)

---

## Dependencies

- Depends on: Story 001 (_overlay 数据结构), Story 003 (_flags + 跟踪集合)
- Depends on: 存档系统 (#7) Story (SaveData 聚合规范、序列化格式)
- Unlocks: 完整的游戏存档/读档循环（与存档系统 #7 对接后）

---

## Completion Notes
**Completed**: 2026-05-18
**Criteria**: 3/3 passing
**Deviations**: None
**Test Evidence**: `tests/integration/memory-change-tracking/save_restore_test.cs` (20 tests)
**Code Review**: Lean mode — skipped
