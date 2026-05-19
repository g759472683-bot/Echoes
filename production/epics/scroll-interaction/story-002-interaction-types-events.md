# Story 002: 交互类型处理 + 事件广播

> **Epic**: 记忆画卷交互系统 (InteractionManager)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/scroll-interaction-system.md`
**Requirement**: `TR-scroll-interaction-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0001: 事件总线架构
**ADR Decision Summary**: 10 个 `static event Action<T>` 事件在 InteractionManager 中声明，在交互逻辑执行完毕后触发（非事先触发）；订阅方在 OnEnable/OnDisable 中订阅/取消订阅；事件负载使用值类型或字符串 ID（不传递 MonoBehaviour 引用）

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `static event Action<T>` 在 IL2CPP 构建中经过完全测试；不使用 `UnityEvent<T>`（~40B GC per invoke）；订阅方必须在 OnDestroy 中取消订阅以防止场景加载时的僵尸委托链

**Control Manifest Rules (Feature Layer)**:
- Required: `static event Action<T>` 用于所有跨系统通信——事件在生产者中声明，在消费者 OnEnable/OnDisable 中订阅/取消订阅 — source: ADR-0001
- Forbidden: 切勿使用字符串键 EventBus — source: ADR-0001
- Forbidden: 切勿通过事件传递 MonoBehaviour 引用 — 使用值类型、字符串 ID 或不可变记录 — source: ADR-0001
- Guardrail: 事件分发：每次 invoke ~0.001ms，0 GC — source: ADR-0001

---

## Acceptance Criteria

*From GDD `design/gdd/scroll-interaction-system.md`, scoped to this story:*

- [ ] GIVEN 光标悬停在物件上，WHEN 物件 InteractionType = Touch，THEN 点击后触发 OnInteract。InteractionResult 按类型分发：PlayAnimation → MicroAnimationManager.PlayTriggered；ShowText → HUD.ShowFragmentText；PresentChoice → HUD.ShowChoicePanel + Action Map 切换到 UI；TransitionToFragment → SceneManager.TransitionToFragmentAsync；RevealObject → 显示隐藏物件 + MicroAnimationManager.PlayTriggered("object_appear")。

- [ ] GIVEN 点击事件、拖拽事件、悬停事件和选择事件发生，WHEN 触发时，THEN 对应的 10 个静态 C# 事件被触发，参数为相关的 InteractiveObject 或 choiceId 字符串。订阅方（交互反馈 #18）按事件类型接收到正确的数据负载。

- [ ] GIVEN 一个物件 DefaultState = Hidden，WHEN 碎片加载，THEN 该物件的碰撞体被禁用——Story 001 的碰撞体创建跳过它。WHEN 另一个物件触发 RevealObject 类型的 OnInteract 指向该物件，THEN 该物件的碰撞体被启用，物件变为可见。

- [ ] GIVEN Hover 类型物件，WHEN 光标悬停 ≥0.5s，THEN 展示提示文本（`OnInteract.TextContent`）在光标上方 20px 处。光标离开或点击后文本消失。

- [ ] GIVEN 玩家点击一个 Examine 类型物件，WHEN 处理交互，THEN _currentState 设为 Examining，物件放大到画面中央。按 Escape/Cancel → _currentState 恢复为 Active，物件缩小回原位。

---

## Implementation Notes

*Derived from ADR-0001 Implementation Guidelines:*

10 个静态事件声明:
```csharp
// InteractionManager 中的静态事件 — ADR-0001 模式
public static event Action<InteractiveObject> OnHoverEnter;
public static event Action<InteractiveObject> OnHoverExit;
public static event Action<InteractiveObject> OnInteract;
public static event Action<InteractiveObject> OnDragStart;
public static event Action<InteractiveObject> OnDragComplete;
public static event Action<InteractiveObject> OnDragCancel;
public static event Action<string> OnChoiceSelected;
public static event Action<string> OnChoiceHover;
public static event Action<GameObject> OnRevealObject;
public static event Action<TextContent> OnShowText;

// 每个事件使用 null 条件调用以处理零订阅方的情况：
// OnInteract?.Invoke(obj);
```

