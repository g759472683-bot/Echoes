# Story 002: 异步加载引擎 + 状态机

> **Epic**: 数据管理系统 (DataManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/data-management.md`
**Requirement**: `TR-data-management-002`, `TR-data-management-007`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0002: 数据管理策略
**ADR Decision Summary**: Addressables 加载 + 三态异步就绪模型 (Cached/Loading/NotRequested) + 并发请求去重（同一 key 的并发 GetAsync 返回同一 Task 引用）

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Addressables 6.2+ 加载失败抛异常（非返回 null）——所有加载调用包裹 try/catch；IL2CPP 构建需验证异常类型一致性

**Control Manifest Rules (Foundation Layer)**:
- Required: Three-state async readiness model — Cached / Loading / NotRequested for all loaded assets
- Required: Concurrent request deduplication — same-key loads return shared Task reference
- Required: `static event Action<T>` for all cross-system communication — source: ADR-0001
- Forbidden: Never use `Task.Result` or `Task.Wait()` on main thread — causes deadlock in Unity SynchronizationContext — source: ADR-0007
- Guardrail: Addressables fragment load: <100ms for cached, <500ms for initial — source: ADR-0002

---

## Acceptance Criteria

*From GDD `design/gdd/data-management.md`, scoped to this story:*

- [ ] GIVEN 游戏启动，WHEN 引擎完成初始化，THEN DataManager 自动加载所有碎片元数据，进入 Ready 状态。元数据加载时间 < 2 秒
- [ ] GIVEN DataManager 处于 Ready 状态，WHEN 任意系统调用 `GetFragmentAsync("ch1", "frag_01")`，THEN 返回该碎片的完整定义（包含情感标签、交互物件、选项分支），延迟 < 50ms（内存中）
- [ ] GIVEN 某碎片的插图尚未加载，WHEN 调用 `GetIllustrationAsync("art_ch1_letter")`，THEN 返回 `Task<Sprite>`——Await 后获得 Sprite 对象
- [ ] 三态就绪模型正确运转：Cached 资产同步返回（无 Task 分配）、Loading 资产返回已有 Task（去重）、NotRequested 资产自动发起加载
- [ ] 两个系统同时请求同一尚未加载的资产时，返回同一个 Task 引用——不发起重复的 Addressables 加载
- [ ] `IsReady(string assetKey)` 同步返回资产就绪状态，不触发加载

---

## Implementation Notes

*Derived from ADR-0002 Implementation Guidelines:*

核心数据结构：
```csharp
private readonly Dictionary<string, object> _cache = new();
private readonly Dictionary<string, Task> _pendingLoads = new();
private readonly Dictionary<string, Readiness> _readiness = new();

private enum Readiness { NotRequested, Loading, Cached }
```

核心逻辑 — GetAsync：
1. 检查 `_cache` → Cached 则直接返回已完成 Task (`Task.FromResult`)
2. 检查 `_pendingLoads` → Loading 则返回已有 Task（并发去重）
3. 否则发起 `Addressables.LoadAssetAsync<T>(key)`，将 Task 存入 `_pendingLoads`
4. 成功：存入 `_cache`，设置 `_readiness[key] = Cached`
5. 失败：包装为 `DataLoadException`，设置 `_readiness[key] = NotRequested`（可重试）
6. finally：从 `_pendingLoads` 移除

状态机 (5 状态)：
- Uninitialized → LoadingMetadata（自动）→ Ready（成功）/ Error（失败）
- Ready ↔ PreloadingChapter（预加载开始/完成）
- Error → Uninitialized（返回主菜单重试）

公开 API：
- `Task<ChapterDefinition> GetChapterAsync(string key)`
- `Task<MemoryFragment> GetFragmentAsync(string chapterKey, string fragmentId)`
- `Task<Sprite> GetIllustrationAsync(string assetKey)`
- `bool IsReady(string assetKey)`

所有公开方法返回 `Task<T>` — 调用方必须 `await`，禁止 `.Result`

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: SO 数据结构 + Addressables 分组（已完成的数据结构）
- Story 003: 章节预加载 + 内存管理 — `PreloadChapterAsync()`、`UnloadChapter()`
- Story 004: 数据验证 + 异常安全 — 三层验证、`DataLoadException` 定义
- Story 005: JSON 序列化桥接

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: 启动加载元数据 < 2s
  - Given: 游戏刚启动，DataManager 处于 Uninitialized 状态
  - When: 引擎完成初始化，DataManager 进入 LoadingMetadata 状态
  - Then: 所有碎片元数据加载完成，DataManager 进入 Ready 状态；加载时间 < 2s
  - Edge cases: 如果元数据加载超时(>10s) → Error 状态；如果没有碎片存在 → Ready 状态 + 空缓存

- **AC-2**: 缓存命中 < 50ms
  - Given: DataManager 处于 Ready 状态，碎片 "ch1/frag_01" 已在缓存中
  - When: 调用 `GetFragmentAsync("ch1", "frag_01")`
  - Then: 返回完整的 MemoryFragment 对象（情感标签、交互物件、选项分支非空）；延迟 < 50ms
  - Edge cases: 查询不存在的碎片 ID → `DataLoadException`；查询不存在章节 → `DataLoadException`

- **AC-3**: GetIllustrationAsync 返回 Sprite
  - Given: 碎片插图的 asset key "art_ch1_letter" 尚未加载
  - When: 调用 `GetIllustrationAsync("art_ch1_letter")` 并 await
  - Then: 返回有效 Sprite 对象（非 null）
  - Edge cases: asset key 不在 Addressables 中 → `DataLoadException` 包含 asset key

- **AC-4**: 并发请求去重
  - Given: asset key "ch2/frag_05" 处于 NotRequested 状态
  - When: 3 个调用方同时调用 `GetFragmentAsync("ch2", "frag_05")`
  - Then: `Addressables.LoadAssetAsync` 仅调用 1 次；3 个调用方收到同一个 Task 引用
  - Edge cases: 加载失败时所有 3 个 Task 都收到 `DataLoadException`

- **AC-5**: IsReady 同步查询
  - Given: asset key "ch1/frag_01" 处于 Cached 状态，"ch2/frag_10" 处于 NotRequested 状态
  - When: 调用 `IsReady("ch1/frag_01")` 和 `IsReady("ch2/frag_10")`
  - Then: 第一个返回 true，第二个返回 false；第二个调用不触发加载

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/data-management/async_engine_test.cs` — must exist and pass

**Status**: [x] Created

---

## Dependencies

- Depends on: Story 001 (需要 ChapterDefinition SO + Addressables 组配置完成)
- Unlocks: Story 003 (预加载需要 GetAsync 引擎), Story 005 (序列化需要 DataManager Ready 状态)

## Completion Notes
**Completed**: 2026-05-14
**Criteria**: 6/6 passing
**Deviations**: ADVISORY: AC-1 QA edge case "元数据加载超时(>10s) → Error" not implemented (no timeout mechanism in LoadMetadata). Defer to Story 003 or follow-up robustness story.
**Test Evidence**: Logic: `tests/unit/data-management/async_engine_test.cs` (36 tests, all 6 ACs covered)
**Code Review**: Complete — 3 issues found and fixed (ReleaseFragment _chapterFragments cleanup, GetAsync<T> type-mismatch guard, AC-1 test assertion correction)
