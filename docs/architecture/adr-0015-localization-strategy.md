# ADR-0015: 本地化策略 — Unity Localization 包 + 双字符串表

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

回响 (Echoes) 需要支持中/英双语。决定使用 Unity Localization 包 + 双 StringTable（UI_Shared 持久 + Narrative_Ch 按章加载）+ 后备链 en → zh-Hans + OnLocaleChanged 事件通知所有 UI 刷新。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | MEDIUM — Unity Localization 包版本需验证与 6.3 LTS 的兼容性 |
| **References Consulted** | `VERSION.md`, `current-best-practices.md` |
| **Post-Cutoff APIs Used** | `com.unity.localization`: `LocalizationSettings`, `Locale`, `StringTable`, `LocalizedString`, `TableReference`, `TableEntryReference` |
| **Verification Required** | Localization 包在 Unity 6.3 LTS 中的版本兼容性；`LocalizedString` 在 UI Toolkit 中的绑定支持 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | ADR-0006 (UI Toolkit 文本绑定), ADR-0019 (MainMenu — 语言切换 UI) |
| **Blocks** | UI + MainMenu Epic (玩家可见文本需要本地化) |
| **Ordering Note** | 在 UI 实现之前完成 |

## Context

### Problem Statement

游戏面向 Steam 中/英玩家群体。所有玩家可见文本（UI 标签、碎片文本、选项文本、结局描述）需要在两种语言间切换。叙事文本量大（每章 20-40 碎片文本），需按章加载以节省内存。

### Constraints

- 目标语言：zh-Hans (简体中文), en (English)
- 后备链：en → zh-Hans（缺失英文翻译时回退中文）
- UI_Shared StringTable 始终常驻内存
- Narrative_Ch StringTable 按章加载/卸载
- 语言切换不需要重启游戏

### Requirements

- Unity Localization 包（官方推荐）
- 两个 StringTable 类型（UI 持久 + 叙事按章）
- `OnLocaleChanged` 事件通知所有 UI 刷新
- 语言选择保存到存档 (SaveData.LocaleCode)

## Decision

**Unity Localization 包 + 双 StringTable + 后备链 + OnLocaleChanged 事件。**

### StringTable 架构

```
LocalizationSettings
├─ StringTable: UI_Shared (常驻内存)
│   ├─ "main_menu_new_game" → "新游戏" / "New Game"
│   ├─ "main_menu_continue" → "继续" / "Continue"
│   ├─ "settings_volume_master" → "主音量" / "Master Volume"
│   └─ ... (所有 UI 文本)
│
├─ StringTable: Narrative_Ch1 (按章加载)
│   ├─ "ch1_frag01_text" → "记忆像旧照片一样褪色..." / "Memories fade like old photographs..."
│   ├─ "ch1_frag01_choiceA" → "揭开它" / "Uncover it"
│   └─ ... (第 1 章所有碎片文本)
│
├─ StringTable: Narrative_Ch2 (按章加载)
│   └─ ...
│
└─ ... (每章一个 Narrative StringTable)
```

### 后备链

```
en → zh-Hans (default)

查询流程:
  1. 请求 key 在 en StringTable 中 → 返回英文
  2. en 中不存在 → 回退到 zh-Hans (默认语言)
  3. zh-Hans 中不存在 → 返回 key 本身 + 日志警告
```

### 语言切换

```csharp
public void SetLocale(string localeCode)
{
    var locale = LocalizationSettings.AvailableLocales
        .GetLocale(localeCode);

    if (locale == null)
    {
        Debug.LogWarning($"Locale '{localeCode}' not available, falling back to zh-Hans");
        locale = LocalizationSettings.AvailableLocales
            .GetLocale("zh-Hans");
    }

    LocalizationSettings.SelectedLocale = locale;
    OnLocaleChanged?.Invoke(localeCode); // static event
}
```

### Architecture Diagram

```
┌──────────────────────────────────────────────┐
│         LocalizationManager                   │
│                                              │
│  GetLocalizedString(tableRef, entryRef)       │
│    → string                                   │
│                                              │
│  SetLocale(localeCode)                        │
│    → LocalizationSettings.SelectedLocale      │
│    → OnLocaleChanged event                    │
│                                              │
│  GetCurrentLocale() → Locale                  │
│                                              │
│  Events:                                      │
│    OnLocaleChanged(string localeCode)         │
└──────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────┐
│  Unity Localization Package                   │
│                                              │
│  LocalizationSettings                         │
│  ├─ AvailableLocales: [zh-Hans, en]          │
│  ├─ SelectedLocale                            │
│  └─ StringTable[]                             │
│       ├─ UI_Shared (persistent)               │
│       └─ Narrative_Ch[N] (per-chapter)        │
│                                              │
│  Addressables-backed StringTable loading      │
└──────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public interface ILocalizationManager
{
    string GetLocalizedString(string tableRef, string entryRef);
    void SetLocale(string localeCode);
    string GetCurrentLocale();
    // static event Action<string> OnLocaleChanged;
}
```

### UI Toolkit 绑定

