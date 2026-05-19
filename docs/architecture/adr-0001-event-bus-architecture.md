# ADR-0001: 事件总线架构 — `static event Action<T>` 松耦合通信

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

19 个 MVP 系统需要松耦合通信但必须保持单向依赖。决定使用 C# `static event Action<T>` 作为跨系统通信的唯一机制，替代中央 EventBus 单例（字符串路由，无类型安全）和 `UnityEvent<T>`（GC 分配）。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — C# `event` 是语言特性，非 Unity API |
| **References Consulted** | `VERSION.md`, `current-best-practices.md` (C# 9) |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | ADR-0002 (DataManager 加载完成通知), ADR-0004 (SceneManager 转场事件), ADR-0005 (InputSystem Action Map 切换通知), ADR-0006 (UIPanelStack 面板事件) |
| **Blocks** | Foundation + Core 层 Epic — 所有跨系统通信依赖此决策 |
| **Ordering Note** | 必须第一个 Accepted，其余 ADR 的接口定义依赖此模式 |

## Context

### Problem Statement

19 个 MVP 系统需要互相通信（输入→交互→状态变更→HUD 刷新→音效反馈），但必须保持松耦合——系统间不应持有对方引用。需要一个统一的通信模式，在编译时类型安全、零分配、支持 1:N 广播。

### Current State

新建项目，无遗留代码。架构文档 `architecture.md` 已将 static event 定为架构原则 #1，但未记录替代方案分析。

### Constraints

- 帧预算 16.6ms — 事件分发不能引入帧率影响
- 所有 19 个系统遵循单向依赖（Presentation → Feature → Core → Foundation）
- 不得使用轮询（违反架构原则 #1）
- 必须支持编辑器调试（哪个系统触发、哪些订阅者响应）

### Requirements

- 1:N 广播（一个事件、多个订阅者）
- 编译时类型安全（不允许字符串 key 路由）
- 零 GC 分配（事件触发不产生垃圾）
- 订阅/取消订阅在 `OnEnable`/`OnDisable` 中完成
- 支持自定义泛型参数（`Action<T>` 的 T 是值类型或引用类型）

## Decision

**选择 C# `static event Action<T>`** 作为跨系统通信的唯一机制。

### 规则

1. **声明**: 事件声明在生产者系统（触发事件的模块）中
   ```csharp
   // #11 InteractionManager 声明
   public static event Action<string> OnChoiceSelected;
   public static event Action<string> OnHoverEnter;
   ```

2. **订阅**: 消费者在 `MonoBehaviour.OnEnable()` 中订阅，`OnDisable()` 中取消
   ```csharp
   void OnEnable()
   {
       InteractionManager.OnChoiceSelected += HandleChoiceSelected;
   }
   void OnDisable()
   {
       InteractionManager.OnChoiceSelected -= HandleChoiceSelected;
   }
   ```

3. **触发**: 生产者直接调用
   ```csharp
   OnChoiceSelected?.Invoke(choiceId);
   ```

4. **参数约定**:
   - 简单数据用值类型或 string（无 GC）
   - 复杂数据用不可变 struct 或 record（C# 9）
   - 禁止传递 `MonoBehaviour` 引用或可变对象

5. **订阅图**: 订阅关系在 `architecture.md` §3.2 记录。新增订阅必须更新该文档。

### Architecture Diagram

```
┌──────────────────────────────────────────────────────────┐
│  static event 声明方 (生产者)                              │
│                                                          │
│  #11 InteractionManager                                  │
│  ├─ OnChoiceSelected(string choiceId)                    │
│  ├─ OnHoverEnter(string objectId)                        │
│  ├─ OnHoverExit(string objectId)                         │
│  ├─ OnInteract(string objectId)                          │
│  ├─ OnDragStart / OnDragComplete / OnDragCancel          │
│  ├─ OnRevealObject / OnShowText                          │
│  └─ OnChoiceHover(string choiceId)                       │
│                                                          │
│  #6 SceneManager                                         │
│  ├─ OnFragmentTransitionStarted(chapterKey, fragmentId)  │
│  └─ OnFragmentTransitioned(chapterKey, fragmentId)       │
│                                                          │
│  #12 ChangeTracker                                       │
│  └─ OnOverlayChanged(targetFragmentId)                   │
│                                                          │
│  #15 ChapterManager                                      │
│  ├─ OnChapterStarted / OnChapterCompleted                │
│  ├─ OnFragmentChanged / OnAllChaptersCompleted           │
│  └─ OnChapterReplayStarted                              │
│                                                          │
│  #4 LocalizationManager                                  │
│  └─ OnLocaleChanged                                      │
│                                                          │
│  #3 AudioManager                                         │
│  └─ OnAudioError                                         │
│                                                          │
│  #1 InputManager                                         │
│  └─ OnGamepadConnectionChanged                           │
└──────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐
│ #18 IFeedback│  │  #17 HUD    │  │ #12 ChangeTracker   │
│ (10 events)  │  │ (4 events)  │  │ (1 event)           │
└─────────────┘  └─────────────┘  └─────────────────────┘
```

### Key Interfaces

```csharp
// 事件参数约定 — 所有事件必须遵循
// - 值类型优先 (避免 GC)
// - 不可变 (防止消费者意外修改)
// - 自描述 (参数名即文档)

// 示例：正确的声明
public static event Action<string> OnChoiceSelected;              // 简单数据
public static event Action<string, string> OnFragmentTransitioned; // 两个简单参数

// 示例：复杂数据用 record (C# 9)
public record AssociationCandidate(string FragmentId, float Score, string Grade);
public static event Action<AssociationCandidate[]> OnPathsUpdated;

// 禁止的模式：
// ✗ public static event Action<MonoBehaviour> ...  (可变对象)
// ✗ public static event Action<object> ...          (装箱)
// ✗ public static event Action ...                  (无参数无类型)
```

### Implementation Guidelines

1. 每个事件的声明类在文件头部注释："Events declared here: [event names]"
2. 订阅必须在 `OnEnable`/`OnDisable` 成对出现 — Code Review 检查
3. 事件触发前检查 `?.Invoke` (处理无订阅者情况)
4. 不创建 `EventArgs` 子类 — 使用值类型参数
5. 新增事件后更新 `architecture.md` §3.2 事件订阅总表
6. **禁止 lambda 订阅热路径事件**: `+= (id) => Handle(id, someLocal)` 在订阅时产生 GC 分配（闭包捕获）。热路径事件（HUD 更新、交互反馈）必须使用方法组订阅：`+= HandleChoiceSelected;`
7. **生产者 OnDestroy 清理**: 声明 static event 的 MonoBehaviour 必须在 `OnDestroy()` 中将事件置 null，防止场景卸载后委托链残留：`OnChoiceSelected = null;`
8. **测试 TearDown 重置**: 每个测试的 `[TearDown]` 必须将所有订阅过的 static event 置 null，防止测试间泄漏

## Alternatives Considered

### Alternative 1: 中央 EventBus 单例 (字符串 Key 路由)

```csharp
EventBus.Emit("choice_selected", choiceId);
EventBus.On("choice_selected", handler);
```

- **Description**: 全局单例 EventBus，事件通过字符串 key 识别和路由
- **Pros**: 全局可达，无需知道生产者类型；运行时灵活（字符串 key 可拼接）
- **Cons**: 无编译时类型检查（key 拼写错误静默失败）；每次 Emit 装箱产生 GC；字符串常量化后失去灵活性；难以追踪订阅图（IDE "Find References" 失效）
- **Estimated Effort**: 与选择方案相同
- **Rejection Reason**: 违背编译时类型安全要求和零 GC 要求。字符串 key 路由在 10+ 事件系统中产生调试噩梦

### Alternative 2: `UnityEvent<T>` (Inspector 可配置)

```csharp
public UnityEvent<string> OnChoiceSelected;
```

- **Description**: 使用 Unity 内置 `UnityEvent<T>`，可在 Inspector 中拖拽配置订阅
- **Pros**: 可在 Unity Inspector 中配置订阅关系，策划友好
- **Cons**: `UnityEvent.Invoke()` 每次产生 ~40B GC 分配（Unity 已知问题）；不支持泛型参数超过 4 个；10 个事件 × 每帧可能触发 = 每帧数百字节 GC；Inspector 配置在 4+ 订阅者时不可维护
- **Estimated Effort**: 更少（Inspector 拖拽代替代码订阅）
- **Rejection Reason**: GC 分配不可接受。Inspector 配置不适合 19 系统规模的订阅图

## Consequences

### Positive

- 零 GC 分配（`?.Invoke()` 是直接委托调用）
- 编译时类型安全（IDE 重构、Find References 可用）
- 订阅图集中可见（`architecture.md` §3.2 表）
- 无单例依赖（纯 static 成员，不依赖 GameObject）

### Negative

- `static event` 是 GC root — 订阅者不取消订阅会导致内存泄漏（缓解: `OnDisable` 强制取消）
- 无法在 Inspector 看到运行时订阅状态（需自定义调试工具）
- 事件声明分散在各生产者类中（缓解: `architecture.md` 事件订阅总表）
- **静态事件跨场景存活**: 静态成员是 GC root，不会随场景卸载而清除。若生产者所在场景卸载后，其 static event 委托链仍存活——新场景的订阅者可能收到来自已卸载系统的事件。生产者必须在 `OnDestroy` 中置空其 static event：`OnChoiceSelected = null;`
- **测试泄漏风险**: 静态事件在测试运行间持续存在。若 `[TearDown]` 不清空静态状态，Test A 的订阅会泄漏到 Test B。测试作者必须使用 method group 订阅（`+= HandleX` 而非 lambda），并在 `[TearDown]` 中重置所有 static event 为 null

### Neutral

- 从传统 Unity 单例模式转变为 static event 模式 — 团队需适应

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 忘记取消订阅导致泄漏 | Medium | High | Coding Standards 强制 OnEnable/OnDisable 成对检查；Code Review checklist |
| 转场中事件触发导致不一致 | Low | High | `_feedbackSuppressed` flag + Inactive Action Map 双重保护 |
| 事件触发顺序不确定 | Low | Medium | 架构原则 #5 单向依赖保证无循环触发 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (per event invoke) | ~0.001ms (直接委托调用) |
| Memory (per event) | 0 bytes (static field, 无分配) |
| GC Allocation | 0 (使用值类型参数或 string) |
| Load Time | 0 (无额外初始化) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] 所有 15 个事件（见事件订阅总表）编译通过，类型安全
- [ ] 每个事件的订阅者在 `OnDisable` 中取消订阅（Code Review checklist）
- [ ] 帧分析确认事件触发不产生 GC Alloc
- [ ] 转场中无事件导致的不一致状态

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `scroll-interaction-system.md` (#11) | 画卷交互 | 10 个公开事件供其他系统订阅 | static event Action<T> 声明在 InteractionManager |
| `interaction-feedback.md` (#18) | 交互反馈 | 订阅交互事件并映射到视觉+音频响应 | OnEnable 订阅 10 事件，OnDisable 取消 |
| `scene-management.md` (#6) | 场景管理 | OnFragmentTransitionStarted/Transitioned 事件 | static event 声明在 SceneManager |
| `memory-change-tracking.md` (#12) | 变化追踪 | OnOverlayChanged 通知 HUD 刷新 | static event 声明在 ChangeTracker |
| `chapter-management.md` (#15) | 章节管理 | 章节开始/完成/碎片切换事件 | static event 声明在 ChapterManager |
| `in-game-hud.md` (#17) | 游戏内 HUD | 接收状态变更并刷新显示 | 订阅 ChangeTracker + ChapterManager 事件 |
| `input-system.md` (#1) | 输入系统 | 设备热插拔通知 | OnGamepadConnectionChanged event |
| `localization-system.md` (#4) | 本地化 | 语言切换通知所有 UI | OnLocaleChanged event |

## Related

- ADR-0002 (DataManager) — 使用此模式通知加载完成
- ADR-0004 (SceneManager) — 使用此模式声明转场事件
- `docs/architecture/architecture.md` §3.2 — 事件订阅关系总表
