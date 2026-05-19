# Story 001: 核心检测引擎 + 状态机

> **Epic**: 记忆画卷交互系统 (InteractionManager)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/scroll-interaction-system.md`
**Requirement**: `TR-scroll-interaction-001`, `TR-scroll-interaction-004`, `TR-scroll-interaction-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0005: 输入系统架构
**ADR Decision Summary**: InteractionManager MonoBehaviour 每帧单次 Physics2D.OverlapPoint (non-alloc) 检测 Interactable 图层上的鼠标悬停物件；Action Map 互斥门控（Gameplay active 时检测，UI/Inactive 时跳过）

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: New Input System 整个 API 表面均为 post-cutoff。Physics2D.OverlapPoint 使用 `ContactFilter2D` + `Collider2D[]` 缓冲 (non-alloc)。不要使用 `Physics2D.OverlapPointAll` (产生 GC 分配)。

**Control Manifest Rules (Feature Layer)**:
- Required: 单次 Physics2D.OverlapPoint per frame — 无 EventSystem/Raycaster — source: ADR-0005
- Forbidden: 切勿使用 Legacy Input Manager (`Input.GetKey`, `Input.mousePosition`) — source: ADR-0005
- Guardrail: 输入轮询：单次 OverlapPoint per frame (~0.1ms) — source: ADR-0005

---

## Acceptance Criteria

*From GDD `design/gdd/scroll-interaction-system.md`, scoped to this story:*

- [ ] GIVEN 玩家进入碎片 A（3 个 InteractiveObject），WHEN OnFragmentTransitioned 触发，THEN InteractionManager 读取 3 个物件定义，为每个创建 BoxCollider2D/CircleCollider2D。光标移到物件上时通过 Physics2D.OverlapPoint 检测到悬停。

- [ ] GIVEN 碎片过渡正在进行（FadeOut/FadeIn），WHEN 玩家点击画面，THEN 点击被忽略——Action Map 为 Inactive，InteractionManager.Update() 在 Action Map 检查时提前返回，不调用 Physics2D.OverlapPoint。

- [ ] GIVEN 碎片 B 有 0 个 InteractiveObject，WHEN 玩家进入该碎片，THEN InteractionManager 悬停检测在空列表上无操作。`_activeObjects` 列表为空，OverlapPoint 仍被调用但无匹配结果。不显示任何墨点或提示。

- [ ] GIVEN 交互状态为 Idle，WHEN 玩家光标进入物件碰撞区域，THEN _currentState 转换为 Active，检测循环正常运行。

---

## Implementation Notes

*Derived from ADR-0005 Implementation Guidelines:*

核心检测循环 (`InteractionManager.Update()`):
```csharp
public class InteractionManager : MonoBehaviour
{
    [SerializeField] private LayerMask _interactableLayer;
    [SerializeField] private ContactFilter2D _filter; // layerMask + no triggers

    private Collider2D[] _results = new Collider2D[4]; // 固定缓冲, non-alloc
    private Collider2D _lastHovered;
    private List<InteractiveObject> _activeObjects = new();

    private InteractionState _currentState = InteractionState.Idle;
    private PlayerControls _controls;

    void Update()
    {
        // Action Map 门控 — ADR-0005
        if (InputManager.CurrentActionMap != ActionMap.Gameplay)
            return;

        if (_currentState is InteractionState.Dragging or
                         InteractionState.ChoicePresenting or
                         InteractionState.Examining or
                         InteractionState.Blocked)
            return;

        Vector2 mousePos = _controls.Gameplay.Point.ReadValue<Vector2>();
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

        int hitCount = Physics2D.OverlapPoint(worldPos, _filter, _results);

        Collider2D hit = hitCount > 0 ? _results[0] : null;

        if (hit != _lastHovered)
        {
            if (_lastHovered != null)
                OnHoverExitHandler(_lastHovered);
            if (hit != null)
                OnHoverEnterHandler(hit);
        }
        else if (hit != null)
        {
            OnHoverStayHandler(hit);
        }

        _lastHovered = hit;
    }
}
```

碰撞体重建 (OnFragmentTransitioned 触发):
```csharp
public void OnFragmentTransitioned(string chapterKey, string fragmentId)
{
    ClearAllColliders();

    MemoryFragment fragment = DataManager.GetFragment(chapterKey, fragmentId);
    if (fragment == null || fragment.InteractiveObjects.Length == 0)
    {
        _activeObjects.Clear();
        _currentState = InteractionState.Idle;
        return;
    }

    foreach (var obj in fragment.InteractiveObjects)
    {
        if (obj.DefaultState == ObjectState.Hidden)
            continue; // Hidden 物件——无碰撞体

        var go = new GameObject($"Interactable_{obj.ObjectId}");
        go.transform.position = obj.HitboxCenter;
        go.layer = _interactableLayer;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = obj.HitboxSize;
        col.isTrigger = true;

        // Disabled 物件有碰撞体但不响应 — 在 OnInteract 中检查
        _activeObjects.Add(obj);
    }

    _currentState = InteractionState.Active;
}
```

