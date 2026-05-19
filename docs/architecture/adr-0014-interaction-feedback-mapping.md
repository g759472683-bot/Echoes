# ADR-0014: 交互反馈映射表 — 10 事件 → 视觉+音频响应

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

玩家的每次交互（悬停、点击、拖拽、选择等）需要获得一致的视觉+音频反馈。决定使用事件→反馈映射表（10 个交互事件 → 视觉+音频响应对）+ 优先级系统 + 防抖 + 转场抑制。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — 纯 C# 事件订阅，无 Unity 特定 API |
| **References Consulted** | `VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (10 个 static event 订阅), ADR-0012 (PlayTriggered/PlayFeedback 动画), ADR-0013 (PlaySFX 音效) |
| **Enables** | None |
| **Blocks** | InteractionFeedback Epic |
| **Ordering Note** | 依赖 ADR-0012, ADR-0013 — 动画和音频系统必须先就绪 |

## Context

### Problem Statement

交互反馈如果每个系统独立实现会导致体验不一致 —— 同一类型的交互可能收到不同的视觉/音频回应。需要一个集中映射表：事件类型 → 反馈响应，保证一致性。

### Constraints

- 10 个交互事件类型（由 InteractionManager 声明）
- 反馈必须 < 50ms 延迟（即时感知）
- 同一对象 300ms 内防抖（防止悬停/点击事件风暴）
- 转场中反馈必须抑制

### Requirements

- 事件→反馈映射表（可配置）
- 反馈优先级：选项确认 > 拖拽完成 > 交互 > 悬停
- 防抖：同对象 300ms
- 转场抑制 flag（`_feedbackSuppressed`）

## Decision

**事件→反馈映射表 + 优先级 + 防抖 + 转场抑制。**

### 完整映射表

| 事件 | 视觉反馈 | 音频反馈 | 优先级 |
|------|---------|---------|--------|
| `OnChoiceSelected` | L3 朱砂发光 + 选项面板关闭动画 | 确认音效 (confirm_01) | 10 (最高) |
| `OnDragComplete` | 墨迹拖尾消散 | 纸张摩擦 (paper_rustle) | 8 |
| `OnInteract` | L2 发光 + 碎片文本更新 | 交互音效 (interact_01) | 5 |
| `OnDragStart` | 墨迹拖尾开始 + 对象高亮 | — | 4 |
| `OnRevealObject` | 对象显隐过渡动画 | 揭示音效 (reveal_01) | 6 |
| `OnShowText` | 文本面板 fade-in | 文本出现 (text_appear) | 3 |
| `OnHoverEnter` | L1 微光 + 光标变化 | 微弱嗡声 (hover_tick) | 2 |
| `OnHoverExit` | L1 微光消失 + 光标恢复 | — | 1 |
| `OnDragCancel` | 拖尾回弹消失 | — | 1 |
| `OnChoiceHover` | 选项高亮 (ink outline) | 选项悬停 (choice_tick) | 2 |

### 优先级抢占

```csharp
private int _currentPriority = 0;
private float _lastInteractTime = 0;

public void OnEventReceived(string eventName, string objectId)
{
    // 1. 转场抑制检查
    if (_feedbackSuppressed) return;

    // 2. 防抖检查 (同对象 + 同事件, 300ms)
    var key = (objectId, eventName);
    if (Time.time - _lastEventTime.GetValueOrDefault(key, 0) < 0.3f) return;

    // 3. 优先级抢占
    var priority = _mapping[eventName].Priority;
    if (priority < _currentPriority) return; // 低优先级事件被当前反馈抑制

    // 4. 执行反馈
    _currentPriority = priority;
    ExecuteFeedback(eventName);
    _lastEventTime[key] = Time.time;
}
```

### 转场抑制

```csharp
// 订阅 SceneManager 事件
void OnEnable()
{
    SceneManager.OnFragmentTransitionStarted += OnTransitionStart;
    SceneManager.OnFragmentTransitioned += OnTransitionEnd;
}

void OnTransitionStart(string chapterKey, string fragmentId)
{
    _feedbackSuppressed = true;
    _currentPriority = 0; // 重置优先级
}

void OnTransitionEnd(string chapterKey, string fragmentId)
{
    _feedbackSuppressed = false;
}
```

### Architecture Diagram

```
┌──────────────────────────────────────────────┐
│        InteractionFeedback (MonoBehaviour)    │
│                                              │
│  OnEnable()                                  │
│    ├─ InteractionManager.OnHoverEnter +=      │
│    ├─ InteractionManager.OnHoverExit +=       │
│    ├─ InteractionManager.OnInteract +=        │
│    ├─ InteractionManager.OnDragStart +=       │
│    ├─ InteractionManager.OnDragComplete +=    │
│    ├─ InteractionManager.OnDragCancel +=      │
│    ├─ InteractionManager.OnChoiceSelected +=  │
│    ├─ InteractionManager.OnChoiceHover +=     │
│    ├─ InteractionManager.OnRevealObject +=    │
│    ├─ InteractionManager.OnShowText +=        │
│    │                                          │
│    ├─ SceneManager.OnFragmentTransitionStarted│
│    └─ SceneManager.OnFragmentTransitioned     │
│                                              │
│  Event→Feedback Mapping Table                │
│  ├─ Priority                                  │
│  ├─ Visual: MicroAnimationManager             │
│  └─ Audio: AudioManager                       │
│                                              │
│  Debounce: 300ms per (objectId, eventName)    │
│  Suppression: _feedbackSuppressed flag        │
│  Priority Gate: _currentPriority              │
└──────────────────────────────────────────────┘
         │                    │
         ▼                    ▼
