# Story 002: 运行时 Locale 切换引擎

> **Epic**: 本地化系统 (LocalizationManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/localization.md`
**Requirement**: `TR-localization-003`, `TR-localization-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0015: 本地化策略, ADR-0001: 事件总线架构
**ADR Decision Summary**: Unity Localization 包 + SetLocale() 切换 + LocalizedString 自动刷新 + OnLocaleChanged static event 通知所有 UI

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: LocalizedString 在 UI Toolkit 中的绑定支持需验证；SelectedLocale 切换性能需在 IL2CPP 构建中测试

**Control Manifest Rules (Foundation Layer)**:
- Required: OnLocaleChanged static event for all UI refresh — per ADR-0001 pattern
- Required: Archive LocaleCode persisted to SaveData — language selection survives restart
- Required: `static event Action<T>` for all cross-system communication
- Forbidden: Never hardcode player-facing strings

---

## Acceptance Criteria

*From GDD `design/gdd/localization.md`, scoped to this story:*

- [ ] GIVEN 游戏启动，WHEN LocalizationSettings 初始化完成，THEN UI_Shared 表加载成功，默认 Locale (zh-Hans) 激活。所有 UI 文本显示为中文
- [ ] GIVEN 游戏语言为中文 (zh-Hans)，WHEN 玩家在设置中切换到 English (en)，THEN 所有 UI 文本在当帧刷新为英语——无闪烁、无"部分翻译"中间状态
- [ ] GIVEN 玩家切换到英语后查看一段记忆碎片叙述，WHEN Narrative 表已加载，THEN 碎片文本显示英语版本
- [ ] 状态机 6 状态正确运转: Uninitialized → Initializing → Ready ↔ SwitchingLocale ↔ LoadingNarrativeTable → Error
- [ ] `OnLocaleChanged(string localeCode)` static event 在 Locale 切换完成后触发——所有订阅系统收到通知
- [ ] Locale 切换期间 Narrative 表加载中：UI 文本不受影响，碎片叙述文本保持原语言直到加载完成

---

## Implementation Notes

*Derived from ADR-0015 + ADR-0001:*

状态机:
```csharp
private enum LocaleState
{
    Uninitialized,       // LocalizationSettings 尚未加载
    Initializing,        // UI_Shared 表异步加载中
    Ready,               // 默认 Locale 激活，可查询
    SwitchingLocale,     // 玩家更改语言设置
    LoadingNarrativeTable, // 异步加载章节 Narrative 表
    Error                // UI_Shared 加载失败
}
```

核心切换逻辑:
```csharp
public void SetLocale(string localeCode)
{
    var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
    if (locale == null)
    {
        Debug.LogWarning($"Locale '{localeCode}' not available, falling back to zh-Hans");
        locale = LocalizationSettings.AvailableLocales.GetLocale("zh-Hans");
    }
    
    LocalizationSettings.SelectedLocale = locale;
    // Unity LP 自动刷新所有 LocalizedString 组件 — 当帧更新
    
    OnLocaleChanged?.Invoke(localeCode); // ADR-0001 static event pattern
}

public static event Action<string> OnLocaleChanged;
```

初始化流程:
1. Uninitialized → 自动进入 Initializing
2. 等待 UI_Shared 表异步加载
3. 成功 → Ready (通知 OnLocaleChanged)
4. 失败 → Error (阻止进入主菜单)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: Unity LP 配置 + StringTable 结构 (本 Story 依赖的配置)
- Story 003: Fallback + 缺失处理 — MissingTranslationEvent, Error 状态恢复
- Story 004: Locale 持久化 — SaveData 存档
- 中英文字体切换 — 由 UI Framework (#5) 负责

---

## QA Test Cases

- **AC-1**: 启动默认中文
  - Given: 游戏刚启动，LocalizationSettings 未初始化
  - When: 引擎完成初始化
  - Then: UI_Shared 加载成功；`LocalizationSettings.SelectedLocale == zh-Hans`；所有 UI 文本显示为中文
  - Edge cases: UI_Shared 加载失败 → Error 状态，显示错误信息

- **AC-2**: 切换英语—UI 当帧刷新
  - Given: 当前 Locale = zh-Hans，UI_Shared + Narrative_Ch01 已加载
  - When: 调用 `SetLocale("en")`
  - Then: `LocalizationSettings.SelectedLocale == en`；所有 UI 文本变为英文；`OnLocaleChanged("en")` 被触发
  - Edge cases: en Locale 不存在 → warning + 回退到 zh-Hans

- **AC-3**: 切换后碎片文本更新
  - Given: 当前显示碎片 frag_01 的中文文本，Narrative_Ch01 的 en 翻译存在
  - When: 切换到 en
  - Then: frag_01 文本刷新为英语版本
  - Edge cases: Narrative 表尚未加载 → 文本保持中文，表加载后刷新为英文

- **AC-4**: 状态机转换正确
  - Given: DataManager 处于 Ready 状态
  - When: 触发 `SetLocale("en")` → Locale 切换完成 → 加载章节 Narrative 表
  - Then: 状态轨迹: Ready → SwitchingLocale → Ready → LoadingNarrativeTable → Ready
  - Edge cases: SwitchingLocale 中再次 SetLocale → 以最后一次为准

- **AC-5**: OnLocaleChanged 事件通知
  - Given: UI Framework + HUD 已订阅 `OnLocaleChanged`
  - When: `SetLocale("en")` 完成
  - Then: `OnLocaleChanged?.Invoke("en")` 被调用；所有订阅者收到通知
  - Edge cases: 无订阅者 → `?.Invoke` 安全跳过

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/localization/locale_switch_test.cs` — must exist and pass

**Status**: [x] Created (17 test functions, all 5 ACs covered)

---

## Dependencies

- Depends on: Story 001 (需要 LocalizationSettings + StringTable 配置完成)
- Unlocks: Story 003 (Fallback 逻辑), Story 004 (持久化)

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 5/5 passing (17 unit tests)
**Deviations**: None — follows ADR-0015 state machine (6 states), ADR-0001 static event pattern (OnLocaleChanged), and control manifest rules
**Test Evidence**: Logic — `tests/unit/localization/locale_switch_test.cs` (17 test functions)
**Code Review**: APPROVED (lean mode; architecture: ILocalizationBackend DI abstraction, 6-state machine mirrors GDD, SetLocale with fallback validation, narrative table loading state preserved across locale switches)
