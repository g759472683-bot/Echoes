# Story 001: PlayerControls 设置 + Action Map 状态机

> **Epic**: 输入系统 (InputManager)
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/input-system.md`
**Requirement**: `TR-input-system-001`, `TR-input-system-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0005: 输入系统架构, ADR-0001: 事件总线架构
**ADR Decision Summary**: Input System 包 + PlayerControls C# 生成 + 两个 Action Map 互斥门控; static event Action<T> 模式用于 OnGamepadConnectionChanged

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: 整个 Input System 包 API 在 LLM 知识截止后（Legacy Input Manager 已弃用）；`PerformInteractiveRebinding` 在 IL2CPP 中需验证

**Control Manifest Rules (Core Layer)**:
- Required: Unity New Input System only — InputActionAsset with Gameplay/UI Action Maps, exclusive gating — source: ADR-0005
- Required: `static event Action<T>` for all cross-system communication — source: ADR-0001
- Forbidden: Never use Legacy Input Manager (`Input.GetKey`, `Input.mousePosition`) — source: ADR-0005

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [ ] GIVEN 游戏首次启动，WHEN 引擎完成初始化，THEN `PlayerControls` 实例被创建，`Gameplay` Action Map 被启用，`UI` Action Map 被禁用
- [ ] GIVEN 玩家按下 Escape 键（Gameplay 状态），WHEN 当前无模态 UI 打开，THEN `Pause` 动作触发，`Gameplay` map 被禁用，`UI` map 被启用
- [ ] GIVEN 暂停菜单打开，WHEN 玩家按下 Escape 或 Gamepad B 按钮，THEN `Cancel` 动作触发，`UI` map 被禁用，`Gameplay` map 被启用
- [ ] GIVEN 场景加载开始，WHEN 加载画面显示，THEN 所有 Action Map 被禁用（Inactive 状态），加载期间不处理任何输入
- [ ] GIVEN 输入系统的 Actions Asset 文件缺失，WHEN 游戏启动并尝试加载该资产，THEN 游戏显示错误信息并阻止进入主菜单

---

## Implementation Notes

*Derived from ADR-0005 Implementation Guidelines:*

PlayerControls 资产加载:
```csharp
public class InputManager : MonoBehaviour, IInputManager
{
    private PlayerControls _controls;
    
    public void Initialize()
    {
        _controls = new PlayerControls();
        _controls.Gameplay.Enable();
        _controls.UI.Disable();
    }
}
```

Action Map 互斥切换:
```csharp
public void SwitchToGameplayMode()
{
    _controls.UI.Disable();
    _controls.Gameplay.Enable();
}

public void SwitchToUIMode()
{
    _controls.Gameplay.Disable();
    _controls.UI.Enable();
}

public void SwitchToInactive()
{
    _controls.Gameplay.Disable();
    _controls.UI.Disable();
}
```

四个输入状态:
| 状态 | 激活的 Action Map | 触发条件 |
|------|-------------------|----------|
| **Gameplay** | Gameplay | 游戏启动默认；从任意模态 UI 关闭后切回 |
| **Menu** | UI | 玩家按 Pause、触发选择对话框、进入章节过渡 |
| **Rebinding** | 仅被重绑定的单个 Action | 玩家在设置中点击"修改按键" |
| **Inactive** | 无 | 加载画面、过场动画、引擎启动/关闭期间 |

InputActionAsset 缺失处理: 检查 asset 是否为 null → 显示错误 UI → `SceneManager.LoadScene` 不被调用。

Actions Asset 位置: 放在 Addressables `Shared_UI` 组或 `Resources/` 中（启动时需要立即可用，不依赖异步加载）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: HoverDetector 悬浮检测 — Physics2D.OverlapPoint 逻辑
- Story 003: 按键重绑定 — PerformInteractiveRebinding 流程
- Story 004: 设备热插拔 — Gamepad 检测 + OnGamepadConnectionChanged
- 输入系统 GDD 规则 4（UI 门控第 2 层 IsPointerOverGameObject）— 由 UI Framework (#5) 的 PanelEventHandler 处理

---

## QA Test Cases

- **AC-1**: 启动初始化
  - Given: 游戏启动，引擎初始化完成
  - When: InputManager.Initialize() 被调用
  - Then: PlayerControls 实例非 null；Gameplay Map enabled；UI Map disabled
  - Edge cases: Actions Asset 为 null → 错误信息显示，不进入主菜单

- **AC-2**: Gameplay → Menu 切换
  - Given: 游戏在 Gameplay 状态
  - When: Escape 键被按下（Pause action performed）
  - Then: Gameplay Map disabled；UI Map enabled；当前状态 = Menu
  - Edge cases: 同一帧内多次 Escape → 仅处理第一次（状态机防重入）

- **AC-3**: Menu → Gameplay 切换
  - Given: 游戏在 Menu 状态，无子菜单打开（面板栈仅一层）
  - When: Escape 键被按下（Cancel action performed）
  - Then: UI Map disabled；Gameplay Map enabled；当前状态 = Gameplay
  - Edge cases: 有子菜单时 Escape 不触发 Cancel 切换——由 UI 框架 PopPanel 处理

- **AC-4**: Any → Inactive 切换
  - Given: 游戏在任意状态
  - When: SceneManager 调用 SwitchToInactive()
  - Then: Gameplay Map disabled；UI Map disabled；所有输入不响应
  - Edge cases: Inactive 期间任何按键 → 不触发任何 Action

- **AC-5**: Actions Asset 缺失
  - Given: PlayerControls InputActionAsset 文件不存在或损坏
  - When: InputManager 尝试初始化
  - Then: 捕获异常；显示错误信息"输入系统异常，请重启游戏"；阻止进入主菜单
  - Edge cases: Asset 在 Addressables 加载失败 → 同上述处理

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/input-system/action-map-state-machine_test.cs` — must exist and pass

**Status**: [x] Created (24 tests)

---

## Dependencies

- Depends on: None（输入系统无上游硬依赖）
- Unlocks: input-system Story 002 (HoverDetector), Story 003 (Key Rebinding), Story 004 (Device Hot-plug); ui-framework Story 001 (UIPanelStack needs SwitchToUIMode)

---

## Completion Notes

**Completed**: 2026-05-13
**Criteria**: 5/5 passing
**Deviations**:
- ADVISORY: Programmatic `InputActionAsset` construction via `ScriptableObject.CreateInstance` instead of `PlayerControls` C# generated class. The ADR-0005 Decision section describes manual construction; the Summary mentions generated class. Functionally equivalent.
- OUT OF SCOPE: `GameSceneManager.cs` was touched to update method calls from legacy `SetActionMap()` to new `SwitchToInactive()`/`SwitchToGameplayMode()` API. Necessary integration point.
**Test Evidence**: Integration test at `tests/integration/input-system/action-map-state-machine_test.cs` (24 tests covering all 5 ACs + edge cases + full state machine cycle)
**Code Review**: Complete — 3 issues found (dead assertions, AC-5 coverage gap, re-entrancy guard), all fixed. Unity 6.3 engine specialist: CLEAN.