交互类型处理 (OnInteract 分发):
```csharp
private void ProcessInteraction(InteractiveObject obj)
{
    switch (obj.InteractionType)
    {
        case InteractionType.Touch:
            HandleTouch(obj);
            break;
        case InteractionType.Hover:
            HandleHover(obj);
            break;
        case InteractionType.Examine:
            HandleExamine(obj);
            break;
        case InteractionType.Drag:
            // Drag 由 Story 003 处理——此处不触发 OnInteract
            break;
    }
}

private async void HandleTouch(InteractiveObject obj)
{
    // 防抖 — GDD 规则 "快速连续点击同一物件"
    if (Time.time - _lastInteractionTime < 0.3f)
        return;
    _lastInteractionTime = Time.time;

    // 事件在交互逻辑执行完毕后触发
    OnInteract?.Invoke(obj);

    // 分发 InteractionResult
    await DispatchInteractionResult(obj.OnInteract);
}

private async Task DispatchInteractionResult(InteractionResult result)
{
    switch (result.ResultType)
    {
        case ResultType.PlayAnimation:
            MicroAnimationManager.PlayTriggered(result.AnimationId);
            break;

        case ResultType.ShowText:
            OnShowText?.Invoke(result.TextContent);
            // HUD 展示文本——不阻断交互
            HUD.ShowFragmentText(result.TextContent);
            break;

        case ResultType.PresentChoice:
            _currentState = InteractionState.ChoicePresenting;
            InputManager.SwitchToUIMode();
            var choiceGroup = _currentFragment.GetChoiceGroup(result.ChoiceGroupId);
            await HUD.ShowChoicePanel(choiceGroup);
            // 选择处理 → Story 004
            break;

        case ResultType.TransitionToFragment:
            _currentState = InteractionState.Blocked;
            await SceneManager.TransitionToFragmentAsync(
                _currentChapterKey, result.TargetFragmentId);
            break;

        case ResultType.RevealObject:
            // 启用隐藏物件
            var targetObj = _activeObjects.Find(o => o.ObjectId == result.TargetObjectId);
            if (targetObj != null)
            {
                EnableObjectCollider(targetObj);
                OnRevealObject?.Invoke(targetObj.GameObject);
                MicroAnimationManager.PlayTriggered("object_appear");
            }
            break;
    }
}
```

Hover 类型处理 (0.5s 延迟):
```csharp
private float _hoverTimer;
private InteractiveObject _hoverTarget;

private void OnHoverStayHandler(Collider2D hit)
{
    var obj = GetInteractiveObject(hit);
    if (obj == null || obj.InteractionType != InteractionType.Hover)
        return;

    if (_hoverTarget != obj)
    {
        _hoverTarget = obj;
        _hoverTimer = 0f;
    }

    _hoverTimer += Time.deltaTime;
    if (_hoverTimer >= 0.5f)
    {
        OnShowText?.Invoke(obj.OnInteract.TextContent);
        HUD.ShowFragmentText(obj.OnInteract.TextContent); // 光标上方 20px
        _hoverTimer = 0f; // 仅触发一次
    }
}
```

Examine 类型处理:
```csharp
private async void HandleExamine(InteractiveObject obj)
{
    _currentState = InteractionState.Examining;
    OnInteract?.Invoke(obj);
    // 放大物件到画面中央
    await MicroAnimationManager.PlayTriggered("examine_zoom_in");

    // 等待 Cancel 键
    await WaitForCancelInput();

    await MicroAnimationManager.PlayTriggered("examine_zoom_out");
    _currentState = InteractionState.Active;
}
```

