# Story 003: 文本浮层 + 章节进度 + 交互提示

> **Epic**: 游戏内HUD (InGameHUD)
> **Status**: Complete
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/in-game-hud.md`
**Requirement**: `TR-in-game-hud-004`, `TR-in-game-hud-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架 + ADR-0001: 事件总线
**ADR Decision Summary**: 文本浮层（#fragment-text-overlay）picking-mode:ignore 不阻挡交互，4s 自动淡出。章节进度（#chapter-progress）——水平墨点（实心朱砂=已访问，空心=未访问，脉动=当前），OnFragmentChanged 触发更新。交互提示（#interaction-hint）——悬停物件名 + 操作提示，延迟 0.5s 出现。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: UI Toolkit Label picking-mode:ignore 通过 `pickingMode = PickingMode.Ignore` 设置。USS transition 的 fade-out 动画使用 opacity transition（GPU 加速）。

**Control Manifest Rules (Feature Layer)**:
- Required: UI Toolkit for all runtime UI — source: ADR-0006
- Required: static event Action<T> for cross-system communication — source: ADR-0001

---

## Acceptance Criteria

*From GDD `design/gdd/in-game-hud.md`, scoped to this story:*

- [ ] GIVEN 交互系统调用 ShowFragmentText("一封泛黄的信...")，WHEN HUD 展示文本浮层，THEN 手写体文字出现在画面指定位置 (screenPosition 参数)。文字 picking-mode:ignore——不阻挡鼠标事件。4 秒后自动淡出（opacity 1→0 over 0.5s CSS transition）。点击任意位置提前关闭。

- [ ] GIVEN 玩家从碎片 A 切换到碎片 B (OnFragmentChanged 触发)，WHEN HUD 更新章节进度，THEN 底部墨点中对应碎片 B 的墨点变为实心朱砂色（#C04040），碎片 A 的墨点保持实心。当前碎片墨点有 L2 脉动（微动画 #9 驱动）。

- [ ] GIVEN 碎片过渡中 (FadeOut/FadeIn)，WHEN SceneFader 遮罩覆盖，THEN HUD 完全隐藏——文本浮层自动关闭，选择面板、关联路径、进度指示全部不可见。

- [ ] GIVEN 光标悬停在一个可交互物件上（OnHoverEnter 触发），WHEN 0.5s 延迟后，THEN .interaction-hint 显示物件名（手写体小字，出现在光标上方 20px）。Hover 类型物件显示物件名 + "..."。

---

## Implementation Notes

*Derived from GDD rules 4–6 + ADR-0001:*

### 文本浮层 (Fragment Text Overlay)

```csharp
void ShowFragmentText(string localizedText, Vector2 screenPosition)
{
    var overlay = _uiDocument.rootVisualElement.Q("#fragment-text-overlay");
    var label = overlay.Q<Label>();
    label.text = localizedText;
    label.pickingMode = PickingMode.Ignore; // 不阻挡鼠标事件
    overlay.style.left = screenPosition.x;
    overlay.style.top = screenPosition.y;
    overlay.style.opacity = 1f;
    overlay.visible = true;

    // 4s auto-fade via USS transition
    _textOverlayTimer = 4.0f;
    _fadeOutRequested = false;
}

void Update() // 或通过 IVisualElementScheduledItem schedule
{
    if (_textOverlayTimer > 0)
    {
        _textOverlayTimer -= Time.deltaTime;
        if (_textOverlayTimer <= 0.5f && !_fadeOutRequested)
        {
            _fadeOutRequested = true;
            // Apply CSS class .fade-out (opacity 1→0 over 0.5s)
            _textOverlay.AddToClassList("fade-out");
        }
        if (_textOverlayTimer <= 0)
        {
            _textOverlay.visible = false;
            _textOverlay.RemoveFromClassList("fade-out");
        }
    }
}
```

- 字体: 手写体中文字体 (TextMeshPro)
- 半透明墨色，无背景框
- 点击任意位置提前关闭——注册 rootVisualElement 的 ClickEvent

### 章节进度 (Chapter Progress)

```csharp
void UpdateChapterProgress(string chapterKey, int visitedCount, int totalFragments)
{
    var container = _uiDocument.rootVisualElement.Q("#chapter-progress");
    var chapterLabel = container.Q<Label>("#chapter-name");
    chapterLabel.text = LocalizationManager.GetLocalizedString(
        ChapterDefinition.GetDisplayNameKey(chapterKey));

    var dotContainer = container.Q("#fragment-count");
    dotContainer.Clear();

    for (int i = 0; i < totalFragments; i++)
    {
        var dot = new VisualElement();
        dot.AddToClassList("chapter-dot");
        if (i < visitedCount)
            dot.AddToClassList("dot-visited");     // 实心朱砂 #C04040
        else
            dot.AddToClassList("dot-unvisited");   // 空心淡墨圈

        dotContainer.Add(dot);
    }

    // 当前碎片墨点: L2 脉动 (微动画 #9)
    if (visitedCount > 0 && visitedCount <= totalFragments)
    {
        var currentDot = dotContainer[visitedCount - 1];
        currentDot.AddToClassList("dot-current"); // L2 脉动样式
    }
}
```

