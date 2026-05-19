# Story 002: 原子文件 I/O + 3 槽位管理

> **Epic**: 存档系统 (SaveManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/save-load-system.md`
**Requirement**: `TR-save-load-system-001`, `TR-save-load-system-003`, `TR-save-load-system-007`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0003: 存档序列化格式与版本迁移策略
**ADR Decision Summary**: JSON + SHA-256 + 原子写入 (.tmp → .sav) + 3 槽位 (save_01/save_02/auto_save) + `Application.persistentDataPath`

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `Application.persistentDataPath` 在 Windows 上指向 `%USERPROFILE%\AppData\LocalLow\[company]\[product]`

**Control Manifest Rules (Foundation Layer)**:
- Required: JSON + SHA-256 checksum for save files — atomic write (.tmp → .sav)
- Required: `static event Action<T>` for all cross-system communication
- Guardrail: Save file write: <200ms (JSON serialize + SHA-256 + atomic write)

---

## Acceptance Criteria

*From GDD `design/gdd/save-load-system.md`, scoped to this story:*

- [ ] 3 个存档槽位: `save_01.sav`, `save_02.sav`, `auto_save.sav` → `Application.persistentDataPath/Saves/[slot_id].sav`
- [ ] GIVEN 游戏进行中，WHEN 调用 `SaveAsync("save_01", saveData)`，THEN 存档文件写入。校验和字段非空。保存时间 < 200ms
- [ ] 原子写入：序列化到 `.sav.tmp` → `File.Move(tmp, final, overwrite: true)` → 崩溃/断电时旧存档完好
- [ ] GIVEN 保存进行中 (Saving 状态)，WHEN 再次触发保存或加载，THEN 操作被忽略——不并发执行
- [ ] 保存失败处理：磁盘满 → IOException 捕获 + "磁盘空间不足"提示；权限错误 → UnauthorizedAccessException 捕获 + "无法写入存档"提示
- [ ] `GetSlotMetadata(string slotId)` 快速扫描槽位（读取 Timestamp + PlayTimeSeconds 仅反序列化 SaveData 的前两个字段）——供主菜单显示存档列表

---

## Implementation Notes

*Derived from ADR-0003:*

文件路径:
```csharp
private static string SaveDirectory => 
    Path.Combine(Application.persistentDataPath, "Saves");

private string GetSlotPath(string slotId) => 
    Path.Combine(SaveDirectory, $"{slotId}.sav");
```

原子写入:
```csharp
public async Task SaveAsync(string slotId, SaveData saveData)
{
    if (_currentState != SaveState.Idle)
    {
        Debug.LogWarning($"Save/Load in progress, ignoring SaveAsync({slotId})");
        return; // 并发防护
    }
    
    _currentState = SaveState.Saving;
    
    try
    {
        // 1. 计算校验和
        saveData.Checksum = SaveDataChecksum.ComputeChecksum(saveData);
        
        // 2. 序列化
        var json = JsonSerializer.Serialize(saveData, _jsonOptions);
        var tmpPath = GetSlotPath(slotId) + ".tmp";
        var finalPath = GetSlotPath(slotId);
        
        // 3. 原子写入
        Directory.CreateDirectory(SaveDirectory);
        await File.WriteAllTextAsync(tmpPath, json);
        File.Move(tmpPath, finalPath, overwrite: true);
        
        // 4. 更新槽位元数据缓存
        _slotMetadata[slotId] = new SlotMetadata
        {
            Timestamp = saveData.Timestamp,
            PlayTimeSeconds = saveData.PlayTimeSeconds
        };
    }
    catch (IOException ex)
    {
        Debug.LogError($"Save failed: {ex.Message}");
        throw new SaveException("disk_full_or_permission", ex);
    }
    finally
    {
        _currentState = SaveState.Idle;
    }
}
```

快速槽位扫描:
```csharp
public SlotMetadata GetSlotMetadata(string slotId)
{
    var path = GetSlotPath(slotId);
    if (!File.Exists(path)) return SlotMetadata.Empty;
    
    // 仅反序列化部分字段 (快——不解析完整 SaveData)
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    var root = doc.RootElement;
    return new SlotMetadata
    {
        Timestamp = root.GetProperty("Timestamp").GetString(),
        PlayTimeSeconds = root.GetProperty("PlayTimeSeconds").GetInt32(),
        CurrentChapterKey = root.GetProperty("CurrentChapterKey").GetString()
    };
}
```

并发防护: 使用 `SaveState` 枚举 (Idle/Saving/Loading/Error)——操作前检查 `_currentState == Idle`，否则忽略或返回错误。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: SaveData struct + SHA-256 Checksum 计算
- Story 003: 收集/恢复编排 — CollectSaveData, RestoreSaveData, 版本迁移
- Story 004: 自动存档引擎 — 何时触发保存
- 存档 UI — 由 Main Menu (#19) 负责

---

## QA Test Cases

- **AC-1**: 保存文件写入 < 200ms
  - Given: SaveData 已填充 200KB 测试数据 (模拟全量存档)
  - When: 调用 `SaveAsync("save_01", saveData)` 并 await
  - Then: Task 在 200ms 内完成；`save_01.sav` 文件存在于 `Saves/` 目录
  - Edge cases: 空 ChangeOverlay/CrossChapterFlags → 仍正常保存；Disk 写入慢 (HDD) → 仍应在 500ms 内

- **AC-2**: 原子写入保护现有存档
  - Given: `save_01.sav` 已存在且内容有效
  - When: 保存过程中在 `File.Move` 之前进程崩溃（模拟：跳过 Move）
  - Then: `save_01.sav` 保持旧内容完整；`.tmp` 文件残留（可清理）
  - Edge cases: `.tmp` 文件已存在 → 先删除旧 .tmp 再写入新 .tmp

- **AC-3**: 并发操作被忽略
  - Given: 当前状态 = Saving
  - When: 调用 `SaveAsync` 或 `LoadAsync`
  - Then: 操作被忽略；Debug.LogWarning 记录；不抛出异常
  - Edge cases: Saving 完成后状态恢复 Idle → 新请求可被处理

- **AC-4**: 磁盘满 → IOException 处理
  - Given: 模拟磁盘空间不足
  - When: `SaveAsync` 中 `WriteAllTextAsync` 抛出 IOException
  - Then: 捕获 IOException；旧存档文件不受影响；抛出 SaveException("disk_full")
  - Edge cases: .tmp 文件可能部分写入 → finally 块清理 .tmp

- **AC-5**: GetSlotMetadata 快速扫描
  - Given: `save_01.sav` 包含完整的 200KB SaveData
  - When: 调用 `GetSlotMetadata("save_01")`
  - Then: 返回 SlotMetadata (Timestamp, PlayTimeSeconds, CurrentChapterKey)；不反序列化完整 SaveData
  - Edge cases: 文件不存在 → 返回 SlotMetadata.Empty

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/save-load-system/file_io_test.cs` — must exist and pass

**Status**: [x] Created (20 test functions, all 5 ACs covered)

---

## Dependencies

- Depends on: Story 001 (SaveData struct + Checksum) — Complete
- Unlocks: Story 003 (收集/恢复编排), Story 004 (自动存档)

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 5/5 passing — all auto-verified via unit tests
**Deviations**: None
**Test Evidence**: Logic — `tests/unit/save-load-system/file_io_test.cs` (20 test functions)
**Code Review**: APPROVED (3 bugs found + fixed during review: HasAnySave IFileAccess bypass, .tmp cleanup on error, DeleteSave state guard)
**Engine Notes**: LoadAsync is sync wrapper (ReadAllText + Task.FromResult) — acceptable for small saves; OnApplicationQuit sync path deferred to S004