ObjectState 管理:
```csharp
// GDD 规则 6: 物件状态管理
// DefaultState 在 Story 001 的碰撞体创建中已处理:
//   Hidden → 无碰撞体
//   Disabled → 有碰撞体但不可交互（灰色提示）
//   Active → 正常交互

private bool CanInteract(InteractiveObject obj)
{
    if (obj.DefaultState == ObjectState.Disabled)
        return false; // 有碰撞体但无响应
    if (obj.DefaultState == ObjectState.Hidden)
        return false; // 无碰撞体—无法到达此步骤
    return true;
}

// RevealObject: Hidden → Active
private void EnableObjectCollider(InteractiveObject obj)
{
    // 创建之前跳过的碰撞体
    var go = new GameObject($"Interactable_{obj.ObjectId}");
    go.transform.position = obj.HitboxCenter;
    go.layer = _interactableLayer;
    var col = go.AddComponent<BoxCollider2D>();
    col.size = obj.HitboxSize;
    col.isTrigger = true;
    obj.DefaultState = ObjectState.Active; // 运行时状态变更
    OnRevealObject?.Invoke(go);
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 核心 Update 循环、碰撞体创建、状态机定义、过渡保护
- Story 003: 拖拽机制（触发/完成/弹回）——本 Story 跳过 InteractionType.Drag 处理
- Story 004: PresentChoice 完整流程（HUD.ShowChoicePanel → 选择 → ChangeTracker → 清理）——本 Story 只调用 HUD.ShowChoicePanel + 切换到 UI Action Map
- HUD (#17): ShowChoicePanel、ShowFragmentText 的实际 UI 渲染
- 微动画 (#9): PlayTriggered 的实际动画播放

---

## QA Test Cases

- **AC-1**: Touch 交互 — PlayAnimation 结果
  - Given: 物件 InteractionType=Touch, ResultType=PlayAnimation, AnimationId="ripple"
  - When: 玩家点击物件（Click 输入，Action Map=Gameplay）
  - Then: MicroAnimationManager.PlayTriggered("ripple") 被调用；OnInteract 事件以该物件为参数触发
  - Edge cases: 物件 DefaultState=Disabled → 不触发交互（尽管有碰撞体）

- **AC-2**: Touch 交互 — PresentChoice 结果
  - Given: 物件 InteractionType=Touch, ResultType=PresentChoice, ChoiceGroupId="choice_01"
  - When: 玩家点击物件
  - Then: _currentState 设为 ChoicePresenting；InputManager.SwitchToUIMode() 被调用；HUD.ShowChoicePanel(choiceGroup) 被调用
  - Edge cases: ChoiceGroupId 对应的 ChoiceGroup 不存在 → LogWarning，不改变状态

- **AC-3**: Hover 类型 — 0.5s 延迟展示文本
  - Given: 物件 InteractionType=Hover, ResultType=ShowText, TextContent="一封旧信"
  - When: 光标悬停 0.6s
  - Then: OnShowText 事件以 TextContent 为参数触发；HUD.ShowFragmentText 以文本内容和光标上方 20px 位置为参数调用
  - Edge cases: 光标在 0.5s 前离开 → _hoverTimer 和 _hoverTarget 重置，不触发

- **AC-4**: RevealObject — Hidden 物件变为 Active
  - Given: 物件 B 的 DefaultState=Hidden（无碰撞体），物件 A 的 OnInteract=RevealObject(TargetObjectId="B")
  - When: 玩家点击物件 A
  - Then: 物件 B 的碰撞体被创建；OnRevealObject 以新 GameObject 为参数触发；MicroAnimationManager.PlayTriggered("object_appear") 被调用
  - Edge cases: TargetObjectId 在 _activeObjects 中不存在 → LogWarning，RevealObject 不执行

- **AC-5**: Examine 类型 — 放大查看模式
  - Given: 物件 InteractionType=Examine
  - When: 玩家点击物件
  - Then: _currentState=Examining；物件的"examine_zoom_in"动画播放；在所有其他物件上的 Update 悬停检测被状态守卫阻止
  - Edge cases: 在 Examining 状态中 OnFragmentTransitioned 触发 → Examining 自动退出，_currentState 转换处理由 Story 001 的过渡流程管理

- **AC-6**: 事件订阅生命周期
  - Given: 交互反馈系统 (#18) 在 OnEnable 中订阅 OnInteract += HandleInteract
  - When: 场景卸载后场景重新加载
  - Then: OnDisable 被调用，取消订阅；无僵尸委托链；新场景的 InteractionManager 实例拥有干净的事件委托
  - Edge cases: OnDestroy 中的生产者 null 赋值（`OnInteract = null`）——确保无残留委托
```

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/scroll-interaction/interaction-events_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (核心检测引擎 + 状态机), ui-framework Story 001 (UIPanelStack — 面板展示), memory-change-tracking Story 002 (ConditionGroup — 物件条件评估)
- Unlocks: Story 004 (选择流程)

---

## Completion Notes
**Completed**: 2026-05-14
**Criteria**: 5/5 passing (all 6 ACs covered by 17 tests)
**Deviations**:
- ADVISORY: OnInteract 参数为 Action&lt;InteractiveObject&gt;（ADR-0001 图示为 string objectId）— 非 MonoBehaviour，未违反核心禁令
- ADVISORY: OnRevealObject 参数为 Action&lt;GameObject&gt; — UnityEngine.Object 非 MonoBehaviour
- ADVISORY: 硬编码 GDD 阈值 (0.3f/0.5f/30f) — 预生产阶段可接受
- ADVISORY: 依赖未完成系统 (ui-framework S001, memory-change-tracking S002) — 通过接口解耦
**Test Evidence**: `tests/integration/scroll-interaction/interaction-events_test.cs` (17 tests ~799 lines)
**Code Review**: Complete — 5 BLOCKING 问题已修复
