# Story 002: HoverDetector 悬浮检测引擎

> **Epic**: 输入系统 (InputManager)
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/input-system.md`
**Requirement**: `TR-input-system-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0005: 输入系统架构
**ADR Decision Summary**: 单一 HoverDetector 组件在 Update() 中轮询鼠标位置，执行单次 Physics2D.OverlapPoint（non-alloc），对比上一帧结果分发 OnHoverEnter/OnHoverExit 事件

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: `Physics2D.OverlapPoint` (non-alloc 版本) 在 Unity 6 中 API 无变化；需验证 Interactable 物理层设置

**Control Manifest Rules (Core Layer)**:
- Required: Single Physics2D.OverlapPoint per frame — no EventSystem/Raycaster for interactable detection — source: ADR-0005
- Forbidden: Never pass MonoBehaviour references through events — use value types, string IDs — source: ADR-0001

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [ ] GIVEN 玩家在记忆画卷中移动鼠标，WHEN 鼠标悬浮在一个带有 `Collider2D`（Interactable 层）的物件上方，THEN `HoverDetector` 检测到碰撞并触发 `OnHoverEnter` 事件，携带物件 ObjectId 和屏幕坐标
- [ ] GIVEN 鼠标悬浮在一个可交互物件上，WHEN 玩家按下鼠标左键，THEN `Click` 事件被触发，携带点击位置的屏幕坐标
- [ ] GIVEN 暂停菜单打开（UI 状态），WHEN 玩家移动鼠标到菜单按钮并点击，THEN `HoverDetector` 和 `Click` 事件不传递给记忆画卷交互系统
- [ ] 悬浮检测每帧仅执行一次 Physics2D.OverlapPoint——不在各交互对象 Update 中独立做射线检测

---

## Implementation Notes

*Derived from ADR-0005:*

HoverDetector 核心逻辑:
```csharp
public class HoverDetector : MonoBehaviour
{
    public static event Action<string, Vector2> OnHoverEnter;
    public static event Action<string> OnHoverExit;
    
    private Collider2D _lastHovered;
    private readonly List<Collider2D> _results = new(1);
    private ContactFilter2D _filter;
    
    private void Start()
    {
        _filter = new ContactFilter2D
        {
            layerMask = LayerMask.GetMask("Interactable"),
            useLayerMask = true
        };
        _filter.NoFilter(); // 不过滤任何碰撞体
    }
    
    private void Update()
    {
        // 仅在 Gameplay 状态下执行
        if (_inputManager.CurrentState != InputState.Gameplay) return;
        
        var mousePos = Mouse.current.position.ReadValue();
        var worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        
        // 单次 non-alloc OverlapPoint
        var hitCount = Physics2D.OverlapPoint(worldPos, _filter, _results);
        var current = hitCount > 0 ? _results[0] : null;
        
        if (current != _lastHovered)
        {
            if (_lastHovered != null)
                OnHoverExit?.Invoke(_lastHovered.name);
            if (current != null)
                OnHoverEnter?.Invoke(current.name, mousePos);
            _lastHovered = current;
        }
    }
}
```

UI 状态下抑制:
- 当 `InputManager.CurrentState == InputState.Menu` 时，`Update()` 提前 return
- HoverDetector 不感知 UI Toolkit 事件——输入门控在 Action Map 级别已切断

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: PlayerControls 实例 + Action Map 状态机 — SwitchToGameplayMode/SwitchToUIMode
- Story 003: 按键重绑定
- 交互反馈系统 (#18): OnHoverEnter/OnHoverExit 事件的视觉/音频响应
- 记忆画卷交互系统 (#11): Click/Scroll 事件的消费和处理

---

## QA Test Cases

- **AC-1**: 悬浮进入检测
  - Given: 场景中有一个 GameObject 带 Collider2D（Interactable 层），鼠标不在其上
  - When: 玩家移动鼠标到该物件上方
  - Then: OnHoverEnter 事件触发，参数包含该物件的 ObjectId（来自 Collider2D 所在 GameObject）和屏幕坐标
  - Edge cases: 物件无 Collider2D → 不触发；物件不在 Interactable 层 → 不触发

- **AC-2**: 悬浮离开检测
  - Given: 鼠标悬浮在物件 A 上
  - When: 玩家移动鼠标离开物件 A（到空白区域或物件 B）
  - Then: OnHoverExit 事件触发，参数为物件 A 的 ObjectId
  - Edge cases: 移到物件 B 上 → OnHoverExit(A) + OnHoverEnter(B) 同一帧依次触发

- **AC-3**: 点击事件
  - Given: 鼠标悬浮在物件 A 上，Gameplay 状态
  - When: 玩家按下鼠标左键
  - Then: Click 事件触发，携带屏幕坐标
  - Edge cases: UI 状态下点击 → Click 事件不触发（InputManager.CurrentState != Gameplay）

- **AC-4**: UI 状态抑制
  - Given: 暂停菜单打开（CurrentState = Menu）
  - When: 鼠标在可交互物件上移动
  - Then: HoverDetector.Update() 提前 return；无 OnHoverEnter/OnHoverExit/Click 事件
  - Edge cases: Menu → Gameplay 切换后 → 下一帧恢复检测

- **AC-5**: 无碰撞检测
  - Given: 鼠标在空白区域（无 Interactable 层碰撞体）
  - When: Update() 执行
  - Then: `_results` 为空；若上一帧有悬浮物件 → OnHoverExit 触发；_lastHovered = null
  - Edge cases: 连续多帧无碰撞 → 仅第一帧触发 OnHoverExit

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/input-system/hover-detector_test.cs` — must exist and pass

**Status**: [x] Created (16 test functions, all 5 ACs covered)

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 5/5 passing (16 unit tests)
**Deviations**: None — HoverDetectorCore follows ADR-0005 single OverlapPoint requirement, ADR-0001 static event pattern, and Core layer control manifest rules
**Test Evidence**: Logic — `tests/unit/input-system/hover-detector_test.cs` (16 test functions)
**Code Review**: Skipped (lean mode)

---

## Dependencies

- Depends on: input-system Story 001 (PlayerControls + Action Map state machine — InputManager.CurrentState)
- Unlocks: None directly（下游交互系统消费 HoverDetector 事件）
