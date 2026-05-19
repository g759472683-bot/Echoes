# Story 004: MVVM 数据绑定 + 显示/隐藏规则

> **Epic**: 游戏内HUD (InGameHUD)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/in-game-hud.md`
**Requirement**: `TR-in-game-hud-002`, `TR-in-game-hud-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架 + ADR-0001: 事件总线
**ADR Decision Summary**: HUD 使用 UI Toolkit 的 INotifyBindablePropertyChanged 实现 MVVM 数据绑定——AssociationPathsDataSource 和 ChapterProgressDataSource 在数据变化时自动更新 UI。HUD 可见性由 GameplayInputActive 标志 + 面板栈状态共同决定——不在面板栈中，但受 UI 框架输入门控。MVVM 更新节流至 10Hz（每 100ms 最多一次 UI 刷新），多个属性变化批处理。

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: INotifyBindablePropertyChanged 是 Unity 6 新 API——LLM 训练数据无覆盖。MVVM 绑定在频繁更新时的性能未验证——必须实现节流策略（dirty flag + schedule.Execute batch update）。HUD 数据源更新频率限制在 10Hz。

**Control Manifest Rules (Feature Layer)**:
- Required: MVVM data binding with throttling — batch updates with dirty flag, max 10Hz HUD refresh — source: ADR-0006
- Required: static event Action<T> pattern — OnFragmentChanged, OnChoiceSelected subscriptions — source: ADR-0001

---

## Acceptance Criteria

*From GDD `design/gdd/in-game-hud.md`, scoped to this story:*

- [ ] GIVEN ChapterProgressDataSource.VisitedCount 从 3 变为 4，WHEN propertyChanged 触发，THEN #chapter-progress 的墨点自动更新——第 4 个墨点变为实心朱砂。UI 刷新率不超过 10Hz（连续 3 次属性变化在 30ms 内 → 仅触发 1 次 layout 更新）。

- [ ] GIVEN 一个 ChoiceGroup 被提交（选择完成），WHEN HideChoicePanel 执行，THEN #choice-panel 隐藏（display:none），#association-paths 和 #chapter-progress 重新可见。

- [ ] GIVEN 游戏状态从 Gameplay 切换到过渡 (OnFragmentTransitionStarted)，WHEN HUD 检测到过渡，THEN 所有子面板可见性标志设为 false → HUD 完全隐藏。过渡完成后 (OnFragmentTransitioned) → HUD 恢复——但仅恢复过渡前可见的子元素。

- [ ] GIVEN UI 框架面板栈非空（暂停菜单或设置面板打开），WHEN 面板栈状态变化，THEN HUD 根据面板栈深度自动隐藏（depth > 0 → hidden）。面板栈清空后 HUD 恢复可见。

---

## Implementation Notes

*Derived from ADR-0006 Implementation Guidelines + GDD rules 7–8:*

### MVVM Data Sources

```csharp
// 关联路径数据源
public class AssociationPathsDataSource : INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    private List<PathCandidateData> _candidates;
    [CreateProperty]
    public List<PathCandidateData> Candidates
    {
        get => _candidates;
        set
        {
            _candidates = value;
            propertyChanged?.Invoke(this,
                new BindablePropertyChangedEventArgs(nameof(Candidates)));
        }
    }
}

// 章节进度数据源
public class ChapterProgressDataSource : INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    private int _visitedCount;
    [CreateProperty]
    public int VisitedCount
    {
        get => _visitedCount;
        set { _visitedCount = value; NotifyPropertyChanged(nameof(VisitedCount)); }
    }

    private int _totalCount;
    [CreateProperty]
    public int TotalCount
    {
        get => _totalCount;
        set { _totalCount = value; NotifyPropertyChanged(nameof(TotalCount)); }
    }
}
```

### MVVM 节流策略

```csharp
public class HudBindingThrottle
{
    private bool _isDirty;
    private IVisualElementScheduledItem _scheduledUpdate;

    public HudBindingThrottle(VisualElement root)
    {
        // Schedule batch update at 10Hz (every 100ms)
        _scheduledUpdate = root.schedule.Execute(() =>
        {
            if (_isDirty)
            {
                _isDirty = false;
                RefreshAllBindings(); // single layout pass
            }
        });
        _scheduledUpdate.Every(100); // 100ms = 10Hz max
    }

    public void MarkDirty()
    {
        _isDirty = true;
        // Multiple MarkDirty() calls within 100ms → single RefreshAllBindings()
    }
}
```

- 数据源 propertyChanged → MarkDirty() → scheduled Execute batch → RefreshAllBindings()
- 选择面板等非连续更新 UI 不适用此限制——直接更新

### HUD 可见性规则表

| 条件 | HUD 行为 |
|------|---------|
| GameplayInputActive = true ∧ 面板栈空 ∧ 非过渡中 | 完全可见 |
| 选择面板展示中 | 仅 #choice-panel 可见；关联路径和进度隐藏 |
| 过渡中 (OnFragmentTransitionStarted) | 完全隐藏；记录 _preTransitionVisibility |
| 过渡完成 (OnFragmentTransitioned) | 恢复 _preTransitionVisibility 状态 |
| UI 面板栈非空 (暂停/设置) | 完全隐藏 |

