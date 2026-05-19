# ADR-0002: 数据管理策略 — Addressables + 三态异步就绪模型

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

游戏需要按章节按需加载 MemoryFragment、插画、音频等资产。决定使用 Unity Addressables 系统 + 三态异步就绪模型（Cached/Loading/NotRequested）+ 并发请求去重（同一 key 的并发 GetAsync 返回同一个 Task 引用）。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | MEDIUM — Addressables 6.2+ 异常行为变更（加载失败从静默 null 变为抛异常） |
| **References Consulted** | `VERSION.md`, `breaking-changes.md`, `current-best-practices.md` |
| **Post-Cutoff APIs Used** | `Addressables.LoadAssetAsync<T>()`, `Addressables.Release()`, `Addressables.DownloadDependenciesAsync()` |
| **Verification Required** | IL2CPP 构建中 Addressables 异常类型与 Editor 一致；`DownloadDependenciesAsync` 超时行为 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (事件总线架构 — 加载完成通知) |
| **Enables** | ADR-0004 (SceneManager 预加载), ADR-0007 (SO + overlay 模式), ADR-0009 (关联引擎数据查询) |
| **Blocks** | Foundation + Core 层 Epic — 所有系统依赖数据加载 |
| **Ordering Note** | 在 ADR-0001 之后、ADR-0004 之前创建 |

## Context

### Problem Statement

回响 (Echoes) 按章节组织内容（每章 20-40 个碎片，每个碎片包含插画、文本、交互对象、情感标签等）。加载全部内容到内存不可行（2GB 预算），需要按需加载 + 预加载策略 + 内存管理。

### Constraints

- 内存上限 2GB（所有资产 + 运行时数据）
- 帧预算 16.6ms — 资产加载不得阻塞主线程
- Addressables 6.2+ 加载失败抛出异常（非静默 null）— 需 try/catch 包装
- 碎片转场 < 500ms（包括资产加载 + 动画）

### Requirements

- 按章节按需加载：按 chapterKey 加载一组的 MemoryFragment SO 引用
- 预加载：上一碎片展示期间预测并预加载下一碎片资产
- 并发安全：对同一 key 的并发加载请求返回同一 Task 引用
- 内存管理：章节切换时卸载上一章资产
- IL2CPP 兼容

## Decision

**使用 Unity Addressables + 三态异步就绪模型 + 并发请求去重。**

### 三态模型

每个 asset key 处于三种状态之一：

```
NotRequested ──► Loading ──► Cached
                    │
                    └──► (失败) ──► NotRequested (可重试)
```

- **Cached**: 资产在内存中，`GetAsync` 返回已完成 Task
- **Loading**: 正在加载中，后续请求返回同一 Task 引用
- **NotRequested**: 未被请求或已释放

### 并发去重核心逻辑

```csharp
private readonly Dictionary<string, Task> _pendingLoads = new();

public async Task<T> GetAsync<T>(string key) where T : class
{
    // 已在内存 → 直接返回
    if (_cache.TryGetValue(key, out var cached) && cached is T result)
        return result;

    // 正在加载 → 返回同一 Task（去重）
    if (_pendingLoads.TryGetValue(key, out var existingTask))
        return await (Task<T>)existingTask;

    // 发起新加载
    var tcs = new TaskCompletionSource<T>();
    _pendingLoads[key] = tcs.Task;

    try
    {
        var handle = Addressables.LoadAssetAsync<T>(key);
        var asset = await handle.Task;
        _cache[key] = asset;
        tcs.SetResult(asset);
        return asset;
    }
    catch (Exception ex)
    {
        tcs.SetException(new DataLoadException(key, ex));
        throw;
    }
    finally
    {
        _pendingLoads.Remove(key);
    }
}
```

### 章节生命周期

```
PreloadChapterAsync(chapterKey)   → 下载依赖、预热关键资产
GetFragmentAsync(chapterKey, id)  → 按需加载单个碎片
UnloadChapter(chapterKey)         → Addressables.Release() 释放章节所有资产
```

### Architecture Diagram

```
┌─────────────────────────────────────────────────────┐
│                    IDataManager                       │
│                                                      │
│  _cache: Dictionary<string, object>                  │
│  _pendingLoads: Dictionary<string, Task>             │
│  _readiness: Dictionary<string, Readiness>           │
│                                                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────────────┐  │
│  │ Cached   │  │ Loading  │  │ NotRequested     │  │
│  │ (立即)   │  │ (await)  │  │ (发起加载)       │  │
│  └──────────┘  └──────────┘  └──────────────────┘  │
│                                                      │
│  GetAsync<T>(key)                                    │
│    ├─ Cached? → return cached                        │
│    ├─ Loading? → await existing Task (去重)           │
│    └─ NotRequested? → Addressables.Load + cache      │
└─────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────┐
│  Addressables                                        │
│  ├─ ChapterDefinitions (ScriptableObject[])          │
│  ├─ MemoryFragment (ScriptableObject, per fragment)  │
│  ├─ Illustrations (Sprite, per fragment)             │
│  └─ AudioClips (per chapter)                         │
└─────────────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public interface IDataManager
{
    Task<ChapterDefinition> GetChapterAsync(string chapterKey);
    Task<MemoryFragment> GetFragmentAsync(string chapterKey, string fragmentId);
    Task<Sprite> GetIllustrationAsync(string assetKey);
    Task PreloadChapterAsync(string chapterKey);
    bool IsReady(string assetKey);
    List<MemoryFragment> GetFragmentsByChapter(string chapterKey);
    void UnloadChapter(string chapterKey);
}

public class DataLoadException : Exception
{
    public string AssetKey { get; }
    public DataLoadException(string key, Exception inner)
        : base($"Failed to load asset: {key}", inner)
    {
        AssetKey = key;
    }
}
```