- MVP 极简版：水平一行墨点，每个点 6px，间距 4px
- 不显示数字/百分比
- OnFragmentChanged 事件触发时更新（ADR-0001 static event 订阅）

### 交互提示 (Interaction Hint)

```csharp
void OnEnable()
{
    InteractionManager.OnHoverEnter += HandleHoverEnter;
    InteractionManager.OnHoverExit += HandleHoverExit;
}

void HandleHoverEnter(InteractiveObject obj, InteractionType type)
{
    _hoverTimer = 0.5f; // 延迟 0.5s 后显示
    _currentHoverObj = obj;
    _currentInteractionType = type;
}

void HandleHoverExit(InteractiveObject obj)
{
    _hoverTimer = 0;
    HideInteractionHint();
}
```

交互类型→提示文本映射:

| 交互类型 | 提示文本 |
|---------|---------|
| Touch | 物件名 |
| Drag | "拖拽" + 物件名 |
| Hover | 物件名 + "..." |
| Examine | "细看" + 物件名 |

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: HUD 根 VisualElement 树、InGameHUD MonoBehaviour 骨架
- Story 002: 关联路径可视化
- Story 004: MVVM 数据绑定、HUD 可见性规则表
- 微动画 (#9): L2 脉动实现（墨点脉动——HUD 只添加 CSS 类，微动画提供动画逻辑）
- 交互系统 (#11): OnHoverEnter/OnHoverExit 事件定义
- 本地化 (#4): 物件名的 TableReference → 本地化文本

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Fragment text overlay display and auto-fade
  - Setup: HUD initialized; ShowFragmentText called with text and screenPosition (600, 400)
  - Verify: Text appears at (600, 400); pickingMode = Ignore; after 4s text fades out (or click dismisses immediately)
  - Pass condition: Text displays, auto-fades in 4s, click dismiss works, no mouse event blocking

- **AC-2**: Chapter progress updates on fragment change
  - Setup: Chapter has 8 fragments; visited 3 so far; OnFragmentChanged fires for fragment index 4
  - Verify: 8 horizontal dots rendered; first 4 dots solid vermilion (#C04040); dots 5-8 hollow; 4th dot has "dot-current" class; chapter name localized
  - Pass condition: Progress bar correctly reflects visited count and current fragment

- **AC-3**: HUD hides during fragment transition
  - Setup: HUD visible with text overlay active; SceneFader.OnFragmentTransitionStarted fires
  - Verify: #fragment-text-overlay closes; whole HUD hidden; no visual elements overlaying the fade
  - Pass condition: HUD fully invisible during FadeOut/FadeIn phase

- **AC-4**: Interaction hint appears after 0.5s hover delay
  - Setup: OnHoverEnter fired with Touch-type InteractiveObject named "泛黄的信封"
  - Verify: After exactly 0.5s, hint text "泛黄的信封" appears at cursor position + 20px above; hand-drawn font; OnHoverExit hides immediately
  - Pass condition: Delayed appear, correct text, positioned correctly, instant dismiss on exit

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/text-overlay-progress-hints-evidence.md` — manual walkthrough doc or interaction test

**Status**: [x] Created — production/qa/evidence/text-overlay-progress-hints-evidence.md

---

## Dependencies

- Depends on: Story 001 (HUD root VisualElement tree + InGameHUD MonoBehaviour); 交互系统 Story 002 (OnHoverEnter/OnHoverExit events); 章节管理 Story 001 (OnFragmentChanged event)
- Unlocks: Story 004 (MVVM binding wires these elements to data sources)

---

## Completion Notes

- **Completed**: 2026-05-19
- **Implementation**: InGameHUD.cs (ShowFragmentText, UpdateChapterProgress, ShowInteractionHint, HideInteractionHint, HandleTextOverlayTimer, HandleHoverTimer, DismissTextOverlay)
- **UXML**: assets/uxml/in-game-hud.uxml (#fragment-text-overlay, #chapter-progress, #interaction-hint)
- **USS**: assets/uss/in-game-hud.uss (.fragment-text-overlay, .fragment-text, .chapter-progress, .chapter-dot, .dot-visited, .dot-unvisited, .dot-current, .interaction-hint, .hint-text)
- **Evidence**: production/qa/evidence/text-overlay-progress-hints-evidence.md
- **Deviations**: Hover hint shows object name directly from OnHoverEnter(string objectId) event — InteractionType not available from event signature, so MVP defaults to Touch-style hint text (object name only). Full type-aware hint text will require event signature change or additional object lookup in vertical slice.
- **Blockers**: None
