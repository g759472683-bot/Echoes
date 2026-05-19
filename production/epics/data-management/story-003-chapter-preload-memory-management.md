# Story 003: 章节预加载 + 内存管理

> **Epic**: 数据管理系统 (DataManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/data-management.md`
**Requirement**: `TR-data-management-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0002: 数据管理策略
**ADR Decision Summary**: Addressables 加载 + 三态异步就绪模型 + 章节预加载在剩余 ≤3 碎片时触发 + `UnloadChapter` 释放旧章资产

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `DownloadDependenciesAsync` 超时行为需在 IL2CPP 构建中验证；`Addressables.Release` 在 Fast Mode 下行为与构建不同

**Control Manifest Rules (Foundation Layer)**:
- Required: Chapter preload trigger at ≤3 fragments remaining — preload next chapter assets in background — source: ADR-0002, ADR-0004
- Required: Addressables for all asset loading
- Guardrail: Addressables fragment load: <100ms for cached, <500ms for initial

---

## Acceptance Criteria

*From GDD `design/gdd/data-management.md`, scoped to this story:*

- [x] GIVEN 玩家在章节 1 最后一个碎片，WHEN 剩余碎片数 ≤ 3，THEN 自动触发 `PreloadChapterAsync("ch2")`，后台加载章节 2 的插图。到达章节 2 时插图已就绪
- [x] `PreloadChapterAsync(string chapterKey)` 调用 `DownloadDependenciesAsync` 下载下一章 AssetBundle 依赖——fire-and-forget 模式（不阻塞当前碎片交互）
- [x] `UnloadChapter(string chapterKey)` 调用 `Addressables.Release()` 释放指定章节的所有资产引用——章节切换时旧章插图从内存移除
- [x] 预加载失败不抛异常——仅日志警告，主加载路径（`GetIllustrationAsync`）在章节切换时重试
- [x] 章节插图缓存策略：当前章 + 下一章（最多 2 个章的插图在内存中）

---

## Implementation Notes

*Derived from ADR-0002 Implementation Guidelines:*

预加载触发逻辑：
- 在 `GetFragmentAsync` 或章节进度更新时检查：当前章节剩余未访问碎片 ≤ `_preloadThreshold`（默认 3）→ 触发下一章预加载
- `PreloadChapterAsync` 内部调用 `Addressables.DownloadDependenciesAsync(chapterArtGroupKey)` 
- 预加载在后台 Task 中运行——不 await，fire-and-forget（但记录 Task 便于章节过渡时 await）

UnloadChapter 逻辑：
- 遍历该章节所有已缓存的 asset key → `Addressables.Release(handle)`
- 从 `_cache` 和 `_readiness` 字典中移除相关条目
- 不卸载 Shared_UI 和 Shared_Audio 组资产

可配置参数 (Tuning Knobs)：
- `_preloadThreshold`: 剩余碎片数触发阈值（默认 3，安全范围 1–5）
- `_chapterIllustrationCache`: 缓存策略 — CurrentOnly / CurrentPlusNext / All（默认 CurrentPlusNext）

预加载失败处理：
```csharp
try
{
    await Addressables.DownloadDependenciesAsync(groupKey).Task;
}
catch (Exception ex)
{
    Debug.LogWarning($"Preload failed for chapter {chapterKey}: {ex.Message}");
    // 不抛异常 — 主加载路径将在章节过渡时重试
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 异步加载引擎 — `GetAsync<T>()` 核心逻辑（PreloadChapterAsync 依赖它）
- Story 004: 数据验证 — 三层验证逻辑
- 章节过渡转场效果 — 由场景管理系统 (#6) 负责

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: 预加载触发
  - Given: 当前章节有 10 个碎片，玩家访问到第 7 个（剩余 3 个）
  - When: 当前碎片加载完成
  - Then: `PreloadChapterAsync(nextChapterKey)` 被自动调用；预加载 Task 在后台运行，不阻塞当前碎片交互
  - Edge cases: 剩余碎片恰为阈值边界 (3 → 触发, 4 → 不触发)；章节无下一章时不触发

- **AC-2**: 预加载完成后即时返回
  - Given: 章节 2 的预加载已完成
  - When: 玩家到达章节 1 末尾，场景管理器调用 `PreloadChapterAsync("ch2")` 并 await
  - Then: Task 立即完成（已缓存），不重复下载
  - Edge cases: 预加载尚未完成时章节过渡 → await 等待完成（不超时则正常，超时用默认插画）

- **AC-3**: UnloadChapter 释放内存
  - Given: 章节 1 的插图和碎片元数据在缓存中
  - When: 调用 `UnloadChapter("ch1")`
  - Then: `_cache` 中章节 1 的资产条目被移除；`Addressables.Release()` 被调用；Shared_UI 和 Shared_Audio 资产不受影响
  - Edge cases: 卸载正在预加载的章节 → 先取消预加载 Task，再释放已缓存资产

- **AC-4**: 预加载失败不崩溃
  - Given: 下一章的 Addressables 组不存在或网络不可用
  - When: `PreloadChapterAsync` 运行中发生异常
  - Then: 异常被捕获，`Debug.LogWarning` 记录；游戏继续正常运行；章节过渡时 `GetIllustrationAsync` 重新尝试加载
  - Edge cases: 连续 3 次预加载失败 → 仍不崩溃，每次独立日志

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/data-management/preload_test.cs` — must exist and pass

**Status**: [x] Created

---

## Dependencies

- Depends on: Story 002 (需要 GetAsync 引擎 + 状态机 Ready 状态)
- Unlocks: 场景管理系统 (#6) 的章节过渡功能

---

## Completion Notes

**Completed**: 2026-05-15
**Criteria**: 5/5 passing
**Deviations**: None — manifest version matches (2026-05-12), all ADR-0002 constraints compliant
**Test Evidence**: Logic — `tests/unit/data-management/preload_test.cs` (12 test methods), `tests/unit/data-management/async_engine_test.cs` (extended mock)
**Code Review**: Complete — 5 issues found and fixed (download handle memory leak, EnforceCacheStrategy next-chapter exclusion, SetCurrentChapter cache enforcement, stale doc comment, ResetCounts clears DownloadedLabels)
