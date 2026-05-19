# Story 003: 碎片过渡引擎 + 事件

> **Epic**: 场景管理系统 (SceneManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/scene-management.md`
**Requirement**: `TR-scene-management-003`, `TR-scene-management-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: 场景管理与转场状态机, ADR-0001: 事件总线架构
**ADR Decision Summary**: FragmentTransition 状态机 + OnFragmentTransitionStarted/Transitioned static events + 过渡期间 Action Map Inactive 输入屏蔽

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `Addressables.LoadAssetAsync` 在 IL2CPP 中的 Task 行为需验证

**Control Manifest Rules (Foundation Layer)**:
- Required: OnFragmentTransitionStarted BEFORE fade-out — suppress interaction feedback before transition begins
- Required: OnFragmentTransitioned AFTER fade-in — resume interaction after transition completes
- Required: `static event Action<T>` for all cross-system communication
- Guardrail: Fragment transition: 0.5s fade-out + 0.5s fade-in = 1.0s total

---

## Acceptance Criteria

*From GDD `design/gdd/scene-management.md`, scoped to this story:*

- [ ] GIVEN 玩家在章节 1 碎片 5，WHEN 触发切换到碎片 6，THEN 遮罩淡出（0.5s）→ 内容切换 → 遮罩淡入（0.5s）。过渡期间输入被屏蔽
- [ ] `TransitionToFragmentAsync(string chapterKey, string fragmentId)` 返回 `Task`——Await 完成时碎片已就绪
- [ ] 过渡 5 步流程严格按序执行: OnFragmentTransitionStarted → FadeOut → 卸载旧内容 → 加载新内容 → FadeIn → OnFragmentTransitioned
- [ ] 过渡进行中时忽略新的 `TransitionToFragmentAsync` 调用——防止双击竞态条件
- [ ] 过渡期间所有 Action Map 进入 Inactive 状态——输入系统不处理任何输入
- [ ] `OnFragmentTransitionStarted(string chapterKey, string fragmentId)` 和 `OnFragmentTransitioned(string chapterKey, string fragmentId)` static events 正确声明和触发

---

## Implementation Notes

*Derived from ADR-0004 + ADR-0001:*

TransitionToFragmentAsync 完整流程:
```csharp
public async Task TransitionToFragmentAsync(string chapterKey, string fragmentId)
{
    if (_currentState != TransitionState.Idle)
        return; // 防止并发过渡
    
    _currentState = TransitionState.FadingOut;
    
    // Step 0: 通知所有系统过渡即将开始
    OnFragmentTransitionStarted?.Invoke(chapterKey, fragmentId);
    
    // Step 1: 遮罩淡出
    await _sceneFader.FadeOut(0.5f);
    _currentState = TransitionState.Loading;
    
    // Step 2: 卸载当前碎片 Sprite + 音频
    UnloadCurrentFragment();
    
    // Step 3: 加载目标碎片
    var fragment = await _dataManager.GetFragmentAsync(chapterKey, fragmentId);
    var sprite = await _dataManager.GetIllustrationAsync(fragment.IllustrationKey);
    await _audioManager.PreloadFragmentAudioAsync(fragment.AudioKeys);
    
    // 更新场景
    _spriteRenderer.sprite = sprite;
    UpdateInteractiveObjects(fragment.InteractiveObjects);
    
    // Step 4: 遮罩淡入
    _currentState = TransitionState.FadingIn;
    await _sceneFader.FadeIn(0.5f);
    
    // Step 5: 通知过渡完成
    _currentState = TransitionState.Idle;
    OnFragmentTransitioned?.Invoke(chapterKey, fragmentId);
}
```

事件声明 (遵循 ADR-0001):
```csharp
public static event Action<string, string> OnFragmentTransitionStarted;
public static event Action<string, string> OnFragmentTransitioned;
```

输入屏蔽: 过渡期间 SceneManager 调用 `InputManager.SetActionMap(ActionMap.Inactive)` ——输入系统确保不处理任何交互输入。过渡完成后恢复为 Gameplay Action Map。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: SceneFader 视觉组件 — FadeOut/FadeIn 具体实现
- Story 004: 章节过渡 — TransitionToChapterAsync
- Story 005: 错误恢复 — 加载失败处理
- 碎片内容的视觉布局 — 由各 UI 系统负责

---

## QA Test Cases

- **AC-1**: 碎片过渡动画时序
  - Given: 当前显示碎片 frag_05
  - When: 调用 `TransitionToFragmentAsync("ch1", "frag_06")`
  - Then: 遮罩淡出 (0.5s) → SpriteRenderer.sprite 更新为 frag_06 的插图 → 遮罩淡入 (0.5s) → 总时长约 1.0s
  - Edge cases: 目标碎片与当前相同 → 忽略（不触发过渡）；插图未缓存 → 遮罩保持覆盖直到加载完成

- **AC-2**: 过渡中阻止并发调用
  - Given: `TransitionToFragmentAsync` 正在进行中（状态 = FadingOut）
  - When: 再次调用 `TransitionToFragmentAsync("ch1", "frag_07")`
  - Then: 第二次调用被忽略（返回已完成 Task 或 null）；只有第一次过渡完成
  - Edge cases: 过渡刚完成（状态 = Idle）→ 可以接受新请求

- **AC-3**: 过渡期间输入屏蔽
  - Given: 碎片过渡中（FadingOut/Loading/FadingIn）
  - When: 玩家按键/移动鼠标
  - Then: 所有 Action Map 为 Inactive——不产生任何交互事件
  - Edge cases: 过渡完成后 Action Map 恢复为 Gameplay——玩家立即可以交互

- **AC-4**: OnFragmentTransitionStarted/Transitioned 事件触发
  - Given: 3 个系统订阅了 OnFragmentTransitionStarted (Interaction Feedback #18, Chapter Manager #15, HUD #17)
  - When: 调用 `TransitionToFragmentAsync`
  - Then: 过渡开始前所有订阅者收到 OnFragmentTransitionStarted 通知；过渡完成后所有订阅者收到 OnFragmentTransitioned 通知
  - Edge cases: 无订阅者 → `?.Invoke` 安全跳过

- **AC-5**: 每次过渡仅触发一次事件
  - Given: 一次碎片切换
  - When: 跟踪事件触发次数
  - Then: OnFragmentTransitionStarted 触发 1 次；OnFragmentTransitioned 触发 1 次
  - Edge cases: 过渡中发生错误（Story 005）→ OnFragmentTransitioned 不触发

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/scene-management/fragment_transition_test.cs` — must exist and pass

**Status**: [x] Created — `tests/unit/scene-management/fragment_transition_test.cs` (17 tests, all pass)

---

## Dependencies

- Depends on: Story 002 (SceneFader FadeOut/FadeIn)
- Unlocks: Story 004 (章节过渡), Story 005 (错误恢复)

---

## Completion Notes

**Completed**: 2026-05-13
**Criteria**: 6/6 passing
**Deviations**:
- Class renamed `SceneManager` → `GameSceneManager` (avoids `UnityEngine.SceneManagement.SceneManager` collision — code review finding)
- Implemented with incomplete dependency (Story 002 SceneFader — Ready, not Complete; decoupled via ISceneFader interface)
- Basic load failure recovery added (try/catch + state reset) — minimal defensive subset of Story 005 scope
**Test Evidence**: `tests/unit/scene-management/fragment_transition_test.cs` — 17 test methods covering all 6 ACs
**Code Review**: Complete — 2 CRITICAL + 1 HIGH + 2 BLOCKING issues fixed
