# Story 004: 自动存档引擎

> **Epic**: 存档系统 (SaveManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/save-load-system.md`
**Requirement**: `TR-save-load-system-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0003: 存档序列化格式与版本迁移策略
**ADR Decision Summary**: 自动存档在章节边界、关键选择和退出时触发；30s 防抖；后台静默执行无通知

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `OnApplicationQuit` 的保存窗口有限——保存必须 < 500ms；`OnApplicationPause` 在移动端代替 OnApplicationQuit

**Control Manifest Rules (Foundation Layer)**:
- Required: Auto-save triggers: chapter start, critical choice (30s debounce), chapter complete, OnApplicationQuit — silent, no notification
- Guardrail: Save file write: <200ms (JSON serialize + SHA-256 + atomic write)

---

## Acceptance Criteria

*From GDD `design/gdd/save-load-system.md`, scoped to this story:*

- [ ] GIVEN 玩家做出关键选择（改变 ChangeOverlay），WHEN 选择完成，THEN 自动存档触发——`auto_save.sav` 包含选择后的 ChangeOverlay
- [ ] GIVEN 玩家在章节边界，WHEN 章节转换完成，THEN 自动存档在后台触发——不显示通知
- [ ] 自动存档触发点完整覆盖：章节开始、关键选择、章节完成、`OnApplicationQuit`
- [ ] 关键选择触发自动存档时，距上一个自动存档 < 30 秒则跳过（防抖）
- [ ] 自动存档始终写入 `auto_save` 槽位——覆盖前一个自动存档

---

## Implementation Notes

*Derived from ADR-0003:*

触发点注册:
```csharp
public class AutoSaveManager
{
    private float _lastAutoSaveTime = -999f;
    private const float AUTO_SAVE_DEBOUNCE = 30f;
    private const string AUTO_SAVE_SLOT = "auto_save";
    
    public void Initialize()
    {
        // 订阅触发点事件
        ChapterManager.OnChapterStarted += OnChapterStarted;
        ChapterManager.OnChapterCompleted += OnChapterCompleted;
        ChangeTracker.OnOverlayChanged += OnCriticalChoice;
        Application.quitting += OnApplicationQuit;
    }
    
    private async void OnChapterStarted(string chapterKey)
    {
        await TriggerAutoSave("chapter_start");
    }
    
    private async void OnChapterCompleted(string chapterKey)
    {
        await TriggerAutoSave("chapter_complete");
    }
    
    private async void OnCriticalChoice(string fragmentId)
    {
        // 30s 防抖
        if (Time.time - _lastAutoSaveTime < AUTO_SAVE_DEBOUNCE)
        {
            Debug.Log("[AutoSave] Skipped — within debounce window");
            return;
        }
        await TriggerAutoSave("critical_choice");
    }
    
    private void OnApplicationQuit()
    {
        // 同步保存——OnApplicationQuit 必须阻塞等待完成
        var saveData = _saveManager.CollectSaveData();
        _saveManager.SaveSync(AUTO_SAVE_SLOT, saveData); // 同步版本
    }
}
```

静默执行:
- 自动存档不显示 UI 提示——不弹"保存中"对话框
- 不播放保存音效（防止打断游戏体验）
- 保存失败仅日志记录——不打断玩家（与手动保存不同，手动保存失败需告知玩家）

OnApplicationQuit 特殊处理:
- 必须使用同步 I/O（`File.WriteAllText` 而非 `WriteAllTextAsync`）——因为应用退出时不等待异步操作
- 保存超时 500ms——超时则放弃保存（宁可丢失自动存档，不让退出卡住）

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 原子文件 I/O — SaveAsync 底层实现
- Story 003: CollectSaveData — 收集状态的逻辑
- 手动保存 UI — 由 Main Menu (#19) 负责
- "关键选择"判定 — 由 ChangeTracker (#12) 的 OnOverlayChanged 事件定义（本 Story 仅订阅）

---

## QA Test Cases

- **AC-1**: 关键选择触发自动存档
  - Given: 游戏中，玩家做出了一个触发 OnOverlayChanged 的选择
  - When: OnOverlayChanged 事件触发
  - Then: `auto_save.sav` 被写入；SaveData.ChangeOverlay 包含该选择的效果
  - Edge cases: 距上次自动存档 < 30s → 跳过（不写入）

- **AC-2**: 章节边界触发自动存档
  - Given: 章节 1 的最后一个碎片已展示
  - When: OnChapterCompleted("ch1") 事件触发
  - Then: `auto_save.sav` 被写入；SaveData.CurrentChapterKey 可能已更新为 "ch2"
  - Edge cases: 保存失败（磁盘满）→ 日志记录 warning，不打断章节过渡

- **AC-3**: OnApplicationQuit 同步保存
  - Given: 游戏运行中
  - When: 玩家关闭应用（Application.Quit）
  - Then: `auto_save.sav` 在退出前同步写入；保存完成前应用不退出
  - Edge cases: 保存超时 500ms → 放弃保存，允许退出

- **AC-4**: 自动存档静默无通知
  - Given: 自动存档触发
  - When: 保存完成
  - Then: 不显示 UI 提示；不播放音效；不弹出通知
  - Edge cases: 自动存档失败 → 仅 Debug.LogWarning 记录

- **AC-5**: 防抖 30s
  - Given: 自动存档刚完成（_lastAutoSaveTime = now）
  - When: 15s 后另一个关键选择触发 OnOverlayChanged
  - Then: 自动存档被跳过；Debug.Log 记录 "Skipped — within debounce window"
  - Edge cases: 31s 后 → 正常触发；chapter_start 和 chapter_complete 触发点不受防抖限制

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/save-load-system/auto_save_test.cs` — must exist and pass

**Status**: [x] Created (18 test functions, all 5 ACs covered)

---

## Dependencies

- Depends on: Story 002 (SaveAsync), Story 003 (CollectSaveData)
- Unlocks: Main Menu (#19) — 自动存档槽位显示在"继续游戏"

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 5/5 passing (18 unit tests, all ACs covered)
**Deviations**: None — follows ADR-0003 trigger points, ADR-0001 static event pattern, and control manifest silent-execution rule
**Test Evidence**: Logic — `tests/unit/save-load-system/auto_save_test.cs` (18 test functions)
**Code Review**: APPROVED (lean mode; architecture: correct DI via ITimeProvider, static event wiring follows ADR-0001, debounce boundary at exactly 30s, OnApplicationQuit sync save with 500ms timeout, silent failure handling per spec)
