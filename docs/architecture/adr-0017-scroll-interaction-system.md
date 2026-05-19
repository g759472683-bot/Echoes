# ADR-0017: 记忆画卷交互系统 — 集中式碰撞检测与 10 事件分发

## Status

Accepted

## Date

2026-05-19

## Last Verified

2026-05-19

## Decision Makers

User + Claude Code (technical-director via /dev-story)

## Summary

记忆画卷交互系统是玩家与记忆世界之间的"手"——将鼠标输入转化为对碎片中物件的触碰、拖拽和悬停检测。决定使用集中式 InteractionManager（每帧单次 Physics2D.OverlapPoint）+ 10 个 public static event + 4 种交互类型（Touch/Drag/Hover/Examine）+ 5 状态互斥机。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Input / Physics 2D |
| **Knowledge Risk** | MEDIUM — Physics2D.OverlapPoint 在 Unity 6 中行为需验证；Input System 包为 post-cutoff |
| **References Consulted** | `VERSION.md` |
| **Post-Cutoff APIs Used** | InputSystem.Get<Vector2>("Point"), InputSystem.GetButtonDown("Click") — Unity 6 Input System 1.8+ |
| **Verification Required** | OverlapPoint 在 UI Toolkit 覆盖下的拾取行为；Input System Gameplay/UI Action Map 切换延迟 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (10 static event 声明模式), ADR-0005 (Input System — Point/Click 输入), ADR-0004 (OnFragmentTransitioned 事件接收), ADR-0007 (ApplyChanges 调用) |
| **Enables** | ADR-0014 (InteractionFeedback — 10 事件订阅), ADR-0012 (MicroAnimation — 交互触发动画) |
| **Blocks** | ScrollInteraction Epic |
| **Ordering Note** | 依赖 ADR-0005（输入就绪）和 ADR-0004（场景过渡事件）；ADR-0014 的反馈映射表依赖本系统的 10 事件声明 |

## Context

### Problem Statement

回响的记忆画卷交互模式不是传统的"点击 UI 按钮"——玩家在画面中触碰物件、拖开表层、悬停犹豫。需要一个集中式的交互检测引擎来处理 Physics2D 碰撞检测、区分点击/拖拽/悬停、管理交互状态互斥、并将交互事件广播给下游系统。当前多个 ADR（0001, 0004, 0005, 0014）以碎片化方式覆盖了交互系统的一部分，但没有独立的架构决策来统一定义 InteractionManager 的完整 API 和行为契约。

### Constraints

- 每帧单次 Physics2D.OverlapPoint（性能约束——不在 Update 中多次查询）
- 不使用 Unity EventSystem / Raycaster——纯 2D 物理检测路径
- 交互检测仅通过 Collider2D（BoxCollider2D / CircleCollider2D）
- 拖拽阈值：5px 触发，30px 完成
- 悬停提示延迟：0.5s
- 点击防抖：0.3s
- 4 种交互类型 + 5 种交互状态互斥

### Requirements

- 集中式 InteractionManager MonoBehaviour（Game 场景持久）
- 10 个 public static event（OnHoverEnter, OnHoverExit, OnInteract, OnDragStart, OnDragComplete, OnDragCancel, OnChoiceSelected, OnChoiceHover, OnRevealObject, OnShowText）
- 4 种交互类型处理（Touch, Drag, Hover, Examine）
- 交互结果分发（PlayAnimation, ShowText, PresentChoice, TransitionToFragment, RevealObject）
- Action Map 自动切换（Gameplay ↔ UI）
- 过渡期间交互保护（Action Map = Inactive 时全屏蔽）

## Decision

**集中式 InteractionManager + Physics2D.OverlapPoint 单点检测 + 10 static event 广播 + 5 状态互斥机。**

### 交互检测主循环

```
InteractionManager.Update():
  1. 检查 Action Map == Gameplay → 否则跳过
  2. Vector2 mousePos = InputSystem.Get<Vector2>("Point")
  3. Collider2D hit = Physics2D.OverlapPoint(mousePos, layerMask: Interactable)
  4. 对比上一帧 hit 对象:
     ├── 不同对象 → OnHoverExit(old) + OnHoverEnter(new)
     └── 相同对象 → OnHoverStay(current)
  5. 处理点击 (InputSystem.GetButtonDown("Click")):
     └── 若悬停物件存在 → OnInteract(current)
  6. 处理拖拽 (InputSystem.GetButton("Click") + mouseDelta > 5px):
     └── 若 InteractionType = Drag → OnDrag(current, delta)
```

### 10 公共事件 API

```csharp
// InteractionManager 公共静态事件 — 交互反馈系统 (#18) 订阅
public static event Action<string> OnHoverEnter;       // 参数: objectId
public static event Action<string> OnHoverExit;        // 参数: objectId
public static event Action<InteractiveObject> OnInteract;
public static event Action<InteractiveObject> OnDragStart;
public static event Action<InteractiveObject> OnDragComplete;
public static event Action<InteractiveObject> OnDragCancel;
public static event Action<string> OnChoiceSelected;    // 参数: choiceId
public static event Action<string> OnChoiceHover;       // 参数: choiceId
public static event Action<GameObject> OnRevealObject;
public static event Action<TextContent> OnShowText;
```

