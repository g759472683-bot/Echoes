# Story 003: 收集/恢复编排 + 版本迁移

> **Epic**: 存档系统 (SaveManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/save-load-system.md`
**Requirement**: `TR-save-load-system-004`, `TR-save-load-system-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0003: 存档序列化格式与版本迁移策略, ADR-0001: 事件总线架构
**ADR Decision Summary**: SaveData 聚合 7 系统状态 + 版本迁移链 (Migrate_V1_to_V2 → Migrate_V2_to_V3 → ...) + 加载后分发恢复

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `System.Text.Json` 序列化 Dictionary 在 IL2CPP 中需验证

**Control Manifest Rules (Foundation Layer)**:
- Required: JSON + SHA-256 checksum for save files — version migration chain
- Required: SaveData aggregates: chapter/fragment progress, ChangeOverlay, CrossChapterFlags, volume settings, locale, unlocked endings
- Forbidden: Never serialize base SO data into save files — save only overlay + flags
- Forbidden: Never pass MonoBehaviour references through events — use value types, string IDs

---

## Acceptance Criteria

*From GDD `design/gdd/save-load-system.md`, scoped to this story:*

- [ ] `CollectSaveData()` 从 7 个系统收集全部运行时状态并填充 SaveData struct
- [ ] GIVEN 槽 1 有存档，WHEN 玩家从主菜单加载槽 1，THEN 恢复到章节 1 碎片 3，语言和音量设置与保存时一致
- [ ] `RestoreSaveData(SaveData data)` 将状态分发到各系统：Localization.SetLocale, Audio.SetVolume, ChangeTracker.RestoreFromSave, CrossChapterTracker.RestoreFromSave, EndingTracker.RestoreFromSave, ChapterManager.LoadAndRestore
- [ ] GIVEN 存档格式版本为 1、游戏版本要求版本 2，WHEN 加载版本 1 存档，THEN 执行版本迁移——迁移后数据正确，下次保存写为版本 2 格式
- [ ] 版本迁移函数链：`Migrate_V1_to_V2` → `Migrate_V2_to_V3` → ... 逐步执行——任意步骤失败时显示"存档与新版本不兼容"

---

## Implementation Notes

*Derived from ADR-0003:*

CollectSaveData:
```csharp
public SaveData CollectSaveData()
{
    var data = new SaveData
    {
        Version = CURRENT_SAVE_VERSION,
        Timestamp = DateTime.UtcNow.ToString("O"),
        LocaleCode = _localizationManager.GetCurrentLocaleCode(),
        PlayTimeSeconds = _playTimeTracker.ElapsedSeconds,
        
        // Chapter progress (from ChapterManager #15)
        CurrentChapterKey = _chapterManager.CurrentChapterKey,
        CurrentFragmentId = _chapterManager.CurrentFragmentId,
        CurrentFragmentIndex = _chapterManager.CurrentFragmentIndex,
        CompletedChapters = _chapterManager.GetCompletedChapters(),
        UnlockedChapters = _chapterManager.GetUnlockedChapters(),
        
        // Change overlay (from ChangeTracker #12)
        ChangeOverlay = _changeTracker.GetPersistableOverlay(),
        
        // Cross-chapter flags (from CrossChapterTracker #16)
        CrossChapterFlags = _crossChapterTracker.GetPersistableFlags(),
        
        // Volume (from AudioManager #3)
        MasterVolume = _audioManager.GetVolume("master"),
        SFXVolume = _audioManager.GetVolume("sfx"),
        MusicVolume = _audioManager.GetVolume("music"),
        AmbienceVolume = _audioManager.GetVolume("ambience"),
        
        // Endings (from EndingTracker #14)
        TriggeredEndingConditionIds = _endingTracker.GetTriggeredIds(),
    };
    return data;
}
```

RestoreSaveData:
```csharp
public async Task RestoreSaveData(SaveData data)
{
    // 恢复顺序重要——语言和音量先恢复
    _localizationManager.RestoreLocale(data.LocaleCode);
    _audioManager.SetVolume("master", data.MasterVolume);
    _audioManager.SetVolume("sfx", data.SFXVolume);
    _audioManager.SetVolume("music", data.MusicVolume);
    _audioManager.SetVolume("ambience", data.AmbienceVolume);
    
    _changeTracker.RestoreFromSave(data.ChangeOverlay);
    _crossChapterTracker.RestoreFromSave(data.CrossChapterFlags);
    _endingTracker.RestoreFromSave(data.TriggeredEndingConditionIds);
    
    // 最后恢复章节进度（触发场景加载）
    await _chapterManager.LoadAndRestore(data);
}
```

版本迁移链:
```csharp
private const int CURRENT_SAVE_VERSION = 1;

private SaveData MigrateIfNeeded(SaveData data)
{
    while (data.Version < CURRENT_SAVE_VERSION)
    {
        data = data.Version switch
        {
            0 => Migrate_V0_to_V1(data),
            // 未来版本在此添加
            _ => throw new SaveMigrationException(
                $"Unsupported save version: {data.Version}")
        };
    }
    return data;
}

private SaveData Migrate_V0_to_V1(SaveData data)
{
    // 迁移逻辑: 填充新字段的默认值
    // data.SomeNewField = defaultValue;
    data.Version = 1;
    return data;
}
```

---

## Out of Scope

*Handled by neighbouring stories or systems:*

- Story 001: SaveData struct 定义 + SHA-256
- Story 002: 文件 I/O 操作
- 各系统的具体 RestoreFromSave 实现 — 由各系统负责
- ChapterManager.LoadAndRestore 的具体章节恢复 — 由 Chapter Management (#15) 负责

---

## QA Test Cases

- **AC-1**: CollectSaveData 收集全部状态
  - Given: 游戏中——章节 1 碎片 3，语言 = en，音量为特定值，有一个 ChangeOverlay 条目
  - When: 调用 `CollectSaveData()`
  - Then: SaveData 所有字段被填充；CurrentChapterKey = "ch1"；CurrentFragmentId = "frag_03"；LocaleCode = "en"；ChangeOverlay 包含相应条目
  - Edge cases: 新游戏（无 ChangeOverlay/CrossChapterFlags）→ 字典为空（非 null）

- **AC-2**: RestoreSaveData 恢复完整状态
  - Given: 一份存档包含 ch1/frag_03, en, 特定音量, 1 个 ChangeOverlay 条目
  - When: 调用 `RestoreSaveData(data)` 并 await
  - Then: 游戏恢复到 ch1/frag_03；语言 = en；音量 = 保存的值；ChangeOverlay 已恢复
  - Edge cases: 存档的章节键不存在 → LoadAndRestore 失败，不恢复任何状态

- **AC-3**: 版本迁移 v1 → v2
  - Given: 版本 1 的 SaveData JSON（无 NewField）
  - When: CURRENT_SAVE_VERSION = 2, 加载该 JSON
  - Then: `Migrate_V1_to_V2` 执行；data.Version = 2；NewField 设置为默认值
  - Edge cases: 版本号大于 CURRENT_SAVE_VERSION → 显示"存档来自更新的游戏版本"

- **AC-4**: 迁移失败处理
  - Given: 迁移函数抛异常（数据格式不可修复）
  - When: 加载中调用 MigrateIfNeeded
  - Then: 捕获 SaveMigrationException；显示"存档与新版本不兼容"；不进入游戏
  - Edge cases: 迁移链中间步骤失败 → 回滚到迁移前状态 (SaveData 不变)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/save-load-system/orchestration_test.cs` — must exist and pass

**Status**: [x] Created (12 test functions, all 4 ACs covered)

---

## Dependencies

- Depends on: Story 002 (原子 I/O), Localization (#4), Audio (#3), ChangeTracker (#12), CrossChapterTracker (#16), ChapterManager (#15), EndingTracker (#14)
- Unlocks: Main Menu (#19) "继续游戏"/"加载游戏"功能

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 4/4 passing (12 integration tests, all ACs covered)
**Deviations**: ADVISORY — `using UnityEngine;` in SaveOrchestrator.cs unused; ADR-0003 migration sketch uses Dictionary pattern but implementation uses switch expression (functionally equivalent)
**Test Evidence**: Integration — `tests/integration/save-load-system/orchestration_test.cs` (12 test functions)
**Code Review**: APPROVED (unity-specialist skipped — lean mode; manual review: clean architecture, correct ADR compliance, all ACs traced to tests)
