# Story 003: 拖拽交互系统

> **Epic**: 记忆画卷交互系统 (InteractionManager)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/scroll-interaction-system.md`
**Requirement**: `TR-scroll-interaction-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0005: 输入系统架构
**ADR Decision Summary**: 使用 New Input System 的 `InputAction.ReadValue<Vector2>()` 跟踪鼠标移动；拖拽阈值和弹回动画为纯 C# 逻辑

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: New Input System API 整个为 post-cutoff。`InputAction.IsPressed()` 和 `ReadValue<Vector2>()` 在 Unity 6 中稳定。拖拽轨迹渲染使用 LineRenderer 或即时 Sprite——不依赖特定 Unity 版本 API。

**Control Manifest Rules (Feature Layer)**:
- Required: 单次 Physics2D.OverlapPoint per frame — source: ADR-0005
- Forbidden: 切勿使用 Legacy Input Manager (`Input.GetMouseButton`, `Input.GetAxis`) — source: ADR-0005
- Guardrail: 输入轮询：~0.1ms per frame — source: ADR-0005

---

## Acceptance Criteria

*From GDD `design/gdd/scroll-interaction-system.md`, scoped to this story:*

- [ ] GIVEN 物件 InteractionType = Drag，WHEN 玩家按住鼠标左键并拖动超过 5px，THEN 物件跟随鼠标移动（transform.position 跟随 worldPos 位移）。拖拽轨迹（淡墨色拖痕）显示在物件原始位置到当前位置之间。OnDragStart 静态事件触发。

- [ ] GIVEN 拖拽进行中，WHEN 玩家拖动物件超过 30px 后释放鼠标，THEN 拖拽完成——触发 OnInteract（与 Touch 的 ResultType 分发相同）。物件停留在最终位置。拖痕在 1.0s 内淡出。OnDragComplete 事件触发。

- [ ] GIVEN 拖拽进行中，WHEN 玩家在未达到 30px 阈值前释放鼠标，THEN 物件弹回原位（spring-back 动画，0.3s EaseOutCubic）。OnDragCancel 事件触发。不触发 OnInteract。

- [ ] GIVEN 拖拽进行中，WHEN 光标悬停在其他物件上，THEN 其他物件的悬停检测被阻断——_currentState = Dragging，Update() 中的状态守卫阻止悬停/点击处理。

---

## Implementation Notes

*Derived from GDD 规则 4 — Drag Interaction 特殊处理:*

拖拽检测与执行:
```csharp
private InteractiveObject _dragTarget;
private Vector2 _dragStartMousePos;
private Vector2 _dragStartObjectPos;
private float _dragTotalDistance;
private bool _isDragging;

private const float DRAG_TRIGGER_THRESHOLD = 5f;   // px
private const float DRAG_COMPLETE_THRESHOLD = 30f;  // px
private const float SPRING_BACK_DURATION = 0.3f;    // seconds

void Update()
{
    // ... (Story 001 中的 Action Map 和状态守卫在这里) ...

    if (_isDragging)
    {
        HandleDragUpdate();
        return; // 拖拽期间跳过所有其他处理
    }

    // 在 OverlapPoint 之后检查拖拽启动:
    if (_lastHovered != null && _controls.Gameplay.Click.WasPressedThisFrame())
    {
        var obj = GetInteractiveObject(_lastHovered);
        if (obj?.InteractionType == InteractionType.Drag)
        {
            StartDrag(obj);
        }
    }
}
```

启动拖拽:
```csharp
private void StartDrag(InteractiveObject obj)
{
    _dragTarget = obj;
    _dragStartMousePos = _controls.Gameplay.Point.ReadValue<Vector2>();
    _dragStartObjectPos = obj.Transform.position;
    _dragTotalDistance = 0f;
    _isDragging = false; // 尚未确认——等待 5px 阈值

    _currentState = InteractionState.Dragging;
    OnDragStart?.Invoke(obj);
}
```

