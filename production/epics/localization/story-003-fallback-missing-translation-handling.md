# Story 003: Fallback 链 + 缺失翻译处理

> **Epic**: 本地化系统 (LocalizationManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/localization.md`
**Requirement**: `TR-localization-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0015: 本地化策略
**ADR Decision Summary**: Fallback 链 en → zh-Hans — 缺失英文翻译自动回退中文；MissingTranslationEvent 回调记录日志；禁止显示原始 Key 名

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Unity LP 内置 Fallback 机制处理回退——但需验证在 IL2CPP 构建中的行为与 Editor 一致

**Control Manifest Rules (Foundation Layer)**:
- Required: Fallback chain en → zh-Hans — missing English falls back to Chinese, never blank text
- Forbidden: Never hardcode player-facing strings — all text via LocalizationManager.GetLocalizedString()
- Forbidden: Never load Narrative StringTables without chapter preload

---

## Acceptance Criteria

*From GDD `design/gdd/localization.md`, scoped to this story:*

- [ ] GIVEN 英语 StringTable 中某个 Key 未填写翻译，WHEN 获取该 Key 的英语字符串，THEN Fallback 链自动返回中文原文——不显示空白、不显示 Key 名
- [ ] GIVEN Key 在所有层级都不存在，WHEN 请求该 Key 的字符串，THEN `MissingTranslationEvent` 回调记录 Error 日志 → Release Build 显示通用省略号 `"……"`，Development Build 显示 `"<MISSING: key.name>"`
- [ ] GIVEN UI_Shared 表加载失败，WHEN 游戏启动时 Addressables 抛出异常，THEN 系统进入 Error 状态，显示错误信息并阻止进入主菜单
- [ ] Fallback 查询顺序：目标 Locale Entry → SharedTableData Fallback → Locale Fallback 链 (en → zh-Hans)，不编写自定义 Fallback 代码

---

## Implementation Notes

*Derived from ADR-0015:*

Fallback 链配置在 `LocalizationSettings` 资产中——Unity LP 内置处理，无需自定义逻辑:
```
en → zh-Hans (default)
```

MissingTranslationEvent 回调:
```csharp
LocalizationSettings.StringDatabase.MissingTranslationState
    .MissingTranslationEvent += (sender, args) =>
    {
        Debug.LogWarning(
            $"[Localization] Missing translation: " +
            $"Table={args.Table}, Key={args.Key}, Locale={args.Locale}");
    };
```

GetLocalizedString 缺失处理:
```csharp
public string GetLocalizedString(TableReference tableRef, TableEntryReference entryRef)
{
    var localizedString = new LocalizedString(tableRef, entryRef);
    var result = localizedString.GetLocalizedString();
    
    if (string.IsNullOrEmpty(result))
    {
        Debug.LogWarning($"Missing translation: {tableRef}/{entryRef}");
        #if DEVELOPMENT_BUILD
        return $"<MISSING: {entryRef}>";
        #else
        return "……";
        #endif
    }
    
    return result;
}
```

Error 状态恢复:
- UI_Shared 加载失败 → 不可恢复硬错误
- 显示引擎内置英文错误提示（硬编码，唯一例外于"禁止硬编码字符串"规则）
- 提供"返回桌面"按钮（不依赖本地化文本）

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: StringTable 配置和 CSV 导入
- Story 002: Locale 切换引擎
- Story 004: Locale 持久化
- 错误 UI 的具体视觉呈现 — 由 UI Framework (#5) 负责

---

## QA Test Cases

- **AC-1**: Fallback en → zh-Hans
  - Given: en StringTable 中 Key "ui.menu.settings.test_label" 未填写翻译（空）
  - When: 当前 Locale = en，获取该 Key 的字符串
  - Then: 返回 zh-Hans 中的对应文本（非空、非 Key 名）
  - Edge cases: zh-Hans 中也缺失 → 返回 "……" (Release) / "<MISSING: key>" (Dev)

- **AC-2**: MissingTranslationEvent 触发
  - Given: 请求一个不存在的 Key "nonexistent.key.abc"
  - When: `GetLocalizedString` 被调用
  - Then: `MissingTranslationEvent` 回调被触发；Debug.LogWarning 输出包含 Table/Key/Locale 信息
  - Edge cases: 连续请求 100 个不存在的 Key → 每个独立触发事件，日志合并到文件

- **AC-3**: UI_Shared 加载失败 → Error
  - Given: UI_Shared Addressables 组被故意移除
  - When: 游戏启动
  - Then: Initializing → Error；显示错误信息 "Failed to load UI text. Please verify game files."；不进入主菜单
  - Edge cases: Error 后点击"返回桌面" → Application.Quit()；Error 后点击"重试" → 重新进入 Initializing

- **AC-4**: 不显示 Key 名
  - Given: Release Build，所有 Fallback 层级都缺失的 Key
  - When: 获取该 Key 的字符串
  - Then: 显示 "……"（中文省略号）；不显示原始 Key 名（如 "narrative.ch01.frag_03.line_01"）
  - Edge cases: 中文字符串为空字符串 "" → 视为缺失（返回 "……"），而非显示空

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/localization/fallback_test.cs` — must exist and pass

**Status**: [x] Created (10 test functions, all 4 ACs covered)

---

## Dependencies

- Depends on: Story 002 (Locale 切换引擎 Ready 状态)
- Unlocks: None directly

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 4/4 passing (10 unit tests)
**Deviations**: None — follows ADR-0015 fallback chain (en → zh-Hans), MissingTranslationEvent callback, release/dev build differentiation per GDD
**Test Evidence**: Logic — `tests/unit/localization/fallback_test.cs` (10 test functions)
**Code Review**: APPROVED (lean mode; fallback returns marker never raw key; empty string treated as missing; Error/Uninitialized states return safe fallback; null table ref handled)
