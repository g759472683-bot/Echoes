# Story 002: SceneFader 墨韵过渡效果

> **Epic**: 场景管理系统 (SceneManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Visual/Feel
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/scene-management.md`
**Requirement**: `TR-scene-management-002`, `TR-scene-management-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: 场景管理与转场状态机
**ADR Decision Summary**: 统一使用 SceneFader 全屏墨迹遮罩（纯黑 VisualElement）+ UI Toolkit opacity transition 实现所有过渡效果

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: UI Toolkit `transition` 属性在运行时支持 opacity/transform——GPU 加速；颜色/尺寸过渡需代码驱动 tween

**Control Manifest Rules (Foundation Layer)**:
- Required: Unified ink-fade transition — SceneFader VisualElement with opacity transition for all transitions
- Required: USS transition only for opacity/transform — GPU-accelerated properties only; color/size changes use code-driven tweens
- Guardrail: Fragment transition: 0.5s fade-out + 0.5s fade-in = 1.0s total

---

## Acceptance Criteria

*From GDD `design/gdd/scene-management.md`, scoped to this story:*

- [ ] SceneFader 是一个持久存在于 Game 场景中的全屏 `VisualElement`，层级在所有内容之上 (`picking-mode: ignore`)
- [ ] 遮罩颜色为纯黑 `rgb(0,0,0)`——模拟墨色晕开
- [ ] 过渡动画使用 UI Toolkit 的 `transition` 属性（`transition-property: opacity`），不编写协程动画
- [ ] FragmentTransition: opacity 0→1 (0.5s 淡出) + opacity 1→0 (0.5s 淡入) = 1.0s 总时长
- [ ] ChapterTransition: opacity 0→1 (1.0s 淡出) + 保持遮罩覆盖 (2-4s 加载) + opacity 1→0 (1.0s 淡入)
- [ ] SceneTransition: 与 ChapterTransition 相同的遮罩淡入淡出模式
- [ ] `SceneFader.FadeOut(float duration)` 和 `SceneFader.FadeIn(float duration)` 返回 `Task`——调用方 await 动画完成

---

## Implementation Notes

*Derived from ADR-0004:*

SceneFader 结构:
```csharp
public class SceneFader : VisualElement
{
    public SceneFader()
    {
        style.position = Position.Absolute;
        style.top = 0;
        style.left = 0;
        style.width = Length.Percent(100);
        style.height = Length.Percent(100);
        style.backgroundColor = new Color(0, 0, 0, 0); // 初始透明
        style.opacity = 0;
        pickingMode = PickingMode.Ignore;
        
        // USS transition 属性在 Theme.uss 中定义:
        // .scene-fader { transition-property: opacity; transition-duration: 0.5s; }
    }
    
    public async Task FadeOut(float duration)
    {
        // 动态设置 transition-duration，opacity → 1
        style.transitionDuration = new List<TimeValue> { new TimeValue(duration, TimeUnit.Second) };
        style.opacity = 1;
        await Task.Delay((int)(duration * 1000));
    }
    
    public async Task FadeIn(float duration)
    {
        style.transitionDuration = new List<TimeValue> { new TimeValue(duration, TimeUnit.Second) };
        style.opacity = 0;
        await Task.Delay((int)(duration * 1000));
    }
}
```

USS 定义 (Theme.uss):
```css
.scene-fader {
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-color: rgb(0, 0, 0);
    opacity: 0;
    transition-property: opacity;
    transition-duration: 0.5s;
    transition-timing-function: ease-in-out;
}
```

SceneFader 挂载在 Game 场景的 UIDocument 根节点下——场景加载时自动存在于视图中。遮罩 `picking-mode: ignore` 确保不阻挡鼠标事件——过渡期间输入由 Action Map 切换管理（Story 003）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 3 场景架构 — SceneFader 的创建和生命周期
- Story 003: 碎片过渡引擎 — TransitionToFragmentAsync 中的 SceneFader 调用
- Story 004: 章节过渡 — TransitionToChapterAsync 中的 SceneFader 调用
- 加载动画/进度条 — GDD 明确规定不需要

---

## QA Test Cases

*Manual verification steps for Visual/Feel story:*

- **AC-1**: SceneFader 视觉覆盖
  - Setup: 进入 Play Mode，进入 Game 场景
  - Verify: SceneFader 存在于 UI Toolkit 层级中；初始 opacity = 0（透明）；`picking-mode: ignore` 不阻挡鼠标
  - Pass condition: 在 UI Toolkit Debugger 中可见 SceneFader 节点，初始不可见

- **AC-2**: FragmentTransition 动画时序
  - Setup: 触发碎片切换
  - Verify: 遮罩从透明渐变为黑色（0.5s），然后从黑色渐变为透明（0.5s）
  - Pass condition: 视觉检查——过渡平滑、无闪烁、无跳帧；总时长约 1.0s

- **AC-3**: ChapterTransition 保持遮罩
  - Setup: 触发章节切换
  - Verify: 遮罩淡出（1.0s）→ 保持黑色覆盖（2-4s 加载中）→ 遮罩淡入（1.0s）
  - Pass condition: 加载期间遮罩始终覆盖——内容切换不可见

- **AC-4**: USS transition 属性驱动
  - Setup: 检查 SceneFader 实现
  - Verify: opacity 过渡通过 USS `transition-property: opacity` 实现——不在 C# 中使用协程或逐帧修改 opacity
  - Pass condition: 代码中无 `while`/`yield return` 逐帧修改 opacity

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: `production/qa/evidence/scene-fader-evidence.md` — screenshot + lead sign-off

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (需要 Game 场景 + UIDocument 根节点)
- Unlocks: Story 003 (碎片过渡), Story 004 (章节过渡)

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 5/7 passing (AC-4/AC-5 DEFERRED to Story 003/004)
**Deviations**:
- ADVISORY: Theme.uss loaded via PanelSettings (Editor config), not Resources.Load — complies with ADR-0002
- ADVISORY: `FindObjectOfType<UIDocument>()` in MountSceneFaderToGameScene — once per scene load, acceptable
- ADVISORY: `_sceneFader` null during Boot→MainMenu/Game transitions — known limitation, SceneFader only after Game scene mount
- ADVISORY: AC-4/AC-5 orchestration deferred to Story 003/004 — SceneFader class supports required capability
**Test Evidence**: Visual/Feel — `production/qa/evidence/scene-fader-evidence.md` (manual sign-off pending)
**Code Review**: Complete — APPROVED (2026-05-17)
