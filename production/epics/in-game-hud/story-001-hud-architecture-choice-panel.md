# Story 001: HUD 架构 + 选择面板

> **Epic**: 游戏内HUD (InGameHUD)
> **Status**: Complete
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/in-game-hud.md`
**Requirement**: `TR-in-game-hud-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架
**ADR Decision Summary**: UI Toolkit (UIDocument + VisualElement) + LIFO 面板栈 + Theme.uss 全局 CSS 变量。HUD 是面板栈之下的持久层——不在栈中，由 GameplayInputActive 标志控制可见性。选择面板使用 UXML 定义结构，USS 引用 Theme.uss 样式变量。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: UI Toolkit runtime 是 Unity 6 新特性——LLM 训练数据覆盖有限。USS transition 仅支持 opacity/transform（GPU 加速），color/size 变化需代码驱动补间。

**Control Manifest Rules (Feature Layer)**:
- Required: UI Toolkit (UIDocument + VisualElement) for all runtime UI — source: ADR-0006
- Required: Theme.uss global CSS variables for visual consistency — source: ADR-0006
- Forbidden: Never use UGUI Canvas — source: ADR-0006

---

## Acceptance Criteria

*From GDD `design/gdd/in-game-hud.md`, scoped to this story:*

- [ ] GIVEN 交互系统调用 ShowChoicePanel(group with 2 options)，WHEN HUD 渲染选择面板，THEN 面板出现在锚点物件旁边 (优先右侧 offset +40px)，2 个选项显示为手写体文字 + 朱砂墨点。键盘焦点在第一个选项上。InputSystem.SwitchToUIMode() 被调用。

- [ ] GIVEN 选择面板展示中，WHEN 玩家点击一个选项，THEN HUD 调用 ChangeTracker.ApplyChanges(option.ContentChanges) → HideChoicePanel 关闭面板 → InputSystem.SwitchToGameplayMode()。

- [ ] GIVEN 选择面板展示中，WHEN 玩家按 Escape，THEN HideChoicePanel 关闭面板 → InputSystem.SwitchToGameplayMode()。不触发任何 ContentChanges。

- [ ] GIVEN 一个 ChoiceGroup 的所有选项被 ConditionGroup 过滤后仅剩 0 个可用选项，WHEN 交互系统调用 ShowChoicePanel，THEN HUD 不展示面板——记录 LogWarning。若仅剩 1 个选项，由交互系统直接触发（HUD 不参与——GDD 规则 2）。

---

## Implementation Notes

*Derived from ADR-0006 Implementation Guidelines:*

### HUD VisualElement Tree (根结构)

```
HUD (VisualElement, Game 场景 UIDocument 内)
├── #fragment-text-overlay      // 碎片文本浮层 (Story 003)
├── #choice-panel               // 选择面板 (本 Story)
│   ├── #choice-prompt          // 选择提示文本
│   └── #choice-options         // 选项列表容器
│       ├── .choice-option      // 单个选项 (手写墨迹 + 朱砂墨点)
│       └── ...
├── #association-paths          // 关联路径可视化 (Story 002)
├── #chapter-progress           // 章节进度 (Story 003)
└── #interaction-hint           // 交互提示 (Story 003)
```

### InGameHUD MonoBehaviour

```csharp
public class InGameHUD : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;
    [SerializeField] private VisualTreeAsset _choiceOptionTemplate; // UXML template

    private VisualElement _choicePanel;
    private VisualElement _choiceOptions;
    private Label _choicePrompt;
    private ChoiceGroup _currentGroup;

    void OnEnable()
    {
        // Subscribe to InteractionManager events (ADR-0001 pattern)
        InteractionManager.OnShowChoicePanel += HandleShowChoicePanel;
        InteractionManager.OnShowFragmentText += HandleShowFragmentText;
        // ... other subscriptions in Story 002/003

        _choicePanel = _uiDocument.rootVisualElement.Q("#choice-panel");
        _choiceOptions = _uiDocument.rootVisualElement.Q("#choice-options");
        _choicePrompt = _uiDocument.rootVisualElement.Q<Label>("#choice-prompt");
        _choicePanel.visible = false;
    }

    void OnDisable()
    {
        InteractionManager.OnShowChoicePanel -= HandleShowChoicePanel;
        InteractionManager.OnShowFragmentText -= HandleShowFragmentText;
    }
}
```