> **签名约定**：OnHoverEnter/OnHoverExit 传递 objectId (string) 而非 InteractiveObject——悬停是最频繁的事件，传轻量标识符避免订阅方持有碎片间过期的 InteractiveObject 引用。

### 4 种交互类型

| InteractionType | 触发方式 | 反馈 | 典型结果 |
|-----------------|---------|------|---------|
| Touch | 鼠标左键点击 | L2→L3 发光 | PlayAnimation / ShowText / PresentChoice / RevealObject |
| Drag | 按住+移动 ≥5px | 物件跟随鼠标 | 拖拽≥30px 完成触发 OnInteract，否则弹回 |
| Hover | 悬停 ≥0.5s | L1→L2 脉动 | ShowText（悬停提示） |
| Examine | 点击进入放大查看 | 物件放大中央 | Cancel/Escape 退出 |

### 5 状态互斥

| 状态 | 允许的交互 | 阻断 |
|------|-----------|------|
| Active | 悬停/点击/拖拽 | — |
| Dragging | 仅当前拖拽 | 点击、悬停其他物件、碎片切换 |
| ChoicePresenting | 仅选项按钮 | 所有画面交互、碎片切换 |
| Examining | 仅 Cancel 退出 | 所有其他交互 |
| Blocked | 无 | 全部交互（过渡/文本展示中） |

### 交互结果分发

```
OnInteract(InteractiveObject obj):
  ResultType = PlayAnimation       → MicroAnimationManager.PlayTriggered(id)
  ResultType = ShowText            → HUD.ShowFragmentText(text)
  ResultType = PresentChoice       → InputSystem.SwitchToUIMode()
                                   → HUD.ShowChoicePanel(group)
                                   → 玩家选择后:
                                     → ChangeTracker.ApplyChanges(fragId, choiceId, changes)
                                     → InputSystem.SwitchToGameplayMode()
  ResultType = TransitionToFragment → SceneManager.TransitionToFragmentAsync(chapter, target)
  ResultType = RevealObject        → 显示隐藏物件 + PlayTriggered("object_appear")
```

### Architecture Diagram

```
┌──────────────────────────────────────────────┐
│          InteractionManager                   │
│          (MonoBehaviour, Game 场景持久)        │
│                                              │
│  Update() — 每帧                             │
│  ├─ Physics2D.OverlapPoint(mousePos)         │
│  ├─ 悬停状态对比 (enter/exit/stay)            │
│  ├─ 点击检测 → OnInteract                    │
│  └─ 拖拽检测 → OnDrag                        │
│                                              │
│  10 public static events:                    │
│  OnHoverEnter/Exit, OnInteract,              │
│  OnDragStart/Complete/Cancel,                │
│  OnChoiceSelected/Hover,                     │
│  OnRevealObject, OnShowText                  │
│                                              │
│  状态机: Active|Dragging|ChoicePresenting    │
│          |Examining|Blocked                   │
│                                              │
│  订阅: GameSceneManager.OnFragmentTransitioned│
└──────────────────────────────────────────────┘
         │          │          │
         ▼          ▼          ▼
┌──────────┐ ┌──────────┐ ┌──────────────┐
│ #18      │ │ #9       │ │ #12          │
│ Feedback │ │ Animation│ │ ChangeTracker│
│ 10事件   │ │ Trigger  │ │ ApplyChanges │
└──────────┘ └──────────┘ └──────────────┘
```

### Implementation Guidelines

1. ADR-0001 事件模式：OnEnable 订阅 GameSceneManager.OnFragmentTransitioned，OnDisable 取消订阅
2. 每帧仅一次 OverlapPoint——不遍历多个 layer
3. 碰撞体重建在 OnFragmentTransitioned 回调中执行——不在 Update 中创建/销毁 Collider2D
4. 拖拽使用世界空间坐标——物件跟随鼠标需考虑 Camera.ScreenToWorldPoint
5. PresentChoice 自动应用：若 MaxSelections=1 且仅 1 个可用选项 → 跳过选择面板，直接触发 ContentChanges
6. 选择面板智能定位：优先物件右侧，空间不足时下方（由 HUD #17 执行定位，本系统只提供物件屏幕位置）
7. Choice.ContentChanges 是 ContentChange[]（ADR-0007 叠加层路径），Choice.OnSelect 是 InteractionResult（向后兼容路径）——两者独立，同时存在时优先 ContentChanges

## Alternatives Considered

### Alternative 1: 使用 Unity EventSystem + Raycaster

- **Description**: 使用 Unity 内置 UI EventSystem + Physics2DRaycaster 处理交互检测
- **Pros**: Unity 原生支持，自动处理事件冒泡
- **Cons**: 依赖 GameObject 层级和 Raycaster 配置；对非 UI 2D 物件的事件路由不直观；额外 GC 分配
- **Rejection Reason**: 回响的交互物件是画面中的 2D 碰撞体而非 UI 元素——纯物理检测路径更直接且性能可预测

