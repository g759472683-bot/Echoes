# Story 004: Locale 持久化集成

> **Epic**: 本地化系统 (LocalizationManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/localization.md`
**Requirement**: GDD Acceptance Criteria #7 (save/restore locale)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0015: 本地化策略, ADR-0003: 存档序列化策略
**ADR Decision Summary**: ADR-0015 — Archive LocaleCode persisted to SaveData; ADR-0003 — JSON + SHA-256 + 原子写入

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM (Localization 包), LOW (纯 C# I/O)
**Engine Notes**: `SelectedLocale.Identifier.Code` 在 Unity LP 中为 BCP-47 标签字符串 (如 "zh-Hans", "en")

**Control Manifest Rules (Foundation Layer)**:
- Required: Archive LocaleCode persisted to SaveData — language selection survives restart — source: ADR-0015
- Required: OnLocaleChanged static event for all UI refresh — source: ADR-0015, ADR-0001
- Forbidden: Never hardcode player-facing strings

---

## Acceptance Criteria

*From GDD `design/gdd/localization.md`, scoped to this story:*

- [ ] GIVEN 当前 Locale 为 en，WHEN 游戏存档被保存然后重新加载，THEN 恢复后语言保持为 en
- [ ] `LocalizationManager.SaveLocaleCode` 返回当前 `SelectedLocale.Identifier.Code` 字符串 (e.g. "en")
- [ ] `LocalizationManager.RestoreLocaleCode(string code)` 调用 `SetLocale(code)` 恢复语言
- [ ] 存档中无 LocaleCode 字段时（旧版存档/首次启动）→ 使用默认 Locale (zh-Hans)
- [ ] Locale 持久化与 Save/Load 系统 (#7) 正确集成——存档流包含 LocaleCode 字段

---

## Implementation Notes

*Derived from ADR-0015 + ADR-0003:*

SaveData 中的 Locale 字段:
```csharp
// 在 SaveData struct 中 (由 Save/Load 系统 #7 定义)
public string LocaleCode;  // BCP-47: "zh-Hans" / "en"
```

LocalizationManager 持久化接口:
```csharp
// 获取当前语言标识符 — 供 SaveManager 在保存时调用
public string GetCurrentLocaleCode()
{
    return LocalizationSettings.SelectedLocale.Identifier.Code;
}

// 恢复语言 — 供 SaveManager 在加载后调用
public void RestoreLocale(string localeCode)
{
    if (string.IsNullOrEmpty(localeCode))
    {
        // 旧版存档或无语言设置 → 保持默认 zh-Hans
        Debug.Log("[Localization] No locale in save data, keeping default zh-Hans");
        return;
    }
    
    SetLocale(localeCode);
}
```

集成流程:
1. **Save**: SaveManager 收集状态 → 调用 `LocalizationManager.GetCurrentLocaleCode()` → 写入 `SaveData.LocaleCode`
2. **Load**: SaveManager 反序列化 SaveData → 调用 `LocalizationManager.RestoreLocale(SaveData.LocaleCode)` → SetLocale 触发 OnLocaleChanged → UI 刷新
3. **New Game**: 无存档 → 使用默认 zh-Hans

---

## Out of Scope

*Handled by neighbouring stories or systems:*

- Story 002: SetLocale 核心切换引擎
- SaveData struct 定义 — 由存档系统 (#7) 负责
- 存档槽位管理、文件 I/O — 由存档系统 (#7) 负责
- 语言选择 UI（设置下拉列表）— 由 Main Menu (#19) 负责

---

## QA Test Cases

- **AC-1**: 存档恢复语言
  - Given: 游戏语言为 en, 存档已保存
  - When: 退出游戏 → 重新启动 → 加载该存档
  - Then: 游戏启动后，`SelectedLocale` 恢复为 en；所有 UI 文本显示英文
  - Edge cases: 存档保存时 Locale = zh-Hans → 恢复后 = zh-Hans

- **AC-2**: GetCurrentLocaleCode 返回正确标识符
  - Given: 当前 Locale = en
  - When: 调用 `GetCurrentLocaleCode()`
  - Then: 返回 "en"
  - Edge cases: Locale = zh-Hans → 返回 "zh-Hans"

- **AC-3**: RestoreLocale 调用 SetLocale
  - Given: 存档中的 LocaleCode = "en"
  - When: 调用 `RestoreLocale("en")`
  - Then: `SetLocale("en")` 被调用；`OnLocaleChanged("en")` 被触发；UI 刷新为英文
  - Edge cases: LocaleCode 为 null/空 → 保持默认 zh-Hans

- **AC-4**: 旧版存档兼容
  - Given: 存档 JSON 中无 `LocaleCode` 字段（旧版存档格式）
  - When: 反序列化后 `SaveData.LocaleCode == null`
  - Then: `RestoreLocale(null)` 被调用 → 保持默认 zh-Hans → 日志记录 "No locale in save data"
  - Edge cases: LocaleCode 包含无效值（如 "fr" 但项目不支持）→ SetLocale 内部 fallback 到 zh-Hans

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/localization/locale_persistence_test.cs` — must exist and pass

**Status**: [x] Created (11 test functions, all 4 ACs covered)

---

## Dependencies

- Depends on: Story 002 (SetLocale 引擎), ADR-0003 (SaveData 结构)
- Unlocks: Main Menu 语言设置 UI (#19)

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 4/4 passing (11 integration tests)
**Deviations**: None — ILocaleProvider implemented by LocalizationManager, integrates with SaveOrchestrator CollectSaveData/RestoreSaveData, legacy save (null/empty LocaleCode) handled per spec
**Test Evidence**: Integration — `tests/integration/localization/locale_persistence_test.cs` (11 test functions)
**Code Review**: APPROVED (lean mode; full round-trip save→restore verified for en and zh-Hans; invalid locale falls back to default; OnLocaleChanged fires on restore; null/empty LocaleCode handled gracefully)
