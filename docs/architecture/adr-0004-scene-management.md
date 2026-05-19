# ADR-0004: 场景管理与转场状态机

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

回响 (Echoes) 有 3 个 Unity 场景（Boot/MainMenu/Game），Game 场景内需要碎片间和章节间转场。决定使用 3 场景架构 + 转场状态机（FragmentTransition/ChapterTransition/SceneTransition）+ SceneFader 全屏墨迹遮罩 + 预加载触发机制。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — `SceneManager.LoadSceneAsync` 是 Unity 核心 API，稳定 |
| **References Consulted** | `VERSION.md`, `current-best-practices.md` |
| **Post-Cutoff APIs Used** | `LoadSceneAsync` (Unity 核心，无版本变更) |
| **Verification Required** | `LoadSceneAsync` 在 IL2CPP 中的完成回调与 Editor 行为一致性 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (转场事件声明), ADR-0002 (IDataManager 资产加载) |
| **Enables** | ADR-0009 (关联引擎 — 转场后计算), ADR-0014 (反馈系统 — 转场中抑制) |
| **Blocks** | ChapterManager Epic — 章节控制依赖场景转场 |
| **Ordering Note** | 在 ADR-0001, ADR-0002 之后创建 |

## Context

### Problem Statement

游戏在 3 个场景之间切换（启动→主菜单→游戏），游戏场景内还需要碎片间转场（同章碎片切换）和章节间转场（章节切换）。转场需要覆盖加载时间、提供视觉反馈、管理交互检测的暂停/恢复。

### Constraints

- 转场总时长 < 500ms（资产加载 + 动画）
- 转场期间交互检测暂停（防止玩家在加载中操作）
- 必须支持预加载（上一碎片展示时预测加载下一碎片）
- SceneFader 使用 UI Toolkit 实现（墨迹展开/收缩视觉风格）

### Requirements

- 3 个 Unity Scene：Boot → MainMenu → Game
- Game 场景内支持 FragmentTransition（同章）和 ChapterTransition（跨章）
- 全屏 SceneFader 遮罩覆盖资产加载
- 转场事件：OnFragmentTransitionStarted / OnFragmentTransitioned
- 预加载触发：剩余 ≤3 个未访问碎片时自动触发

## Decision

**使用 3 场景 + 转场状态机 + SceneFader + 预加载触发。**

### 转场状态机

```
                    ┌──────────────┐
                    │    IDLE      │ ← 无转场进行中
                    └──────┬───────┘
                           │ TransitionToFragment/Chapter/Scene
                           ▼
                    ┌──────────────┐
           ┌───────│  FADING_OUT  │ ← SceneFader 墨迹展开
           │       └──────┬───────┘
           │              │ 动画完成
           │              ▼
           │       ┌──────────────┐
           │       │   LOADING    │ ← 资产加载 / 场景加载
           │       └──────┬───────┘
           │              │ 加载完成
           │              ▼
           │       ┌──────────────┐
           │       │  FADING_IN   │ ← SceneFader 墨迹收缩
           │       └──────┬───────┘
           │              │ 动画完成 → OnFragmentTransitioned
           │              ▼
           │       ┌──────────────┐
           └──────►│    IDLE      │
                   └──────────────┘
```

### 转场类型

| 类型 | 触发 | 加载内容 | SceneFader |
|------|------|---------|------------|
| **SceneTransition** | LoadSceneAsync("Game"/"MainMenu") | 新场景 | 是 |
| **ChapterTransition** | TransitionToChapterAsync(chapterKey) | 新章节资产 + 第一个碎片 | 是 |
| **FragmentTransition** | TransitionToFragmentAsync(chapterKey, id) | 新碎片资产 | 是 (快速) |

### 预加载触发逻辑

```csharp
// 在 OnFragmentTransitioned 中检查
var remaining = totalFragments - visitedFragments.Count;
if (remaining <= 3 && !_preloadedThisChapter)
{
    _preloadedThisChapter = true;
    await DataManager.PreloadChapterAsync(chapterKey);
}
```

### Architecture Diagram

```
┌──────────────────────────────────────────────────────┐
│                  ISceneManager                         │
│                                                       │
│  LoadSceneAsync("MainMenu")     ──── SceneTransition  │
│  TransitionToChapterAsync(key)  ──── ChapterTransition│
│  TransitionToFragmentAsync(k,f) ──── FragmentTransition│
│  PreloadChapterAsync(key)                             │
│                                                       │
│  Events:                                              │
│    OnFragmentTransitionStarted(chapterKey, fragmentId)│
│    OnFragmentTransitioned(chapterKey, fragmentId)      │
└──────────────────────────────────────────────────────┘
         │                    │
         ▼                    ▼
┌─────────────────┐  ┌──────────────────┐
│  #2 DataManager  │  │  SceneFader      │
│  (资产加载)       │  │  (UI Toolkit      │
│                  │  │   VisualElement)  │
└─────────────────┘  └──────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────┐
│  3 个 Unity Scene                                    │
│  Boot → MainMenu → Game                              │
│  (Build Settings 中注册)                              │
└─────────────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public interface ISceneManager
{
    // 场景级
    Task LoadSceneAsync(string sceneName);

    // 转场
    Task TransitionToFragmentAsync(string chapterKey, string fragmentId);
    Task TransitionToChapterAsync(string chapterKey);

    // 预加载
    Task PreloadChapterAsync(string chapterKey);

    // 事件
    // static event Action<string, string> OnFragmentTransitionStarted;
    // static event Action<string, string> OnFragmentTransitioned;
}

public enum TransitionType
{
    SceneTransition,
    ChapterTransition,
    FragmentTransition
}
```

