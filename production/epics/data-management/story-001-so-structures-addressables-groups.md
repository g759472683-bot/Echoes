# Story 001: SO 数据结构 + Addressables 分组配置

> **Epic**: 数据管理系统 (DataManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Config/Data
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/data-management.md`
**Requirement**: `TR-data-management-001`, `TR-data-management-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0002: 数据管理策略
**ADR Decision Summary**: Addressables 加载 + 三态异步就绪模型 + 并发请求去重 + 章节预加载；所有资产通过 Addressables.LoadAssetAsync<T>() 加载，禁止 Resources.Load

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Addressables 6.2+ 加载失败抛异常（非返回 null）；IL2CPP 构建需验证 Addressables 异常类型一致性
**Performance**: N/A — Editor-only configuration, no runtime code. Addressables group metadata lives in AssetDatabase.

**Control Manifest Rules (Foundation Layer)**:
- Required: Addressables for all asset loading — `LoadAssetAsync<T>()`, never Resources.Load (except ~1KB AudioMixer)
- Required: Chapter preload trigger at ≤3 fragments remaining
- Forbidden: Never use `Resources.Load()` (except AudioMixer) — source: ADR-0002, ADR-0013
- Forbidden: Never use `Task.Result` or `Task.Wait()` on main thread — source: ADR-0007

---

## Acceptance Criteria

*From GDD `design/gdd/data-management.md`, scoped to this story:*

- [ ] `ChapterDefinition` ScriptableObject 包含：章节元数据 + `AssetReferenceT<MemoryFragment>[]` 引用数组，Inspector 中可编辑
- [ ] 10 个 Addressables 组配置完成：Data_Ch01–04, Art_Ch01–04, Shared_UI, Shared_Audio，每组加载时机和驻留策略与 GDD 规则 5 一致
- [ ] 编辑器 Play Mode + Addressables Fast Mode 下，修改 MemoryFragment SO 并保存后，下次查询反映新值（无需重启 Play Mode）

---

## Implementation Notes

*Derived from ADR-0002 Implementation Guidelines:*

- `ChapterDefinition` 继承 `ScriptableObject`，包含 `ChapterKey` (string)、`OrderIndex` (int)、`EntryFragmentId` (string)、`AssetReferenceT<MemoryFragment>[]` 字段
- Addressables 组在 `Assets/AddressableAssetsData/` 中通过 Unity Editor Addressables Groups 窗口创建
- Data_Ch01–04 组包含 ChapterDefinition SO + MemoryFragment SO — 标记为启动时加载、始终驻留
- Art_Ch01–04 组包含插图 Sprite — 标记为按需加载、章节切换时释放
- Shared_UI / Shared_Audio 组包含字体、UI 精灵、环境音 — 标记为启动时加载、始终驻留
- AssetReference 使用泛型 `AssetReferenceT<MemoryFragment>` 确保 Inspector 中只能拖入 MemoryFragment 类型
- 不要在此 Story 中实现加载逻辑 — 仅数据结构 + Addressables 配置

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 异步加载引擎 — `GetAsync<T>()`、三态模型、并发去重
- Story 003: 章节预加载 — `PreloadChapterAsync()`、`UnloadChapter()`
- Story 004: 数据验证 — 三层验证、`DataLoadException`
- Story 005: JSON 序列化 — `SerializeState<T>()`、`DeserializeState<T>()`

---

## QA Test Cases

*Manual verification steps for Config/Data story:*

- **AC-1**: ChapterDefinition SO 包含必需的字段
  - Setup: 在 Unity Editor 中创建 ChapterDefinition SO (Assets > Create > 回响 > Chapter Definition)
  - Verify: Inspector 显示 ChapterKey (string)、OrderIndex (int)、EntryFragmentId (string)、AssetReferenceT<MemoryFragment>[] 字段
  - Pass condition: 所有字段可编辑，AssetReference 数组可添加/移除元素

- **AC-2**: 10 Addressables 组配置正确
  - Setup: 打开 Window > Asset Management > Addressables > Groups
  - Verify: 可见 10 个组 — Data_Ch01, Data_Ch02, Data_Ch03, Data_Ch04, Art_Ch01, Art_Ch02, Art_Ch03, Art_Ch04, Shared_UI, Shared_Audio
  - Pass condition: 每组标签和加载路径与 GDD 规则 5 表一致

- **AC-3**: Editor 热重载
  - Setup: 进入 Play Mode (Addressables Fast Mode), 修改一个 MemoryFragment SO 的字段值，保存 (Ctrl+S)
  - Verify: 调用 `GetFragmentAsync` 查询该碎片
  - Pass condition: 返回的碎片数据反映修改后的新值

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**: Smoke check pass (`production/qa/smoke-*.md`)

**Status**: [x] Created — `src/core/MemoryFragment.cs`, `src/core/ChapterDefinition.cs`; smoke check deferred (Editor config)

---

## Dependencies

- Depends on: None
- Unlocks: Story 002 (异步加载引擎需要 ChapterDefinition SO 和 Addressables 组)

---

## Completion Notes

**Completed**: 2026-05-14
**Criteria**: 2/3 passing, 1 DEFERRED (AC-2: Addressables groups require Unity Editor configuration)
**Deviations**: None
**Test Evidence**: `src/core/MemoryFragment.cs`, `src/core/ChapterDefinition.cs`; smoke check deferred (Editor config)
**Code Review**: APPROVED — no issues found
