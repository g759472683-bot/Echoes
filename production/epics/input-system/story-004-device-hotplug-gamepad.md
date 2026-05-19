# Story 004: 设备热插拔检测 + 手柄菜单支持

> **Epic**: 输入系统 (InputManager)
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/input-system.md`
**Requirement**: `TR-input-system-004`, `TR-input-system-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0005: 输入系统架构, ADR-0001: 事件总线架构
**ADR Decision Summary**: 设备热插拔通过 InputSystem.onDeviceChange 检测；OnGamepadConnectionChanged static event 通知 UI；手柄仅菜单导航

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: `InputSystem.onDeviceChange` 回调在 Unity 6 中已验证可用；`Gamepad.current` 在设备断开后自动置 null

**Control Manifest Rules (Core Layer)**:
- Required: `static event Action<bool> OnGamepadConnectionChanged` — source: ADR-0005
- Required: `static event Action<T>` for all cross-system communication — source: ADR-0001

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [ ] GIVEN 无手柄连接，WHEN 游戏在任意状态下运行，THEN UI 中不显示手柄按钮提示（如 "Press A to confirm"），仅显示键盘提示
- [ ] GIVEN 游戏运行中，WHEN 手柄被插入，THEN `Gamepad.current` 变为非 null，OnGamepadConnectionChanged(true) 触发，UI 导航提示更新为同时显示键盘和手柄提示
- [ ] GIVEN 手柄已连接，WHEN 手柄被拔出，THEN `Gamepad.current` 变为 null，OnGamepadConnectionChanged(false) 触发，手柄提示消失，键盘提示保持
- [ ] GIVEN 手柄连接，WHEN 游戏在 Menu 状态，THEN 手柄 D-Pad/Left Stick/A/B/Start 可导航菜单；Gameplay 状态下手柄输入不响应

---

## Implementation Notes

*Derived from ADR-0005:*

设备热插拔检测:
```csharp
public static event Action<bool> OnGamepadConnectionChanged;

private void OnEnable()
{
    InputSystem.onDeviceChange += OnDeviceChange;
}

private void OnDisable()
{
    InputSystem.onDeviceChange -= OnDeviceChange;
}

private void OnDeviceChange(InputDevice device, InputDeviceChange change)
{
    if (device is Gamepad)
    {
        switch (change)
        {
            case InputDeviceChange.Added:
            case InputDeviceChange.Reconnected:
                OnGamepadConnectionChanged?.Invoke(true);
                break;
            case InputDeviceChange.Removed:
            case InputDeviceChange.Disconnected:
                OnGamepadConnectionChanged?.Invoke(false);
                break;
        }
    }
}
```

手柄范围限制:
- `Gameplay` Action Map 中不包含手柄绑定——仅 `Mouse` 和 `Keyboard` 绑定
- `UI` Action Map 包含手柄绑定：Navigate (D-Pad/Left Stick)、Confirm (Gamepad A)、Cancel (Gamepad B)、Start (Gamepad Start)
- 检查 `Gamepad.current == null` → 手柄 UI 提示隐藏

手柄提示可见性规则:
| Gamepad.current | UI 状态 | 显示 |
|-----------------|---------|------|
| null | 任意 | 仅键盘提示 |
| 非 null | Gameplay | 仅键盘提示（Gameplay map 无手柄绑定） |
| 非 null | Menu | 键盘 + 手柄提示 |

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: PlayerControls 资产 + Action Map 定义——本 Story 消费 Gameplay/UI Map 中的手柄绑定
- 手柄按键重绑定——GDD 规则 7: 手柄绑定保持默认，不可重绑定
- UI 提示的具体渲染——由 UI Framework (#5) 和 Main Menu (#19) 负责

---

## QA Test Cases

- **AC-1**: 无手柄时提示隐藏
  - Given: Gamepad.current == null
  - When: 主菜单或暂停菜单打开
  - Then: UI 中不显示手柄按钮提示；仅显示键盘提示（如 "Press Enter to confirm"）
  - Edge cases: 游戏启动时即无手柄 → 从不显示手柄提示

- **AC-2**: 手柄插入检测
  - Given: 游戏运行中，Gamepad.current == null
  - When: 玩家插入手柄
  - Then: InputSystem.onDeviceChange 触发 Added；OnGamepadConnectionChanged(true) 被调用；UI 更新为同时显示键盘+手柄提示
  - Edge cases: 游戏启动时已有手柄 → 初始化时 Gamepad.current 非 null；无需额外处理

- **AC-3**: 手柄拔出检测
  - Given: 手柄已连接，Menu 状态，UI 显示手柄提示
  - When: 玩家拔出手柄
  - Then: InputSystem.onDeviceChange 触发 Removed；OnGamepadConnectionChanged(false) 被调用；手柄提示消失
  - Edge cases: Gameplay 状态下拔出 → 不影响（Gameplay 不用手柄）

- **AC-4**: 手柄仅 UI 可用
  - Given: 手柄已连接，Gameplay 状态
  - When: 玩家按手柄 A 按钮
  - Then: 无响应——Gameplay Action Map 中无手柄绑定
  - Edge cases: 按 Start 按钮 → Pause action（在 Gameplay map 中未绑手柄，但 Start 在 UI map 中）→ 需要额外处理：Start 绑定放在 Gameplay map 而非 UI map？不——遵循 GDD：Pause 动作绑定 Escape/Gamepad Start，但 GDD 说 Gameplay map 无手柄绑定。→ Pause action 需在 Gameplay map 中绑定 Gamepad Start 作为例外。或者将 Pause 的 Gamepad Start 绑定放在 Gameplay map 中。（确认：GDD AC 第 6 条说"按 Escape 或 Gamepad B"关闭暂停菜单——Gamepad Start 打开暂停菜单。手柄 Start 应能在 Gameplay 状态触发 Pause。）

- **AC-5**: OnGamepadConnectionChanged 订阅
  - Given: UI Framework 订阅了 OnGamepadConnectionChanged
  - When: 手柄插入/拔出
  - Then: UI Framework 收到事件并更新提示文本；无 GC 分配
  - Edge cases: 场景切换后订阅不丢失（OnEnable 重新订阅）

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/input-system/device-hotplug_test.cs` — must exist and pass

**Status**: [x] Created (16 test functions, all 4 ACs covered)

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 4/4 passing (16 unit tests)
**Deviations**: None — DeviceHotplugDetector follows ADR-0005 gamepad scope limitation (menu-only) and ADR-0001 static event pattern. Gamepad hints gating per spec.
**Test Evidence**: Logic — `tests/unit/input-system/device-hotplug_test.cs` (16 test functions)
**Code Review**: Skipped (lean mode)

---

## Dependencies

- Depends on: input-system Story 001 (PlayerControls + Action Map 定义)
- Unlocks: None directly（UI Framework 和 Main Menu 消费 OnGamepadConnectionChanged）