### Implementation Guidelines

1. SceneFader 使用 UI Toolkit `VisualElement` + USS `transition` 实现（~300ms 动画）
2. `OnFragmentTransitionStarted` 在 FADING_OUT 开始时触发
3. `OnFragmentTransitioned` 在 FADING_IN 完成后触发
4. 转场中拒绝新的转场请求（检查状态机 != IDLE）
5. Boot 场景只做初始化（Phase 1+2），完成后自动跳转 MainMenu
6. 预加载是 fire-and-forget — 失败不阻塞当前碎片

## Alternatives Considered

### Alternative 1: 单场景 + 全部 Prefab 动态加载

- **Description**: 只用一个 Game 场景，所有 UI 用 UIDocument 动态构建，场景切换用 Prefab 加载/卸载
- **Pros**: 无场景异步加载复杂度
- **Cons**: 启动时场景为空（无预览效果）；内存管理完全依赖 Addressables Release（容易遗漏）；Unity Scene 边界天然隔离被浪费
- **Rejection Reason**: 3 场景架构利用 Unity Scene 天然隔离（Boot 后释放启动资源、MainMenu 与 Gameplay 内存隔离）

### Alternative 2: 使用 Unity SceneManager.LoadScene (同步)

- **Description**: 同步加载场景，加载期间游戏冻结
- **Pros**: 代码简单，无异步复杂度
- **Cons**: 场景加载可能 > 500ms → 冻结感明显；无法显示 SceneFader 动画（主线程阻塞）
- **Rejection Reason**: 同步加载导致明显卡顿，用户体验差

## Consequences

### Positive

- Scene 隔离：MainMenu 和 Game 场景内存完全分离（前台场景占内存，后台释放）
- 状态机保证转场原子性
- 预加载减少碎片间等待
- SceneFader 提供视觉连贯性

### Negative

- 3 场景增加了 Build Settings 配置
- 转场中需要暂停交互检测（额外状态管理）
- 异步加载复杂度（需处理超时、取消）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 转场中再次触发转场 | Medium | Medium | 状态机 != IDLE 时拒绝请求 |
| LoadSceneAsync 在 IL2CPP 行为不一致 | Low | Medium | Pre-Production IL2CPP 构建验证 |
| 预加载未完成就触发转场 | Medium | Low | GetFragmentAsync 内部处理 Loading 状态（三态模型），无损 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (状态机检查) | ~0.001ms |
| SceneFader 动画 | ~300ms (USS transition, GPU 端) |
| Scene Load (空场景) | ~50-100ms |
| Fragment Load (含资产) | ~200-400ms (取决于插画大小) |
| Memory (场景切换释放) | -10~30MB (前场景资产) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] Boot → MainMenu → Game 完整场景流程可走通
- [ ] FragmentTransition 中拒绝新转场请求
- [ ] OnFragmentTransitionStarted/Transitioned 事件按正确时序触发
- [ ] SceneFader 动画覆盖整个加载时间
- [ ] 剩余≤3碎片时自动触发预加载

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `scene-management.md` (#6) | 场景管理 | 3 场景架构 (Boot/MainMenu/Game) | 本 ADR 定义场景结构和切换策略 |
| `scene-management.md` (#6) | 场景管理 | FragmentTransition/ChapterTransition 状态机 | 转场状态机完整定义 |
| `scene-management.md` (#6) | 场景管理 | OnFragmentTransitionStarted/Transitioned 事件 | static event 声明在 SceneManager |
| `scene-management.md` (#6) | 场景管理 | 预加载触发 (≤3 碎片) | 本 ADR 定义触发逻辑 |
| `scene-management.md` (#6) | 场景管理 | SceneFader 全屏墨迹遮罩 | UI Toolkit VisualElement + USS transition |
| `interaction-feedback.md` (#18) | 交互反馈 | 转场开始时抑制反馈 | OnFragmentTransitionStarted → _feedbackSuppressed = true |
| `scroll-interaction-system.md` (#11) | 画卷交互 | 转场中暂停交互检测 | OnFragmentTransitionStarted → 暂停; OnFragmentTransitioned → 重建 |

## Related

- ADR-0001 — 转场事件使用 static event 模式
- ADR-0002 — IDataManager 提供资产加载
- `docs/architecture/architecture.md` §4.1 — ISceneManager API 边界
