# Story 004: 选择流程 + Escape 取消

> **Epic**: 记忆画卷交互系统 (InteractionManager)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/scroll-interaction-system.md`
**Requirement**: `TR-scroll-interaction-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0001: 事件总线架构 / ADR-0007: SO 不可变 + Overlay
**ADR Decision Summary**: 选择完成通过 static event 触发 ChangeTracker.ApplyChanges；ContentChanges 通过 overlay 机制应用；选择面板的 Escape 取消实现"不做选择"路径

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: UI Toolkit 的 UIDocument 面板展示由 UI 框架 (#5) 处理；Action Map 切换使用 InputSystem 包——post-cutoff API。`[SerializeReference]` 在 IL2CPP 构建中用于 ContentChange 多态——需要 link.xml 保护。
**Performance**: N/A — 面板展示/隐藏为事件驱动一次性操作，非每帧热路径。选择流程为 async 等待玩家输入，无持续计算开销。

**Control Manifest Rules (Feature Layer)**:
- Required: `static event Action<T>` 用于所有跨系统通信 — source: ADR-0001
- Required: Base SO (immutable) + ChangeTracker._overlay (mutable) 两层模型 — source: ADR-0007
- Forbidden: 切勿在运行时直接修改 ScriptableObject 字段 — 使用 ApplyChanges() → _overlay — source: ADR-0007
- Forbidden: 切勿通过事件传递 MonoBehaviour 引用 — source: ADR-0001

---

## Acceptance Criteria

*From GDD `design/gdd/scroll-interaction-system.md`, scoped to this story:*

- [ ] GIVEN 玩家点击一个 Touch 物件且 ResultType = PresentChoice，WHEN ChoiceGroup 有 2 个选项，THEN HUD 展示选择面板——面板出现在物件旁边（优先右侧，空间不足时下方）。Action Map 切换到 UI。OnChoiceHover 事件在光标悬停于选项上时触发。

- [ ] GIVEN 选择面板展示中且有 2 个选项，WHEN 玩家点击一个选项，THEN 该选项的 choiceId 通过 OnChoiceSelected 事件广播。ChangeTracker.ApplyChanges(fragmentId, choiceId, chosenOption.ContentChanges) 被调用。选择面板淡出 (0.3s)。Action Map 切回 Gameplay。_currentState 恢复为 Active。

- [ ] GIVEN 选择面板展示中，WHEN 玩家按 Escape，THEN 面板关闭，Action Map 切回 Gameplay。不触发任何 ContentChanges——"不做选择"是有效的交互结果。_currentState 恢复为 Active。

- [ ] GIVEN ChoiceGroup 的 MaxSelections=1 且只有一个可用选项（其他选项的 ConditionGroup 不满足），WHEN 玩家点击物件，THEN 跳过选择面板——直接触发该选项的 ContentChanges。此行为无障碍等价于"非选择式的揭示"。

---

## Implementation Notes

*Derived from GDD 规则 8 + 规则 3 (选择面板呈现):*

选择面板流程:
```csharp
// 从 Story 002 的 DispatchInteractionResult 中调用
private async Task HandlePresentChoice(string choiceGroupId)
{
    var choiceGroup = _currentFragment.GetChoiceGroup(choiceGroupId);
    if (choiceGroup == null)
    {
        Debug.LogWarning($"ChoiceGroup [{choiceGroupId}] not found in fragment [{_currentFragment.FragmentId}]");
        return;
    }

    // 单选项 + MaxSelections=1 → 自动应用，跳过面板 (TR-005)
    var availableOptions = choiceGroup.Options
        .Where(o => ChangeTracker.EvaluateCondition(o.ChoiceCondition))
        .ToArray();

    if (choiceGroup.MaxSelections == 1 && availableOptions.Length == 1)
    {
        await ApplyChoice(availableOptions[0], _currentFragment.FragmentId);
        return;
    }

    if (availableOptions.Length == 0)
    {
        Debug.LogWarning($"ChoiceGroup [{choiceGroupId}] has 0 available options");
        return; // 无可选项——不做选择
    }

    // 选择面板展示
    _currentState = InteractionState.ChoicePresenting;
    InputManager.SwitchToUIMode();

    // 面板智能定位——优先右侧，空间不足时下方
    Vector2 panelPosition = CalculateChoicePanelPosition(_lastInteractedObject);

    // HUD 展示面板——返回玩家选择的 choiceId，若 Escape 则返回 null
    string selectedChoiceId = await HUD.ShowChoicePanel(choiceGroup, panelPosition);

    if (selectedChoiceId == null)
    {
        // Escape 取消——AC-5
        HandleChoiceCancelled();
        return;
    }

    var chosenOption = choiceGroup.Options
        .FirstOrDefault(o => o.ChoiceId == selectedChoiceId);
    if (chosenOption == null)
    {
        HandleChoiceCancelled();
        return;
    }

    await ApplyChoice(chosenOption, _currentFragment.FragmentId);
}
```

应用选择:
```csharp
private async Task ApplyChoice(ChoiceOption option, string fragmentId)
{
    OnChoiceSelected?.Invoke(option.ChoiceId);

    // ADR-0007: ApplyChanges 写入 overlay——不修改 SO
    ChangeTracker.ApplyChanges(fragmentId, option.ChoiceId, option.ContentChanges);

    // 面板淡出
    await HUD.HideChoicePanel(0.3f);

    // 恢复 Gameplay 状态
    InputManager.SwitchToGameplayMode();
    _currentState = InteractionState.Active;
}
```

Escape 取消:
```csharp
private void HandleChoiceCancelled()
{
    // AC-5: "不做选择"是有效结果——不触发 ContentChanges
    HUD.HideChoicePanel(0.3f); // fire-and-forget
    InputManager.SwitchToGameplayMode();
    _currentState = InteractionState.Active;
    // 不调用 ChangeTracker.ApplyChanges —— 无内容变化
}
```

面板智能定位:
```csharp
private Vector2 CalculateChoicePanelPosition(InteractiveObject anchor)
{
    Vector2 screenPos = Camera.main.WorldToScreenPoint(anchor.Transform.position);
    float panelWidth = 300f;  // USS 中定义
    float panelHeight = 200f; // 估算值

    // 优先右侧
    Vector2 rightPos = screenPos + new Vector2(50f, 0f);
    if (rightPos.x + panelWidth <= Screen.width)
        return rightPos;

    // 空间不足——下方
    Vector2 belowPos = screenPos + new Vector2(0f, -50f);
    if (belowPos.y - panelHeight >= 0)
        return belowPos;

    // 兜底——屏幕中央
    return new Vector2(Screen.width / 2f, Screen.height / 2f);
}
```

单选项自动应用 (TR-005):
```csharp
// 在 HandlePresentChoice 中——上面的代码片段中已包含此逻辑
// MaxSelections == 1 && availableOptions.Length == 1
//   → 直接 ApplyChoice，不展示选择面板
// 这实现了 GDD 规则: "只有 1 个可用选项的 ChoiceGroup 等同于非选择式的揭示"
```

Escape 输入处理:
```csharp
// 在 InteractionManager 中订阅 UI Action Map Cancel 事件
void OnEnable()
{
    _controls.UI.Cancel.performed += OnUICancelPerformed;
}

void OnDisable()
{
    _controls.UI.Cancel.performed -= OnUICancelPerformed;
}

private void OnUICancelPerformed(InputAction.CallbackContext ctx)
{
    if (_currentState == InteractionState.ChoicePresenting)
    {
        // HUD 内部的 Escape 处理——HUD 返回 selectedChoiceId=null
        // HandlePresentChoice 中的 selectedChoiceId == null 分支处理此情况
    }
    else if (_currentState == InteractionState.Examining)
    {
        // Story 002: Examine 模式中的 Cancel 退出
    }
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: PresentChoice 流程的启动（HUD.ShowChoicePanel 调用）和 InteractionResult 分发——本 Story 处理面板展示后的所有逻辑
- 变化追踪 (#12): ChangeTracker.ApplyChanges 实现——本 Story 只调用该接口
- HUD (#17): ShowChoicePanel/HideChoicePanel 的 UXML 渲染、面板样式、手写墨迹选项渲染
- UI 框架 (#5): Action Map 切换、Theme.uss 变量
- Story 001: _currentState 状态机定义和守卫

---

## QA Test Cases

- **AC-1**: 双选项选择面板 — 选择选项 1
  - Given: 玩家点击 Touch 物件，ChoiceGroup 有 2 个可用选项（选项 A，选项 B）
  - When: 玩家点击选项 A
  - Then: OnChoiceSelected("option_a") 事件触发；ChangeTracker.ApplyChanges(fragmentId, "option_a", ContentChanges[]) 被调用；HUD.HideChoicePanel(0.3f) 被调用；InputManager.SwitchToGameplayMode() 被调用；_currentState=Active
  - Edge cases: 玩家在 0.3s 淡出期间点击另一物件 → _currentState 守卫阻止交互，直到动画完成

- **AC-2**: Escape 取消选择
  - Given: 选择面板展示中，有 2 个可用选项
  - When: 玩家按 Escape/Cancel
  - Then: HUD.HideChoicePanel(0.3f) 被调用；InputManager.SwitchToGameplayMode() 被调用；ChangeTracker.ApplyChanges 未被调用；OnChoiceSelected 事件未被触发；_currentState=Active
  - Edge cases: Escape 处于非选择状态（Active/Dragging/Examining）→ Cancel 输入被输入系统或 Story 002 处理

- **AC-3**: 单选项自动应用
  - Given: ChoiceGroup.MaxSelections=1，选项 A 的 ConditionGroup 满足，选项 B 的 ConditionGroup 未满足
  - When: 玩家点击物件触发 PresentChoice
  - Then: 不展示选择面板；ApplyChoice(option_a, fragmentId) 直接调用；OnChoiceSelected("option_a") 触发；ChangeTracker.ApplyChanges 被调用；_currentState 从未变为 ChoicePresenting
  - Edge cases: 所有 0 个选项满足条件 → 不展示面板，不触发 ContentChanges，_currentState 恢复为 Active

- **AC-4**: 无可用选项
  - Given: ChoiceGroup 所有选项的 ChoiceCondition 均评估为 false
  - When: 玩家点击物件触发 PresentChoice
  - Then: LogWarning "ChoiceGroup [id] has 0 available options" 被记录；不展示选择面板；不触发 ContentChanges；_currentState 恢复为 Active
  - Edge cases: ChoiceGroup 定义中 Options 数组为空 → 与无可用选项处理相同

- **AC-5**: 选择面板智能定位 — 优先右侧
  - Given: 锚点物件 x < Screen.width - 300px - 50px（右侧有足够空间）
  - When: CalculateChoicePanelPosition 被调用
  - Then: 返回锚点右侧 50px 处的位置
  - Edge cases: 右侧空间不足 → 返回锚点下方 50px 处；上下方都空间不足 → 返回屏幕中央

- **AC-6**: OnChoiceHover 广播
  - Given: 选择面板展示中，光标悬停在选项 B 上
  - When: HUD 检测到选项悬停
  - Then: OnChoiceHover?.Invoke("option_b") 被调用
  - Edge cases: 快速将光标从选项 A 移到选项 B → OnChoiceHover("option_a") 随后 OnChoiceHover("option_b") 被调用——由 HUD 内部管理
```

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/scroll-interaction/choice-flow_test.cs` — must exist and pass

**Status**: [x] Created — `tests/integration/scroll-interaction/choice-flow_test.cs` (8 tests)

---

## Dependencies

- Depends on: Story 001 (核心检测引擎 + 状态机), Story 002 (PresentChoice 启动 + InteractionResult 分发), memory-change-tracking Story 001 (ChangeTracker.ApplyChanges), ui-framework Story 001 (UIPanelStack — HUD 面板管理)
- Unlocks: None — 此 Epic 中的最后一个 Story

---

## Completion Notes

**Completed**: 2026-05-14
**Criteria**: 7/7 passing
**Deviations**:
- ChangeTracker.ApplyChanges() stubbed with TODO — blocked by memory-change-tracking epic
- Implemented with incomplete dependencies per user directive
**Test Evidence**: `tests/integration/scroll-interaction/choice-flow_test.cs` (8 tests, all passing)
**Code Review**: Complete — STATE-01 fixed (auto-apply path no longer calls ApplyChoice/HideChoicePanel)