拖拽更新:
```csharp
private void HandleDragUpdate()
{
    Vector2 currentMousePos = _controls.Gameplay.Point.ReadValue<Vector2>();
    Vector2 worldMousePos = Camera.main.ScreenToWorldPoint(currentMousePos);
    Vector2 delta = worldMousePos - Camera.main.ScreenToWorldPoint(_dragStartMousePos);

    if (!_isDragging)
    {
        // 检查 5px 触发阈值
        if (delta.magnitude >= DRAG_TRIGGER_THRESHOLD)
        {
            _isDragging = true;
        }
        else
        {
            return; // 小于 5px——仍可能是点击，不移动物件
        }
    }

    // 物件跟随鼠标
    _dragTarget.Transform.position = _dragStartObjectPos + delta;
    _dragTotalDistance = delta.magnitude;

    // 渲染拖拽轨迹 (淡墨色——原位置到当前位置的线条)
    RenderDragTrail(_dragStartObjectPos, _dragTarget.Transform.position);

    // 检查释放
    if (_controls.Gameplay.Click.WasReleasedThisFrame())
    {
        CompleteDrag();
    }
}
```

拖拽完成 + 弹回:
```csharp
private async void CompleteDrag()
{
    _isDragging = false;

    if (_dragTotalDistance >= DRAG_COMPLETE_THRESHOLD)
    {
        // 拖拽完成——触发 OnInteract
        OnDragComplete?.Invoke(_dragTarget);
        FadeDragTrail(1.0f);
        await DispatchInteractionResult(_dragTarget.OnInteract); // 复用 Story 002
    }
    else
    {
        // 弹回——0.3s EaseOutCubic
        OnDragCancel?.Invoke(_dragTarget);
        FadeDragTrail(0.3f);
        await SpringBack(_dragTarget.Transform,
            _dragTarget.Transform.position,
            _dragStartObjectPos,
            SPRING_BACK_DURATION);
    }

    _dragTarget = null;
    _currentState = InteractionState.Active;
}

// EaseOutCubic: f(t) = 1 - (1 - t)^3
private async Task SpringBack(Transform target, Vector2 from, Vector2 to, float duration)
{
    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float ease = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
        target.position = Vector2.Lerp(from, to, ease);
        await Task.Yield();
    }
    target.position = to; // 精确最终位置
}
```

拖拽轨迹:
```csharp
// 简易拖痕 — 可根据设计需要替换为更复杂的实现
private LineRenderer _trailRenderer;

private void RenderDragTrail(Vector2 from, Vector2 to)
{
    if (_trailRenderer == null)
    {
        var go = new GameObject("DragTrail");
        _trailRenderer = go.AddComponent<LineRenderer>();
        _trailRenderer.startColor = new Color(0.3f, 0.2f, 0.2f, 0.5f); // 淡墨色
        _trailRenderer.endColor = new Color(0.3f, 0.2f, 0.2f, 0.1f);
        _trailRenderer.startWidth = 0.03f;
        _trailRenderer.endWidth = 0.01f;
        _trailRenderer.positionCount = 2;
    }
    _trailRenderer.SetPosition(0, from);
    _trailRenderer.SetPosition(1, to);
}

private async void FadeDragTrail(float duration)
{
    float elapsed = 0f;
    Color startColor = _trailRenderer.startColor;
    Color endColor = _trailRenderer.endColor;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        _trailRenderer.startColor = Color.Lerp(startColor, Color.clear, t);
        _trailRenderer.endColor = Color.Lerp(endColor, Color.clear, t);
        await Task.Yield();
    }
    if (_trailRenderer != null)
        Destroy(_trailRenderer.gameObject);
}
```