### Implementation Guidelines

1. 所有公开方法返回 `Task<T>` — 调用方必须 await，禁止 `.Result`
2. `Addressables.LoadAssetAsync<T>()` 必须 try/catch 包装
3. 预加载失败不抛异常 — 仅日志警告，主加载路径重试
4. `UnloadChapter` 调用 `Addressables.Release()` — 释放 AssetBundle 引用
5. 线程安全：`_cache` 和 `_pendingLoads` 操作在 Unity 主线程（通过 Task 调度器保证）

## Alternatives Considered

### Alternative 1: Resources.Load (同步加载)

- **Description**: 使用 Unity 传统 `Resources` 文件夹 + `Resources.Load<T>()`
- **Pros**: 简单，同步，无外部包依赖
- **Cons**: 内存中驻留（无 AssetBundle 级别释放）；启动时构建 Resources 索引慢；Unity 官方不推荐用于生产项目
- **Rejection Reason**: 无法按章节释放内存；2GB 内存预算无法容纳全量资产

### Alternative 2: AssetBundles 手动管理

- **Description**: 手动构建、分组、加载 AssetBundles，不使用 Addressables
- **Pros**: 完全控制；无 Addressables 包开销
- **Cons**: 需要自行管理引用计数、依赖解析、内容目录；开发成本高
- **Rejection Reason**: Addressables 提供开箱即用的依赖管理和内容目录；手动方案投入产出比低

## Consequences

### Positive

- 按章节释放内存（`UnloadChapter` → `Addressables.Release()`）
- 并发安全：去重避免同一资产重复加载
- 预加载可预测性：三态模型明确资产就绪状态
- 可测试：纯 C# 接口，单元测试 mock IDataManager

### Negative

- Addressables 包依赖（版本升级可能引入 breaking changes）
- 异步编程复杂度（所有消费方需要 await）
- 需要处理 Addressables 异常（6.2+ 行为变更）
- 首次加载可能有延迟（AssetBundle 下载）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Addressables 版本更新 breaking change | Low | High | 锁定 Addressables 版本在 manifest.json；升级前完整回归测试 |
| IL2CPP 中 Addressables 异常类型不一致 | Low | Medium | Pre-Production 阶段 IL2CPP 构建验证 |
| 碎片转场中加载超时 | Medium | Medium | 预加载机制（剩余≤3碎片触发）；超时回退到默认插画 |
| _cache 内存膨胀 | Medium | Medium | UnloadChapter 主动释放；内存监控 + 阈值告警 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (首次加载 Addressables asset) | ~2-5ms (I/O wait, 已在 Task 中) |
| CPU (缓存命中 GetAsync) | ~0.001ms (Dictionary lookup) |
| Memory (per loaded chapter) | ~10-30MB (取决于插画和音频) |
| Load Time (PreloadChapter) | ~1-3s (后台下载，不阻塞 UI) |
| GC Allocation (GetAsync cached) | 0 (Task 已完成，无分配) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] 同一 key 的 3 个并发 `GetAsync` 调用只触发 1 次 `Addressables.LoadAssetAsync`
- [ ] `UnloadChapter` 后 `GetFragmentAsync` 重新加载（非缓存返回）
- [ ] `Addressables.LoadAssetAsync` 失败时抛出 `DataLoadException`，包含 assetKey
- [ ] `PreloadChapterAsync` 失败不抛异常（仅日志警告）
- [ ] 100 次并发 `GetAsync` 无死锁

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `data-management.md` (#2) | 数据管理 | 三态异步就绪模型 | 本 ADR 定义完整三态模型和状态转换规则 |
| `data-management.md` (#2) | 数据管理 | 避免重复网络请求 | Task 去重：_pendingLoads Dictionary 复用 |
| `data-management.md` (#2) | 数据管理 | 资产预加载策略 | PreloadChapterAsync 实现 |
| `scene-management.md` (#6) | 场景管理 | 碎片资产按需加载 | GetFragmentAsync/GetIllustrationAsync |
| `audio-system.md` (#3) | 音频 | 章节音频预加载 | PreloadChapterAudioAsync 复用三态模型 |

## Related

- ADR-0001 — 加载完成通知使用 static event
- ADR-0004 — SceneManager 消费 IDataManager 接口
- `docs/architecture/architecture.md` §4.1 — IDataManager API 边界
