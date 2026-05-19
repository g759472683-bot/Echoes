# Story 001: 事件订阅 + 反馈映射 + 优先级 + 防抖 + 转场抑制

> **Epic**: 交互反馈系统 (InteractionFeedback)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/interaction-feedback.md`
**Requirement**: `TR-interaction-feedback-001`, `TR-interaction-feedback-002`, `TR-interaction-feedback-003`, `TR-interaction-feedback-004`, `TR-interaction-feedback-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0014: 交互反馈映射表
**ADR Decision Summary**: 一个轻量 MonoBehaviour——InteractionFeedback——订阅交互系统 (#11) 的 10 个事件 + 场景管理 (#6) 的 2 个过渡事件。事件→反馈映射表 (FeedbackMapping[]) 定义每个事件的视觉+音频响应。优先级系统（0-10）：选择确认 > 拖拽完成 > 交互触发 > 悬停。300ms 防抖按 (objectId, eventName) 键。_feedbackSuppressed flag 在过渡期间抑制所有反馈。纯事件驱动——无 Update()。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯 C# 逻辑——不依赖特定 Unity API。static event Action<T> 订阅模式（ADR-0001）。Debounce 使用 Dictionary<(string, string), float> 记录上次触发时间。

**Control Manifest Rules (Feature Layer)**:
- Required: 10-event → visual+audio mapping table — centralized feedback definition — source: ADR-0014
- Required: Feedback priority system (0-10) — higher suppresses lower — source: ADR-0014
- Required: 300ms debounce per (objectId, eventName) — source: ADR-0014
- Required: Transition suppression via _feedbackSuppressed flag — source: ADR-0014
- Required: Pure event-driven (no Update()) — source: ADR-0014
- Required: static event Action<T> pattern — OnEnable subscribe, OnDisable unsubscribe — source: ADR-0001
- Forbidden: Never implement feedback per-system — centralized mapping table — source: ADR-0014
- Forbidden: Never fire feedback during scene transitions — source: ADR-0014

---

## Acceptance Criteria

*From GDD `design/gdd/interaction-feedback.md`, scoped to this story:*

- [ ] GIVEN 玩家光标移到物件上 (InteractionManager.OnHoverEnter 触发)，WHEN InteractionFeedback 处理事件，THEN 调用 MicroAnimationManager.SetGlowLevel(objectId, L2_Breathing)。无音频播放。0.3s 内同一物件的 OnHoverEnter 再次触发时不重复调用 SetGlowLevel（防抖）。

- [ ] GIVEN 玩家点击物件 (InteractionManager.OnInteract 触发，Touch 类型)，WHEN 处理事件，THEN MicroAnimationManager.PlayTriggered("L3_flash") + AudioManager.PlaySFX("sfx_touch_generic") 调用。优先级 5。

- [ ] GIVEN 玩家在选择面板中确认选择 (InteractionManager.OnChoiceSelected 触发)，WHEN 处理事件，THEN MicroAnimationManager.SetGlowLevel(objectId, L3_InnerGlow) + AudioManager.PlaySFX("sfx_choice_confirm")。优先级 10——如果 OnDragComplete (优先级 8) 在同一帧触发，仅 ChoiceSelected 的反馈执行。

- [ ] GIVEN 碎片过渡进行中 (SceneManager.OnFragmentTransitionStarted 触发)，WHEN 任何交互事件随后到达，THEN _feedbackSuppressed = true → Handle* 方法在入口检查 _feedbackSuppressed → 立即 return，不调用微动画或音频。

- [ ] GIVEN 过渡完成 (SceneManager.OnFragmentTransitioned 触发)，WHEN 下一个交互事件到达，THEN _feedbackSuppressed = false → 反馈正常执行。

---

## Implementation Notes

*Derived from ADR-0014 Implementation Guidelines:*

### FeedbackMapping SO

```csharp
[CreateAssetMenu(menuName = "Echoes/FeedbackMappings")]
public class FeedbackMappings : ScriptableObject
{
    public FeedbackMapping[] Mappings;
}

[Serializable]
public class FeedbackMapping
{
    public string EventName;            // "OnHoverEnter", "OnInteract", ...
    public string VisualAnimationId;     // MicroAnimationManager animation ID
    public string AudioKey;              // AudioManager SFX key (empty = no audio)
    public int Priority;                 // 0-10, higher = more important
    public bool IsDebounced;            // Apply 300ms debounce?
}
```

### 事件→反馈映射表

| 事件 | 视觉 | 音频 | 优先级 | 防抖 |
|------|------|------|--------|------|
| OnHoverEnter | L1→L2 脉动 | — (无) | 2 | Yes (300ms) |
| OnHoverExit | L2→L1 回退 | — | 2 | No |
| OnInteract (Touch) | L2→L3 内光闪烁 (0.3s) | sfx_touch_generic | 5 | Yes (300ms) |
| OnDragStart | 拖痕出现 | sfx_drag_start | 6 | No |
| OnDragComplete | L3 内光闪烁 | sfx_drag_complete | 8 | No |
| OnDragCancel | spring-back 动画 | sfx_drag_cancel | 4 | No |
| OnChoiceSelected | L3 内光 → 墨点变色 | sfx_choice_confirm | 10 | No |
| OnChoiceHover | L1→L2 脉动 | sfx_hover_tick | 3 | Yes (300ms) |
| OnRevealObject | 物件出现动画 + L3 闪光 | sfx_reveal | 7 | No |
| OnShowText | 无 | sfx_text_appear | 1 | No |

### InteractionFeedback MonoBehaviour

```csharp
public class InteractionFeedback : MonoBehaviour
{
    [SerializeField] private FeedbackMappings _mappings;

    private bool _feedbackSuppressed;
    private Dictionary<(string, string), float> _lastTriggerTime;
    private int _currentFeedbackPriority;

    void OnEnable()
    {
        // 10 InteractionManager events
        InteractionManager.OnHoverEnter += HandleHoverEnter;
        InteractionManager.OnHoverExit += HandleHoverExit;
        InteractionManager.OnInteract += HandleInteract;
        InteractionManager.OnDragStart += HandleDragStart;
        InteractionManager.OnDragComplete += HandleDragComplete;
        InteractionManager.OnDragCancel += HandleDragCancel;
        InteractionManager.OnChoiceSelected += HandleChoiceSelected;
        InteractionManager.OnChoiceHover += HandleChoiceHover;
        InteractionManager.OnRevealObject += HandleRevealObject;
        InteractionManager.OnShowText += HandleShowText;

        // 2 SceneManager transition events
        SceneManager.OnFragmentTransitionStarted += SuppressFeedback;
        SceneManager.OnFragmentTransitioned += RestoreFeedback;
    }

    void OnDisable()
    {
        InteractionManager.OnHoverEnter -= HandleHoverEnter;
        InteractionManager.OnHoverExit -= HandleHoverExit;
        InteractionManager.OnInteract -= HandleInteract;
        InteractionManager.OnDragStart -= HandleDragStart;
        InteractionManager.OnDragComplete -= HandleDragComplete;
        InteractionManager.OnDragCancel -= HandleDragCancel;
        InteractionManager.OnChoiceSelected -= HandleChoiceSelected;
        InteractionManager.OnChoiceHover -= HandleChoiceHover;
        InteractionManager.OnRevealObject -= HandleRevealObject;
        InteractionManager.OnShowText -= HandleShowText;

        SceneManager.OnFragmentTransitionStarted -= SuppressFeedback;
        SceneManager.OnFragmentTransitioned -= RestoreFeedback;
    }

    // No Update() — pure event-driven
}
```

### 优先级检查

```csharp
bool TryClaimFeedback(int priority)
{
    // If a higher-priority feedback is currently active, reject
    if (priority < _currentFeedbackPriority)
        return false;

    _currentFeedbackPriority = priority;
    return true;
}

// After visual feedback completes (MicroTween onComplete):
void ReleaseFeedback()
{
    _currentFeedbackPriority = 0; // Allow any priority
}
```

- 选择确认 (10) > 拖拽完成 (8) > 交互触发 (5-7) > 悬停 (2-3)
- 同类事件：新事件打断旧事件的视觉反馈——音频不中断（短音效播放完成）

### 防抖

```csharp
bool IsDebounced(string objectId, string eventName)
{
    var key = (objectId, eventName);
    float lastTime;
    if (_lastTriggerTime.TryGetValue(key, out lastTime))
    {
        if (Time.time - lastTime < 0.3f)
            return true; // Debounced
    }
    _lastTriggerTime[key] = Time.time;
    return false;
}
```

### 过渡抑制

```csharp
void SuppressFeedback(string fromFragmentId, string toFragmentId)
{
    _feedbackSuppressed = true;
}

void RestoreFeedback(string fragmentId)
{
    _feedbackSuppressed = false;
}

void HandleInteract(string objectId, string interactionType)
{
    if (_feedbackSuppressed) return; // Gate check — ALL handlers must start with this
    // ... handle feedback
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 视觉+音频反馈协调——MicroAnimationManager.PlayTriggered 和 AudioManager.PlaySFX 的实际调用（本 Story 定义映射表和处理函数骨架）
- 微动画 (#9): SetGlowLevel、PlayTriggered 实现
- 音频系统 (#3): PlaySFX 实现、音效资产加载
- 交互系统 (#11): 10 个 static events 的定义和触发
- 场景管理 (#6): OnFragmentTransitionStarted/Transitioned 事件定义

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Hover enter triggers L2 pulse — no audio
  - Given: InteractionFeedback initialized with mappings; OnHoverEnter("obj_01", "Touch") fires
  - When: Handler executes
  - Then: MicroAnimationManager.SetGlowLevel("obj_01", L2_Breathing) called; AudioManager.PlaySFX NOT called; same event within 300ms → SetGlowLevel NOT called again (debounced)
  - Edge cases: OnHoverExit before 300ms → resets to L1, debounce timer for OnHoverEnter resets

- **AC-2**: Interact triggers L3 flash + touch SFX — priority 5
  - Given: OnInteract("obj_01", "Touch") fires
  - When: Handler executes
  - Then: MicroAnimationManager.PlayTriggered("L3_flash") called; AudioManager.PlaySFX("sfx_touch_generic") called; feedback priority set to 5; 300ms debounce applied
  - Edge cases: OnChoiceSelected (priority 10) fires while OnInteract feedback active → OnInteract visual interrupted, audio continues to completion

- **AC-3**: Choice selected (priority 10) preempts lower-priority feedback
  - Given: OnDragComplete feedback (priority 8) executing; OnChoiceSelected fires in same frame
  - When: Both events processed
  - Then: TryClaimFeedback(10) succeeds; TryClaimFeedback(8) rejected; only OnChoiceSelected visual (L3_InnerGlow + ink color change) + audio (sfx_choice_confirm) execute
  - Edge cases: Same priority events → newer preempts older visual; audio continues

- **AC-4**: Transition suppresses all feedback
  - Given: OnFragmentTransitionStarted fired; _feedbackSuppressed = true
  - When: OnHoverEnter("obj_01") fires during transition
  - Then: HandleHoverEnter immediately returns at _feedbackSuppressed check; no micro-animation call; no audio call
  - Edge cases: OnFragmentTransitioned fires → _feedbackSuppressed = false → next event processes normally

- **AC-5**: Post-transition feedback resumes
  - Given: Transition complete; OnFragmentTransitioned fired; _feedbackSuppressed = false
  - When: OnInteract fires
  - Then: Feedback executes normally (visual + audio); no leakage from suppressed events during transition
  - Edge cases: Rapid transition start/stop (double transition) → suppressed during both, resumes only after final OnFragmentTransitioned

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/interaction-feedback/event_subscription_feedback_test.cs` — must exist and pass

**Status**: [x] Created — tests/unit/interaction-feedback/event_subscription_feedback_test.cs (471 lines)

---

## Dependencies

- Depends on: 交互系统 Story 002 (10 static events defined); 场景管理 Story 001 (OnFragmentTransitionStarted/Transitioned events); ADR-0014 (FeedbackMappings SO structure)
- Unlocks: Story 002 (visual+audio coordination implementation)

---

## Completion Notes

**Completed**: 2026-05-19
**Criteria**: 5/5 auto-verified
**Deviations**: None
**Test Evidence**: tests/unit/interaction-feedback/event_subscription_feedback_test.cs — exists
**Files created**:
- src/core/FeedbackMappings.cs — FeedbackMappings SO + FeedbackMapping class
- src/core/InteractionFeedback.cs — 10 event handlers + 2 transition handlers + priority + debounce + testability stubs
**Next**: /story-done story-001