拖拽期间互斥 (GDD 规则 5):
- `_currentState = Dragging` → Update() 中所有悬停/点击/Examine 处理被跳过
- 同一时间只能有 1 个拖拽进行中（`_dragTarget` 阻止多拖拽）
- 场景过渡期间（Action Map Inactive）——拖拽由 Story 001 的 Action Map 守卫阻止
- 在拖拽完成/取消前碎片切换被阻止（由 SceneManager 检查 InteractionManager._currentState）

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 状态机定义、Update 循环结构、碰撞体管理
- Story 002: InteractionResult 分发（`DispatchInteractionResult` 复用——拖拽完成时调用）
- 微动画 (#9): L2/L3 视觉反馈和触发动效——本 Story 只负责拖拽逻辑和拖痕
- 音频 (#3): 拖拽音效——由交互反馈系统 (#18) 通过订阅 OnDragStart/OnDragComplete/OnDragCancel 管理

---

## QA Test Cases

- **AC-1**: 拖拽触发 — 5px 阈值
  - Given: 物件 InteractionType=Drag，玩家按下鼠标左键
  - When: 鼠标移动 3px（<5px 阈值）
  - Then: _isDragging 为 false；物件不移动；不渲染拖痕
  - Edge cases: 玩家移动 3px → 释放 → 无拖拽，无点击。物件保持原位。InteractionType=Drag 的物件不响应点击——只响应拖拽。

- **AC-2**: 拖拽激活 — 超过 5px
  - Given: 玩家按下鼠标左键于 Drag 物件上
  - When: 鼠标移动 6px（≥5px 阈值）
  - Then: _isDragging 设为 true；物件开始跟随鼠标 delta 移动；拖痕渲染在原始位置和当前位置之间；OnDragStart 事件触发
  - Edge cases: 物件移动到画面边缘外 → 继续跟随鼠标（不夹紧），物件在屏幕外不可见但位置继续更新

- **AC-3**: 拖拽完成 — 超过 30px
  - Given: 拖拽进行中，_dragTotalDistance ≥ 30px
  - When: 玩家释放鼠标左键
  - Then: OnDragComplete 事件触发；OnInteract 以该物件为参数触发；DispatchInteractionResult 以物件 OnInteract 为参数调用；拖痕在 1.0s 内淡出
  - Edge cases: DispatchInteractionResult=PresentChoice → _currentState 转换为 ChoicePresenting（Story 004）

- **AC-4**: 拖拽取消 — 弹回
  - Given: 拖拽进行中，_dragTotalDistance = 15px（<30px）
  - When: 玩家释放鼠标左键
  - Then: 物件弹回原始位置，0.3s EaseOutCubic；OnDragCancel 事件触发；不触发 OnInteract；拖痕在 0.3s 内淡出
  - Edge cases: 弹回动画期间调用 StartDrag → 被 _currentState=Dragging 守卫阻止

- **AC-5**: 拖拽互斥
  - Given: 拖拽进行中（_currentState = Dragging）
  - When: Update 循环运行，光标悬停在另一个物件上
  - Then: 悬停/离开检测被跳过；不触发 OnHoverEnter/OnHoverExit；不启动新的拖拽
  - Edge cases: 在 Dragging 状态中 Action Map 切换为 UI（不应发生——拖拽期间不允许状态变化）→ 在 Update() 顶部被 Action Map 守卫捕获

- **AC-6**: 拖拽轨迹淡出
  - Given: 拖拽完成，拖痕可见
  - When: 1.0s 过去
  - Then: 拖痕完全淡出（alpha=0）；LineRenderer GameObject 被销毁
  - Edge cases: 在淡出动画期间开始新的拖拽 → 旧拖痕被销毁，新拖痕创建
```

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/scroll-interaction/drag-system_test.cs` — must exist and pass

**Status**: [x] Created — `tests/unit/scroll-interaction/drag-system_test.cs` (16 tests)

---

## Dependencies

- Depends on: Story 001 (核心检测引擎 + 状态机), Story 002 (InteractionResult 分发 — DispatchInteractionResult 复用)
- Unlocks: None directly — 并行的 Logic Story

---

## Completion Notes
**Completed**: 2026-05-14
**Criteria**: 6/6 passing
**Deviations**:
- Drag 常量硬编码 (5f/30f/0.3f/1.0f/0.3f) — 后续应数据驱动
- SpringBack/FadeDragTrail 用 IEnumerator 协程替代 async Task/async void — 零 GC 优化
- OnDragStart 在 5px 阈值处触发（与 QA AC-2 一致，与 ADR 伪代码不同）
- URP Shader: `Sprites/Default` → `Universal Render Pipeline/2D/Sprite-Lit-Default`
**Test Evidence**: `tests/unit/scroll-interaction/drag-system_test.cs` — 16 tests covering all 6 ACs
**Code Review**: Complete (6 issues found → fixed)