```csharp
// UXML 中使用 LocalizedString 绑定
// <Label text="LocalizedString(UI_Shared, main_menu_new_game)" />

// 或在 C# 代码中手动设置
void OnLocaleChanged(string localeCode)
{
    newGameButton.text = LocalizationManager
        .GetLocalizedString("UI_Shared", "main_menu_new_game");
}
```

### Implementation Guidelines

1. Unity Localization 包通过 Package Manager 安装，版本锁定在 manifest.json
2. UI_Shared StringTable 在 Boot 场景加载并常驻内存
3. Narrative_Ch StringTable 通过 Addressables 按章加载（与碎片资产同步预加载）
4. `OnLocaleChanged` 触发时，所有 UI Controller 重新查询本地化字符串
5. 存档中的 LocaleCode 在 MainMenu 加载时应用

## Alternatives Considered

### Alternative 1: 自定义 CSV/JSON 本地化系统

- **Description**: 自研本地化：CSV/JSON 文件 → Dictionary 加载 → 手动切换
- **Pros**: 无外部包依赖；完全控制
- **Cons**: 需要自建 Editor 工具、缺失翻译检测、复数形式处理、后备链；开发成本高
- **Rejection Reason**: Unity Localization 包提供开箱即用的 Editor 工具、后备链、Addressables 集成

### Alternative 2: 仅中文（无本地化）

- **Description**: 只支持中文，不引入本地化系统
- **Pros**: 零复杂度
- **Cons**: Steam 受众中有大量英文用户；后期添加本地化成本指数增长
- **Rejection Reason**: 技术偏好明确中/英双语支持；从 Day 1 设计本地化比后期 retrofit 便宜

## Consequences

### Positive

- 官方包支持：Editor 工具、缺失翻译检测、复数/性别变体
- Addressables 集成：Narrative StringTable 按章管理内存
- 运行时语言切换无需重启
- 后备链保证不会出现空白文本

### Negative

- Unity Localization 包是外部依赖（版本升级有风险）
- Addressables-backed StringTable 在首次加载时可能有延迟
- UI Toolkit 中 `LocalizedString` 绑定需要手动刷新（`OnLocaleChanged` 事件驱动）
- 每章需要维护 2 套 StringTable 条目（中英文各一）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Localization 包与 Unity 6.3 LTS 不兼容 | Low | Medium | Pre-Production 早期验证；如有问题替换为自定义 JSON 方案 |
| Narrative StringTable 加载延迟导致碎片文本空白 | Low | Medium | StringTable 与碎片资产同步预加载（DataManager.PreloadChapterAsync 包含 StringTable） |
| 翻译缺失导致空白文本 | Medium | Low | 后备链保证至少显示一种语言；Editor 缺失翻译检测工具 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (GetLocalizedString, cached) | ~0.01ms (StringTable lookup) |
| Memory (UI_Shared StringTable, 2 locales) | ~50KB (估算 ~200 UI entries) |
| Memory (Narrative_Ch StringTable, per chapter, 2 locales) | ~100-300KB (每章 ~30 碎片 × ~200 words) |
| Load Time (Narrative StringTable initial load) | ~100-500ms (Addressables, 与碎片资产并行) |
| GC Allocation (GetLocalizedString) | 0 (返回 interned string reference) |

## Migration Plan

新建项目，无迁移需求。若未来添加第 3+ 语言：
1. 在 `LocalizationSettings.AvailableLocales` 注册新 Locale
2. 为每个 StringTable 创建新语言列
3. 更新后备链顺序
4. 更新存档中的 LocaleCode 枚举

## Validation Criteria

- [ ] 运行时切换语言后所有 UI 文本刷新（OnLocaleChanged 事件验证）
- [ ] 缺失英文翻译时回退显示中文（后备链验证）
- [ ] 切换章节时卸载当前 Narrative StringTable 并加载新章节
- [ ] UI_Shared StringTable 在任何场景下都可用
- [ ] 语言选择持久化到存档并在重启后恢复

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `localization-system.md` (#4) | 本地化 | 中/英双语支持 | zh-Hans + en Locale |
| `localization-system.md` (#4) | 本地化 | 双 StringTable (UI 持久 + 叙事按章) | UI_Shared + Narrative_Ch[N] |
| `localization-system.md` (#4) | 本地化 | 后备链 en → zh-Hans | LocalizationSettings fallback |
| `localization-system.md` (#4) | 本地化 | OnLocaleChanged 事件通知 | static event, ADR-0001 符合 |
| `save-load-system.md` (#7) | 存档 | 语言设置持久化 | SaveData.LocaleCode |
| `main-menu.md` (#19) | 主菜单 | 语言切换 UI | SetLocale(localeCode) |
| `in-game-hud.md` (#17) | 游戏 HUD | 碎片文本本地化 | GetLocalizedString("Narrative_ChN", entryRef) |

## Related

- ADR-0006 — UI Toolkit 文本绑定
- ADR-0002 — Addressables 按章加载 StringTable
- `docs/engine-reference/unity/modules/core.md` — Localization 包信息
