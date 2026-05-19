# Story 003: 运行时按键重绑定

> **Epic**: 输入系统 (InputManager)
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/input-system.md`
**Requirement**: `TR-input-system-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0005: 输入系统架构
**ADR Decision Summary**: PerformInteractiveRebinding() + SaveBindingOverridesAsJson() 持久化到 PlayerPrefs；重绑定超时 30s

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: `PerformInteractiveRebinding` 在 IL2CPP 中需验证兼容性；`SaveBindingOverridesAsJson` / `LoadBindingOverridesFromJson` API 无变化

**Control Manifest Rules (Core Layer)**:
- Required: Unity New Input System only — source: ADR-0005
- Forbidden: Never use Legacy Input Manager — source: ADR-0005

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [ ] GIVEN 玩家在设置界面点击"修改按键"，WHEN 选择了 `Confirm` 动作进行重绑定，THEN 系统进入 Rebinding 状态——仅监听下一个按键输入，30秒超时后自动恢复原绑定
- [ ] GIVEN 玩家完成了 `Confirm` 动作的重绑定（按下了新按键），WHEN 重绑定操作完成，THEN 新绑定被保存到 `PlayerPrefs`，旧绑定被覆盖，重绑定界面显示新按键名称
- [ ] GIVEN 玩家将两个动作绑定到同一个按键，WHEN 后绑定的完成，THEN 之前的绑定被清除——该动作变为未绑定状态
- [ ] GIVEN 玩家在重绑定过程中断开正在重绑定的设备，WHEN 设备断开，THEN 重绑定超时，恢复到原绑定，显示"设备已断开，重绑定取消"

---

## Implementation Notes

*Derived from ADR-0005:*

重绑定核心流程:
```csharp
public void StartRebinding(string actionName)
{
    var action = _controls.Gameplay[actionName] ?? _controls.UI[actionName];
    if (action == null) return;
    
    _currentState = InputState.Rebinding;
    
    action.PerformInteractiveRebinding()
        .WithTimeout(30f)
        .OnComplete(operation =>
        {
            // 清除重复绑定
            RemoveDuplicateBinding(action, operation.selectedControl);
            // 持久化
            SaveBindings();
            _currentState = InputState.Menu;
        })
        .OnCancel(operation =>
        {
            _currentState = InputState.Menu;
        })
        .Start();
}

private void SaveBindings()
{
    var overrides = _controls.SaveBindingOverridesAsJson();
    PlayerPrefs.SetString(REBINDING_PREFS_KEY, overrides);
    PlayerPrefs.Save();
}

public void LoadBindings()
{
    var overrides = PlayerPrefs.GetString(REBINDING_PREFS_KEY, "");
    if (!string.IsNullOrEmpty(overrides))
        _controls.LoadBindingOverridesFromJson(overrides);
}
```

重复绑定处理:
- `PerformInteractiveRebinding().WithControlsExcluding()` 不适用——允许玩家有意覆盖
- 完成重绑定后扫描所有 Action，若其他 Action 有相同 binding path → 清除旧 Action 的该 binding
- 被清除的 Action 若变为无绑定 → 标记为 Unbound 状态（设置界面显示警告图标）

超时处理:
- `.WithTimeout(30f)` 自动触发 OnCancel
- OnCancel 中恢复 `_currentState = InputState.Menu`——不修改绑定

PlayerPrefs Key 格式: `InputRebindingOverrides`（单一 Key 保存所有重绑定 JSON）

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: PlayerControls 资产 + Action Map 状态机 — Rebinding 状态的定义和切换
- 重绑定 UI 界面 — 由 Main Menu (#19) 设置面板实现（本 Story 仅提供 StartRebinding API + 事件回调）
- 手柄按键重绑定 — GDD 规则 7: 仅键盘/鼠标按键可重绑定

---

## QA Test Cases

- **AC-1**: 正常重绑定流程
  - Given: 玩家在设置界面，当前 Confirm 绑定到 Enter
  - When: 调用 StartRebinding("Confirm")，玩家按下 Space
  - Then: 操作完成；Confirm 绑定变为 Space；旧 Enter 绑定被移除；PlayerPrefs 保存新绑定
  - Edge cases: 玩家在重绑定过程中按 Escape → 操作取消，保持 Enter 绑定

- **AC-2**: 30s 超时
  - Given: 玩家进入重绑定流程
  - When: 30 秒内未按下任何按键
  - Then: OnCancel 触发；绑定不变；状态恢复到 Menu；显示"重绑定超时，已恢复原按键"
  - Edge cases: 29 秒时按键 → 正常完成

- **AC-3**: 重复绑定处理
  - Given: Confirm 绑定到 Enter，Cancel 绑定到 Escape
  - When: 玩家将 Confirm 重绑定到 Escape
  - Then: Confirm 绑定变为 Escape；Cancel 的 Escape 绑定被清除；Cancel 变为未绑定状态
  - Edge cases: Cancel 仅有 Escape 一个绑定 → Cancel 显示警告"未绑定"

- **AC-4**: 设备断开
  - Given: 玩家正在重绑定键盘按键
  - When: 键盘设备断开连接
  - Then: 重绑定取消；恢复原绑定；显示"设备已断开，重绑定取消"
  - Edge cases: 无线键盘信号丢失 → 由 Input System 自动检测设备断开

- **AC-5**: 持久化验证
  - Given: 玩家已完成重绑定（Confirm = Space）
  - When: 游戏重启，InputManager 初始化后调用 LoadBindings()
  - Then: Confirm 绑定仍为 Space
  - Edge cases: PlayerPrefs 被清除 → LoadBindings 返回空字符串，使用默认绑定

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/input-system/key-rebinding_test.cs` — must exist and pass

**Status**: [x] Created (17 test functions, all 5 ACs covered)

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 5/5 passing (17 unit tests)
**Deviations**: None — InputRebindingManager follows ADR-0005 PerformInteractiveRebinding pattern and ADR-0001 static event pattern. Duplicate resolution per spec. PlayerPrefs persistence via IBindingStore abstraction.
**Test Evidence**: Logic — `tests/unit/input-system/key-rebinding_test.cs` (17 test functions)
**Code Review**: Skipped (lean mode)

---

## Dependencies

- Depends on: input-system Story 001 (PlayerControls 实例 + Rebinding 状态)
- Unlocks: Main Menu (#19) 设置面板——按键重绑定界面