```csharp
void EvaluateVisibility()
{
    bool shouldShow = _gameplayInputActive
        && !_isTransitioning
        && UIPanelStack.StackDepth == 0;

    _rootVisualElement.visible = shouldShow;

    if (shouldShow && _choicePanelVisible)
    {
        // Choice panel mode: hide paths + progress, show only choice
        _associationPaths.visible = false;
        _chapterProgress.visible = false;
        _choicePanel.visible = true;
    }
    else if (shouldShow)
    {
        // Normal gameplay
        _associationPaths.visible = true;
        _chapterProgress.visible = true;
        _choicePanel.visible = false;
    }
}
```

### 事件订阅 (ADR-0001 pattern)

```csharp
void OnEnable()
{
    // Input gating
    InputManager.OnGameplayInputActiveChanged += HandleGameplayInputActiveChanged;
    // Transitions
    SceneManager.OnFragmentTransitionStarted += HandleTransitionStarted;
    SceneManager.OnFragmentTransitioned += HandleTransitionEnded;
    // Data updates
    ChapterManager.OnFragmentChanged += HandleFragmentChanged;
    // UI Framework
    UIPanelStack.OnStackChanged += EvaluateVisibility;
}

void OnDisable()
{
    InputManager.OnGameplayInputActiveChanged -= HandleGameplayInputActiveChanged;
    SceneManager.OnFragmentTransitionStarted -= HandleTransitionStarted;
    SceneManager.OnFragmentTransitioned -= HandleTransitionEnded;
    ChapterManager.OnFragmentChanged -= HandleFragmentChanged;
    UIPanelStack.OnStackChanged -= EvaluateVisibility;
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: HUD VisualElement 树结构 + 选择面板渲染
- Story 002: 关联路径 VisualElement 创建
- Story 003: 文本浮层、章节进度墨点渲染、交互提示渲染
- 输入系统 (#1): GameplayInputActive 标志管理
- UI 框架 (#5): UIPanelStack.OnStackChanged 事件

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: MVVM binding with throttle — batch update at 10Hz
  - Given: ChapterProgressDataSource.VisitedCount changes 3→4→5→6 in 30ms (3 rapid changes)
  - When: propertyChanged fires 3 times within a single 100ms window
  - Then: MarkDirty() called 3 times; RefreshAllBindings() called exactly once at next scheduled interval; #chapter-progress reflects final value (VisitedCount=6)
  - Edge cases: Single property change outside batch window → immediate refresh (within 100ms)

- **AC-2**: Choice panel hides — game elements restore
  - Given: Choice panel visible (paths + progress hidden); choice selected
  - When: HideChoicePanel() called
  - Then: #choice-panel display:none; #association-paths visible; #chapter-progress visible
  - Edge cases: Another panel opens immediately after close → transition handled by visibility rules

- **AC-3**: HUD hides during transition and restores
  - Given: HUD fully visible (paths + progress shown, no choice panel); OnFragmentTransitionStarted fires
  - When: Transition begins
  - Then: _preTransitionVisibility recorded; HUD hidden; OnFragmentTransitioned fires → HUD restores to _preTransitionVisibility state (same elements visible)
  - Edge cases: Text overlay active when transition starts → text dismissed and NOT restored after transition

- **AC-4**: HUD hides when UI panel stack non-empty
  - Given: HUD visible in gameplay; pause menu opens (UIPanelStack.StackDepth → 1)
  - When: UIPanelStack.OnStackChanged fires
  - Then: HUD hidden; pause menu closes (StackDepth → 0) → HUD visible again
  - Edge cases: StackDepth goes 0→1→2→1→0 → HUD toggles hidden→visible correctly at each boundary

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/in-game-hud/mvvm_binding_visibility_test.cs` — must exist and pass

**Status**: [x] Created — tests/unit/in-game-hud/mvvm_binding_visibility_test.cs (25 tests)

---

## Dependencies

- Depends on: Story 001 (HUD VisualElement tree); Story 002 (association paths); Story 003 (text overlay, progress, hints); UI 框架 Story 002 (UIPanelStack + OnStackChanged); 输入系统 Story 002 (GameplayInputActive)
- Unlocks: None (final HUD story)

---

## Completion Notes

- **Completed**: 2026-05-19
- **Implementation**: HudBindingThrottle.cs, AssociationPathsDataSource.cs, ChapterProgressDataSource.cs, InGameHUD.cs (EvaluateVisibility, HandleStackChanged, HandleTransitionStarted/Ended, HandleGameplayInputActiveChanged, RefreshAllBindings)
- **InputManager modification**: Added OnGameplayInputActiveChanged event — fires in SwitchToGameplayMode(true), SwitchToUIMode(false), SwitchToInactive(false), SwitchToRebindingMode(false)
- **Test file**: tests/unit/in-game-hud/mvvm_binding_visibility_test.cs — 25 test methods covering throttle, visibility rules, MVVM data sources, candidate truncation, positioning, and strength visual mapping
- **Deviations**: UIPanelStackCore.StackDepth is instance property, not static — visibility evaluation tracks stack depth via OnStackChanged(int depth) event handler with local _stackDepth field.
- **Blockers**: None
