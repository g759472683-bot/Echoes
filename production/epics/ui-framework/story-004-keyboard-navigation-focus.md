# Story 004: 键盘导航 + 焦点管理

> **Epic**: UI 框架 (UIPanelStack)
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/ui-framework.md`
**Requirement**: `TR-ui-framework-003`, `TR-ui-framework-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架
**ADR Decision Summary**: FocusController 管理键盘焦点；PushPanel 自动聚焦第一个可交互元素；PopPanel 恢复上一个面板的最后焦点位置；UI Action Map 的 Navigate/Confirm/Cancel/TabNext/TabPrevious 绑定导航行为

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: UI Toolkit `FocusController` 和 `Focusable` 接口在 Unity 6 中稳定；`FocusNextInDirection` 可用；确保 `focusable="true"` 在 UXML 中设置

**Control Manifest Rules (Foundation Layer)**:
- Required: UI Toolkit for all runtime UI — source: ADR-0006

---

## Acceptance Criteria

*From GDD `design/gdd/ui-framework.md`, scoped to this story:*

- [ ] GIVEN 设置面板中"音量滑块"聚焦，WHEN 玩家按 Arrow Down，THEN 焦点移到下一个可交互元素。焦点视觉指示器（outline）可见
- [ ] GIVEN 面板打开，WHEN 检查焦点，THEN 第一个可交互元素自动获得焦点
- [ ] GIVEN 面板栈中有两个面板（暂停 → 设置），WHEN PopPanel 关闭设置面板，THEN 焦点恢复到暂停面板中最后聚焦的元素
- [ ] GIVEN 无手柄连接，WHEN 任意菜单打开，THEN 不显示手柄按钮提示——仅显示键盘提示

---

## Implementation Notes

*Derived from ADR-0006:*

焦点管理器:
```csharp
public class FocusManager
{
    private Dictionary<string, Focusable> _lastFocused = new();
    private FocusController _focusController;
    
    public void OnPanelOpened(string panelId, VisualElement root)
    {
        // 恢复上次焦点位置
        if (_lastFocused.TryGetValue(panelId, out var lastFocus))
        {
            lastFocus.Focus();
        }
        else
        {
            // 自动聚焦第一个可交互元素
            var first = root.Query<VisualElement>()
                .Where(e => e.focusable)
                .First();
            first?.Focus();
        }
    }
    
    public void OnPanelClosed(string panelId, VisualElement root)
    {
        // 保存当前焦点位置
        _lastFocused[panelId] = root.focusController.focusedElement as Focusable;
    }
}
```

导航键绑定:
```csharp
// InputManager UI Action Map 事件处理
private void OnNavigate(Vector2 direction)
{
    if (direction.y < 0)
        _focusController.FocusNextInDirection(FocusNavigationDirection.Down);
    else if (direction.y > 0)
        _focusController.FocusNextInDirection(FocusNavigationDirection.Up);
    else if (direction.x > 0)
        _focusController.FocusNextInDirection(FocusNavigationDirection.Right);
    else if (direction.x < 0)
        _focusController.FocusNextInDirection(FocusNavigationDirection.Left);
}

private void OnTabNext()
{
    _focusController.FocusNext();
}

private void OnTabPrevious()
{
    _focusController.FocusPrevious();
}
```

Confirm/Cancel 集成:
```csharp
private void OnConfirm()
{
    var focused = _focusController.focusedElement;
    if (focused is Button button)
        button.clicked?.Invoke();
    // 其他可交互类型（Toggle, Slider 等）自行处理
}

private void OnCancel()
{
    _panelStack.PopPanel();
}
```

焦点视觉指示器:
```css
/* Theme.uss */
*:focus {
    outline-color: rgba(var(--color-accent), 0.8);
    outline-width: 2px;
    outline-style: solid;
}
```

UXML 要求: 所有可交互元素设置 `focusable="true"`

手柄提示切换:
- 订阅 InputManager.OnGamepadConnectionChanged
- Gamepad 连接 → 显示手柄提示（如 "Press A"）
- Gamepad 断开 → 隐藏手柄提示，保留键盘提示

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: UIPanelStack 核心——PushPanel/PopPanel 调用本 Story 的焦点方法
- input-system Story 004: OnGamepadConnectionChanged 事件声明
- 各面板的具体 UXML 布局——由各 UI 子系统负责设置 `focusable="true"` 和 `tabIndex`
- UI Toolkit 的 `RadioButtonGroup` / `Slider` / `Toggle` 焦点行为——由 UI Toolkit 内置支持

---

## QA Test Cases

- **AC-1**: 面板打开自动聚焦
  - Given: 暂停面板 UXML 中第一个可交互元素是"继续游戏"按钮
  - When: PushPanel("pause-menu")
  - Then: "继续游戏"按钮获得焦点；焦点指示器（outline）可见
  - Edge cases: 面板中无可聚焦元素 → 无焦点，Cancel 键仍有效

- **AC-2**: ArrowDown 导航
  - Given: "音量滑块"获得焦点（暂停面板中第 3 个元素）
  - When: 玩家按 Arrow Down
  - Then: 焦点移到下一个可交互元素（第 4 个）；焦点指示器跟随移动
  - Edge cases: 最后一个元素按 Arrow Down → 焦点不移动（或循环到第一个——由 UI Toolkit 行为决定）

- **AC-3**: PopPanel 焦点恢复
  - Given: 暂停面板中"设置"按钮曾获得焦点；然后 PushPanel("settings-panel")
  - When: PopPanel() 关闭设置面板
  - Then: 暂停面板重新激活；"设置"按钮恢复焦点
  - Edge cases: 暂停面板被 PopPanel 后再 PushPanel → 上次焦点位置丢失，自动聚焦第一个元素

- **AC-4**: Enter 触发按钮
  - Given: "继续游戏"按钮获得焦点
  - When: 玩家按 Enter
  - Then: 按钮的 `clicked` 事件触发；行为与鼠标点击一致
  - Edge cases: 聚焦元素非 Button（如 Toggle）→ 触发其 Submit 事件

- **AC-5**: 手柄提示可见性
  - Given: 无手柄连接
  - When: 暂停菜单打开
  - Then: 仅显示键盘提示（如 "Press Enter"）；手柄提示隐藏
  - Edge cases: 手柄插入 → OnGamepadConnectionChanged(true) → 手柄提示出现

- **AC-6**: Tab 焦点切换
  - Given: 暂停菜单打开，第 1 个元素聚焦
  - When: 按 Tab
  - Then: 焦点移到下一个焦点组；按 Shift+Tab → 焦点移到上一个焦点组
  - Edge cases: 当前在最后一个焦点组 → Tab 循环到第一个

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/ui-framework/keyboard-navigation_test.cs` — must exist and pass

**Status**: [x] Created — `tests/unit/ui-framework/keyboard-navigation_test.cs` (41 tests)

---

## Dependencies

- Depends on: ui-framework Story 001 (UIPanelStack), Story 002 (Theme.uss — 焦点 outline 样式)
- Unlocks: MainMenu (#19) — 全部菜单键盘导航, InGameHUD (#17) — HUD 焦点管理

---

## Completion Notes

**Completed**: 2026-05-18
**Criteria**: 6/6 auto-verified (all logic testable — no Unity runtime required)
**Deviations**: None
**Test Evidence**: `tests/unit/ui-framework/keyboard-navigation_test.cs` — 41 tests covering all 6 ACs + edge cases
**Code Review**: Skipped (lean mode)
