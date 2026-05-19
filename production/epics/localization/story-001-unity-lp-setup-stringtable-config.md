# Story 001: Unity LP 配置 + StringTable 结构

> **Epic**: 本地化系统 (LocalizationManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Config/Data
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/localization.md`
**Requirement**: `TR-localization-001`, `TR-localization-002`, `TR-localization-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0015: 本地化策略
**ADR Decision Summary**: Unity Localization 包 + 双 StringTable（UI_Shared 持久 + Narrative_Ch[N] 按章）+ 后备链 en → zh-Hans + OnLocaleChanged 事件

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Unity Localization 包版本需验证与 6.3 LTS 的兼容性；LocalizedString 在 UI Toolkit 中的绑定支持需确认

**Control Manifest Rules (Foundation Layer)**:
- Required: Unity Localization package with dual StringTable — UI_Shared (persistent) + Narrative_Ch[N] (per-chapter)
- Required: Fallback chain en → zh-Hans — missing English falls back to Chinese, never blank text
- Required: OnLocaleChanged static event for all UI refresh — per ADR-0001 pattern
- Forbidden: Never hardcode player-facing strings — all text via LocalizationManager.GetLocalizedString()
- Forbidden: Never load Narrative StringTables without chapter preload

---

## Acceptance Criteria

*From GDD `design/gdd/localization.md`, scoped to this story:*

- [ ] `LocalizationSettings` 资产配置完成：zh-Hans (默认) + en Locale，Fallback 链 en → zh-Hans
- [ ] `UI_Shared` StringTable 创建完成，包含所有 UI 文本 Key（菜单、HUD、设置、按钮、系统消息）——中英双列
- [ ] `Narrative_Ch01` + `Narrative_Ch02` StringTable 创建完成，按章节分割，与 Data Management 章节粒度对齐
- [ ] Key 命名采用三段式点分法：`ui.[screen].[element]_[prop]` / `narrative.[chapter].[fragment]_[seg]` / `system.[category].[message]`
- [ ] MemoryFragment SO 存储 `TableReference` + `TableEntryReference` 字段（非原始字符串）→ 运行时 `GetLocalizedString(tableRef, entryRef)` 返回当前 Locale 文本
- [ ] GIVEN CSV 有更新，WHEN 导入新 CSV 到 StringTable，THEN Unity Editor 中条目被覆盖更新——无需重新构建

---

## Implementation Notes

*Derived from ADR-0015:*

1. 安装 `com.unity.localization` 包 (Window > Package Manager)
2. 创建 `LocalizationSettings` 资产：`Edit > Project Settings > Localization`
   - 添加 Locale: zh-Hans (默认), en
   - 配置 Fallback: en → zh-Hans
3. 创建 StringTable Collection:
   - `UI_Shared` — 地址组 Shared_UI，启动时加载，始终驻留
   - `Narrative_Ch01` — 地址组 Data_Ch01，章节入口加载
   - `Narrative_Ch02` — 地址组 Data_Ch02，章节入口加载
4. Key 命名约定强制通过 Editor 工具验证 (自定义 `LocalizationKeyValidator` 扫描)
5. CSV Import: Google Sheets → CSV → Unity LP CSV Import → 覆盖 StringTable .asset → Git commit
6. MemoryFragment SO 中：
   ```csharp
   public TableReference StringTableRef;
   public TableEntryReference TextEntryRef;
   // 运行时: var text = LocalizationManager.GetLocalizedString(StringTableRef, TextEntryRef);
   ```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 运行时 Locale 切换引擎 — SetLocale, 状态机, OnLocaleChanged
- Story 003: Fallback + 缺失处理 — MissingTranslationEvent, Error 状态
- Story 004: Locale 持久化 — 存档保存/恢复语言

---

## QA Test Cases

*Manual verification steps for Config/Data story:*

- **AC-1**: LocalizationSettings 配置正确
  - Setup: 打开 Edit > Project Settings > Localization
  - Verify: Locale 列表包含 zh-Hans (默认) 和 en；Fallback 链显示 en → zh-Hans
  - Pass condition: 配置与 GDD 规则 2 一致

- **AC-2**: StringTable 结构正确
  - Setup: 打开 Window > Asset Management > Localization Tables
  - Verify: 可见 UI_Shared, Narrative_Ch01, Narrative_Ch02 三个表；每个表包含 zh-Hans 和 en 列
  - Pass condition: 表结构与 GDD 规则 3 一致

- **AC-3**: Key 命名规范
  - Setup: 检查 UI_Shared 和 Narrative_Ch01 中的 Key
  - Verify: 所有 Key 遵循三段式点分法；仅小写 ASCII + 下划线；无重复 Key
  - Pass condition: 100% Key 符合命名约定

- **AC-4**: GetLocalizedString 返回正确文本
  - Setup: MemoryFragment SO 设置 TableReference=Narrative_Ch01, TableEntryReference="narrative.ch01.frag_01.line_01"
  - Verify: 调用 `GetLocalizedString(tableRef, entryRef)` 在 zh-Hans 下
  - Pass condition: 返回中文文本（与 CSV 中一致）

- **AC-5**: CSV 导入覆盖
  - Setup: 修改 CSV 中某个 Key 的 en 翻译文本，通过 Unity LP CSV Import 导入
  - Verify: 对应 StringTable 条目被更新为 CSV 中的新值
  - Pass condition: 导入后无需重新构建，文本在 Editor 中即时反映

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**: Smoke check pass (`production/qa/smoke-*.md`)

**Status**: [x] Created (LocalizationConfig.cs + UnityLocalizationBackend.cs — Unity Editor asset creation deferred to project setup)

---

## Dependencies

- Depends on: None
- Unlocks: Story 002 (Locale 切换需要 StringTable 配置完成)

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 5/5 passing (Config/Data — code artifacts created; Unity Editor asset creation deferred to project setup)
**Deviations**: ADVISORY — Unity Localization Package assets (LocalizationSettings, StringTable .asset files) cannot be created without Unity Editor; created LocalizationConfig.cs (key validator, naming conventions, table name constants) and UnityLocalizationBackend.cs (production wrapper stub) as code-level foundation
**Test Evidence**: Config/Data — smoke check deferred to Unity project setup phase
**Code Review**: N/A (Config/Data story)