交互状态机 (`InteractionState` 枚举):
```csharp
public enum InteractionState
{
    Idle,              // 无碎片活跃
    Active,            // 正常悬停检测
    Dragging,          // 拖拽进行中 (Story 003)
    ChoicePresenting,  // 选择面板展示中 (Story 004)
    Examining,         // 放大查看模式 (Story 002)
    Blocked            // 过渡/文本展示中
}
```

状态互斥规则 (GDD 规则 5):
| 状态 | 允许的交互 | 阻断 |
|------|-----------|------|
| Active | 悬停 → 点击 / 拖拽 / 悬停等待 | — |
| Dragging | 仅当前拖拽 | 点击、悬停其他物件、碎片切换 |
| ChoicePresenting | 仅 ChoiceGroup 选项按钮 | 所有画面物件交互、碎片切换 |
| Examining | 仅 Cancel 退出 | 所有其他交互 |
| Blocked | 无 | 全部交互 |

Action Map 检查优先级:
- `Update()` 第一行必须检查 `CurrentActionMap == Gameplay`
- Inactive 时提前返回——不读取鼠标位置、不调用 OverlapPoint
- 过渡期间 SceneManager 已将 Action Map 设为 Inactive

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 四个交互类型（Touch/Hover/Drag/Examine）处理 + 10 个静态事件 + InteractionResult 分发
- Story 003: 拖拽机制（5px/30px 阈值、物件跟随、拖痕、弹回）
- Story 004: 选择面板流程（PresentChoice → HUD → ChangeTracker）+ Escape 取消
- 微动画 (#9): 墨点 L1/L2/L3 视觉反馈——本 Story 只负责检测

---

## QA Test Cases

- **AC-1**: 碰撞体创建 + 悬停检测
  - Given: OnFragmentTransitioned 被调用，碎片有 3 个 InteractiveObject（全部 DefaultState=Active）
  - When: Update() 运行，光标位于物件 1 的碰撞区域内
  - Then: `_results[0]` 非 null；`_lastHovered` 设置为该物件的 Collider2D；首次检测到悬停时 `OnHoverEnterHandler` 被调用
  - Edge cases: 两个物件碰撞区域重叠 → `_results[0]` 是 SortOrder 最高者；SortOrder 相同 → 确定性选择（实例 ID 较小者）

- **AC-2**: 过渡保护
  - Given: SceneManager 正在进行碎片过渡，Action Map 设为 Inactive
  - When: Update() 运行，玩家点击画面
  - Then: Update() 在 Action Map 检查处提前返回；不调用 OverlapPoint；不触发任何交互事件
  - Edge cases: 过渡期间移动鼠标 → 无悬停状态变化记录

- **AC-3**: 零物件碎片
  - Given: OnFragmentTransitioned 被调用，碎片 InteractiveObjects 数组长度为 0
  - When: Update() 运行，光标在画面任意位置
  - Then: `_activeObjects` 列表为空；OverlapPoint 仍被调用但无匹配；_currentState 保持 Idle
  - Edge cases: 随后过渡到有物件的碎片 → 碰撞体正确创建，_currentState 转为 Active

- **AC-4**: 状态转换 — Idle → Active
  - Given: InteractionManager 处于 Idle 状态
  - When: OnFragmentTransitioned 完成，碰撞体已创建
  - Then: _currentState = Active；Update() 中的状态守卫允许悬停检测继续
  - Edge cases: 碎片过渡期间调用 OnFragmentTransitioned → 忽略；CurrentState 不应从 Blocked 直接变为 Active

- **AC-5**: 悬停离开检测
  - Given: _lastHovered = 物件 A 的 Collider2D
  - When: 光标移出物件 A 的碰撞区域（OverlapPoint 返回不同结果或无结果）
  - Then: OnHoverExitHandler(_lastHovered) 被调用；_lastHovered 更新为新物件或 null
  - Edge cases: 光标移出画面 → hitCount=0，_lastHovered 设为 null
```

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/scroll-interaction/core-engine_test.cs` — must exist and pass

**Status**: [x] Created (18 tests)

---

## Dependencies

- Depends on: input-system Story 001 (PlayerControls + Action Map 状态机), scene-management Story 003 (OnFragmentTransitioned 事件)
- Unlocks: Story 002 (交互类型 + 事件广播), Story 003 (拖拽系统), Story 004 (选择流程)

---

## Completion Notes

**Completed**: 2026-05-14
**Criteria**: 5/5 passing
**Deviations**:
- ADVISORY: `IDataManager.cs` modified — added `GetCachedFragment()` sync cache method. Necessary for synchronous fragment data access during `OnFragmentTransitioned` callback.
- ADVISORY: Programmatic `_pointAction` caching via `FindAction("Point")` instead of C# generated `PlayerControls.Point` property. Consistent with existing InputManager pattern.
- ADVISORY: `InteractiveObject.cs` expanded from minimal stub — added HitboxCenter, HitboxSize, DefaultState, Type, SortOrder fields.
**Test Evidence**: Unit test at `tests/unit/scroll-interaction/core-engine_test.cs` (18 tests covering 5 ACs + state machine + edge cases + ADR-0001 lifecycle)
**Code Review**: Complete — 4 issues found (Camera.main caching, _pointAction null guard, SortOrder disambiguation, layer index caching), all fixed. Unity 6.3 engine specialist: CLEAN (lean mode).