### Alternative 2: 每个 InteractiveObject 挂独立 MonoBehaviour 自行检测

- **Description**: 每个物件的 GameObject 上挂脚本，在 Update 中各自检测鼠标
- **Pros**: 分散职责
- **Cons**: N 个物件 = N 次 OverlapPoint 查询；状态互斥难以协调（拖拽中需要屏蔽其他物件）；事件顺序不确定
- **Rejection Reason**: 集中式管理器保证单次物理查询 + 确定性事件顺序 + 全局状态互斥

### Alternative 3: 使用 C# event (非 static)

- **Description**: 每个 InteractionManager 实例使用 instance event，订阅方通过引用获取
- **Pros**: 更符合 OOP 封装
- **Cons**: 需要传递 InteractionManager 引用给所有订阅方；与 ADR-0001 的 static event 全局事件总线模式不一致
- **Rejection Reason**: ADR-0001 已确立 static event 模式——本系统遵循项目全局架构规范

## Consequences

### Positive

- 单次物理查询（每帧 1 次 OverlapPoint）——性能可预测
- 10 事件完整覆盖所有交互行为——下游系统无需猜测交互何时发生
- 状态互斥机防止交互冲突（如拖拽中误触点击）
- 集中式管理器使交互逻辑可单元测试（注入 IMousePositionProvider 等抽象）

### Negative

- 10 个 static event 需在 OnEnable/OnDisable 中正确订阅/取消——漏掉会导致内存泄漏或静默失效
- Action Map 切换（Gameplay ↔ UI）依赖 Input System 包的切换延迟——需验证
- Examine 模式的视觉设计尚未确定（物件放大还是展示细节插图？）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| OverlapPoint 在 UI Toolkit 覆盖下拾取异常 | Low | Medium | 验证 UI Document picking-mode 与 2D 物理层的交互 |
| 快速连续点击导致竞态 | Low | Low | 0.3s 防抖 + _lastInteractionTime 检查 |
| 碰撞体重建在过渡中有帧延迟 | Low | Low | OnFragmentTransitioned 在遮罩覆盖期间触发——重建在玩家可见前完成 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (Update, 无交互) | ~0.05ms (OverlapPoint + null check) |
| CPU (Update, 悬停检测中) | ~0.1ms |
| CPU (碰撞体重建, 10 物件) | ~0.5ms (一次性，在过渡中) |
| Memory (InteractionManager) | ~2KB |
| GC Allocation (Update) | 0 (所有路径零分配) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] 光标悬停在物件上时 OnHoverEnter 触发（objectId 参数正确）
- [ ] 光标离开物件时 OnHoverExit 触发
- [ ] 点击 Touch 物件 → OnInteract 触发 → InteractionResult 正确分发
- [ ] 拖拽 ≥30px 完成 → OnDragComplete 触发；拖拽 <30px 释放 → 物件弹回 + OnDragCancel
- [ ] PresentChoice 时 Action Map 切换到 UI，选择完成后切回 Gameplay
- [ ] 过渡期间（Action Map = Inactive）所有交互被忽略
- [ ] MaxSelections=1 且仅 1 个可用选项时跳过选择面板直接应用 ContentChanges
- [ ] 状态互斥：拖拽中不能点击其他物件
- [ ] 10 个 static event 在 OnDisable 中全部取消订阅

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `scroll-interaction-system.md` (#11) | 画卷交互 | 集中式 InteractionManager | 单例 MonoBehaviour + 每帧单次 OverlapPoint |
| `scroll-interaction-system.md` (#11) | 画卷交互 | 10 公共事件 API | 10 static event 声明 |
| `scroll-interaction-system.md` (#11) | 画卷交互 | 4 种交互类型 | Touch/Drag/Hover/Examine 处理分支 |
| `scroll-interaction-system.md` (#11) | 画卷交互 | 交互结果分发 | 5 种 ResultType → 对应系统调用 |
| `scroll-interaction-system.md` (#11) | 画卷交互 | 状态互斥 | 5 状态机 (Active/Dragging/ChoicePresenting/Examining/Blocked) |
| `scroll-interaction-system.md` (#11) | 画卷交互 | 过渡期间交互保护 | Action Map Inactive 检查 |
| `interaction-feedback.md` (#18) | 交互反馈 | 10 事件订阅 | 本系统声明事件，ADR-0014 定义反馈映射 |

## Related

- ADR-0001 — static event 声明模式（本系统 10 个事件遵循此模式）
- ADR-0005 — Input System 提供 Point/Click 输入
- ADR-0004 — OnFragmentTransitioned 事件接收
- ADR-0007 — ApplyChanges 调用（选择后触发内容变化）
- ADR-0014 — InteractionFeedback 订阅本系统的 10 事件
- ADR-0012 — MicroAnimation 响应交互触发动画
- `design/gdd/scroll-interaction-system.md` — 完整 GDD