### Choice Panel 定位算法

```csharp
Vector2 CalculatePanelPosition(Vector2 anchorScreenPos, float panelWidth, float panelHeight)
{
    // Priority: right side (+40px horizontal offset)
    Vector2 pos = anchorScreenPos + new Vector2(40, 0);
    if (pos.x + panelWidth > Screen.width)
        pos = anchorScreenPos - new Vector2(panelWidth + 40, 0); // flip left
    if (pos.x < 0)
        pos = anchorScreenPos + new Vector2(0, 20); // fallback below
    return pos;
}
```

### 选项渲染

```csharp
void RenderChoiceOptions(ChoiceGroup group)
{
    _choiceOptions.Clear();
    foreach (var choice in group.Choices)
    {
        var optionEl = _choiceOptionTemplate.CloneTree();
        // 手写体文本 (TextMeshPro 字体 via UXML label)
        optionEl.Q<Label>(".choice-text").text =
            LocalizationManager.GetLocalizedString(choice.LabelKey);
        // 朱砂墨点 (L1 静态——悬停时 L2 脉动由 Story 003/微动画处理)
        _choiceOptions.Add(optionEl);
    }
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 关联路径可视化 (#association-paths)
- Story 003: 文本浮层、章节进度、交互提示
- Story 004: MVVM 数据绑定实现、HUD 可见性规则表
- 微动画 (#9): L2 脉动（选项悬停时墨点动画）
- 交互反馈 (#18): 选择确认音效（sfx_choice_confirm）
- 交互系统 (#11): ChoiceGroup 结构定义、选项 Condition 过滤

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: ShowChoicePanel renders choice panel
  - Setup: HUD initialized; InteractionManager fires OnShowChoicePanel with a 2-option ChoiceGroup; anchorPosition at (500, 300)
  - Verify: #choice-panel visible; 2 .choice-option elements present; panel positioned at anchor + 40px right; keyboard focus on first option
  - Pass condition: Panel visible with correct option count, positioning, and focus

- **AC-2**: Player clicks an option → ApplyChanges → HideChoicePanel
  - Setup: Choice panel open with 2 options; first option has ContentChange[]
  - Verify: Click on first option → ChangeTracker.ApplyChanges called with correct ContentChange[] → #choice-panel hidden → InputSystem.SwitchToGameplayMode called
  - Pass condition: Full click-to-apply-to-hide chain executes; Action Map returns to Gameplay

- **AC-3**: Escape key closes panel without changes
  - Setup: Choice panel open with 2 options
  - Verify: Press Escape → #choice-panel hidden → InputSystem.SwitchToGameplayMode called; ChangeTracker.ApplyChanges NOT called
  - Pass condition: Panel closes cleanly; no ContentChanges applied

- **AC-4**: Empty choice panel edge case
  - Setup: InteractionManager fires OnShowChoicePanel with ChoiceGroup containing 0 available options (all filtered out)
  - Verify: HUD logs LogWarning; #choice-panel remains hidden
  - Pass condition: No panel shown; no exception thrown; warning logged

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/hud-architecture-choice-panel-evidence.md` — manual walkthrough doc or interaction test

**Status**: [x] Created — production/qa/evidence/hud-architecture-choice-panel-evidence.md

---

## Dependencies

- Depends on: UI 框架 Story 002 (UIPanelStack + Theme.uss); 交互系统 Story 003 (OnShowChoicePanel event)
- Unlocks: Story 002 (关联路径可视化 uses same HUD root VisualElement)

---

## Completion Notes

- **Completed**: 2026-05-19
- **Implementation**: InGameHUD.cs (ShowChoicePanel, HideChoicePanel, RenderChoiceOptions, CalculatePanelPosition)
- **UXML**: assets/uxml/in-game-hud.uxml (#choice-panel, #choice-options, #choice-prompt)
- **USS**: assets/uss/in-game-hud.uss (.choice-panel, .choice-option, .choice-text, .choice-prompt)
- **Evidence**: production/qa/evidence/hud-architecture-choice-panel-evidence.md
- **Deviations**: None. InteractionManager.OnShowChoicePanel does not exist as a static event — ShowChoicePanel is called directly via IHUD interface injection, consistent with InteractionManager DispatchInteractionResult flow.
- **Blockers**: None