┌──────────────────┐  ┌──────────────┐
│ MicroAnimationMgr │  │ AudioManager │
│ PlayTriggered()  │  │ PlaySFX()    │
│ PlayFeedback()   │  │              │
└──────────────────┘  └──────────────┘
```

### Key Interfaces

```csharp
public class InteractionFeedback : MonoBehaviour
{
    // 纯事件驱动 — 无公开方法, 无 Update()
    // 所有工作在 OnEnable/OnDisable 事件订阅 + 回调中完成

    [System.Serializable]
    public struct FeedbackMapping
    {
        public string EventName;
        public string VisualAnimationId;   // MicroAnimationManager key
        public string AudioClipKey;        // AudioManager SFX key
        public int Priority;               // 0-10, higher = more important
        public bool HasHaptic;             // (future)
    }
}
```

### Implementation Guidelines

1. 映射表在 `FeedbackMappings` ScriptableObject 中可配置（非硬编码）
2. `OnDisable` 取消所有 12 个事件订阅（10 Interaction + 2 Scene）
3. 当 `_currentPriority == 10` 时（OnChoiceSelected 正在播放），屏蔽所有低优先级反馈
4. 反馈执行完成后 0.3s 重置 `_currentPriority`（给优先级窗口）
5. 无 `Update()` — 完全事件驱动，符合架构原则 #1

## Alternatives Considered

### Alternative 1: 各系统独立实现反馈

- **Description**: 每个消费者系统（HUD、Animation、Audio）各自订阅事件并自行决定反馈
- **Pros**: 灵活
- **Cons**: 反馈不一致（HUD 响应了但音效没播）；优先级不可控；防抖不可控
- **Rejection Reason**: 集中映射表保证一致性（同一事件 → 同一组反馈）

### Alternative 2: ScriptableObject 事件通道 (Unity 原生)

- **Description**: 使用 ScriptableObject Event Channel 模式（SO 作为事件通道）
- **Pros**: Inspector 可拖拽配置通道订阅
- **Cons**: 需要为每个事件创建一个 SO；项目文件膨胀；与已选的 static event 模式冲突
- **Rejection Reason**: 与 ADR-0001 的 static event 模式不兼容

## Consequences

### Positive

- 反馈一致性：同一事件始终产生相同的视觉+音频响应
- 防抖防止事件风暴导致反馈过载
- 转场抑制保证加载期间无干扰反馈
- 纯事件驱动，无 Update() 开销

### Negative

- 映射表需要维护（新增事件 → 更新映射）
- 优先级抢占可能导致低优先级事件静默丢失（特别是连续快速交互时）
- 10 个事件订阅在 OnEnable/OnDisable 中容易漏掉

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 快速交互导致低优先级反馈丢失 | Medium | Low | 用户几乎不会注意到 hover tick 丢失 |
| 订阅/取消订阅不同步导致泄漏 | Low | Medium | Code Review checklist 强制检查 |
| 转场中 _feedbackSuppressed 未重置 | Low | Medium | OnFragmentTransitioned 保证重置 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (单反馈执行) | ~0.2ms (查找映射 + 调用 PlaySFX/PlayFeedback) |
| CPU (防抖检查) | ~0.01ms (Dictionary lookup) |
| Memory (映射表) | ~1KB (10 entries) |
| GC Allocation | 0 (所有路径无分配) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] 10 个事件全部有对应的视觉+音频反馈
- [ ] OnChoiceSelected 播放期间，hover 音效被抑制
- [ ] 同一对象 300ms 内重复 hover 不重复播放音效
- [ ] 转场中所有反馈被抑制
- [ ] 转场结束后反馈恢复正常
- [ ] OnDisable 取消全部 12 个事件订阅

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `interaction-feedback.md` (#18) | 交互反馈 | 10 事件 → 视觉+音频映射 | 完整映射表 |
| `interaction-feedback.md` (#18) | 交互反馈 | 反馈优先级 | Priority 系统 (0-10) |
| `interaction-feedback.md` (#18) | 交互反馈 | 防抖 0.3s | (objectId, eventName) key + 时间窗口 |
| `interaction-feedback.md` (#18) | 交互反馈 | 转场抑制 | _feedbackSuppressed flag |
| `scroll-interaction-system.md` (#11) | 画卷交互 | 交互事件触发反馈 | 订阅 10 个 static events |
| `scene-management.md` (#6) | 场景管理 | 转场中抑制交互反馈 | OnFragmentTransitionStarted/Transitioned |

## Related

- ADR-0001 — 10 个 static event 声明自 InteractionManager
- ADR-0012 — MicroAnimationManager 视觉反馈
- ADR-0013 — AudioManager 音效反馈
- `docs/architecture/architecture.md` §3.2 — 事件订阅关系总表
