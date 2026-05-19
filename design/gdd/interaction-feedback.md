# 交互反馈系统 (Interaction Feedback)

> **Status**: Designed (pending review)
> **Author**: 用户 + ui-programmer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 4 (画卷中有呼吸) — 直接支撑——反馈是画卷"活着"的感觉

## Overview

交互反馈系统是《回响》中玩家操作的"应答层"。当玩家触碰画卷中的物件——光标移上去、点下去、拖开去——交互反馈系统负责让这个触碰被"感觉到"：物件微微发光、墨点在触碰处短暂晕开、一声轻微的纸页摩挲声在耳边响起。它不检测触碰（那是交互系统 #11 的职责），不渲染动画（那是微动画 #9 的职责），不播放音频（那是音频系统 #3 的职责）——它协调这些系统，在每次交互事件的时机触发正确的视觉+听觉反馈。

在技术层面，它是一个轻量的事件监听器：订阅交互系统 (#11) 的交互事件（OnHoverEnter、OnInteract、OnDragStart、OnDragComplete、OnChoiceSelected），对每个事件调用微动画 (#9) 的触发方法和音频系统 (#3) 的音效播放。无状态——纯事件→反馈映射。

## Player Fantasy

玩家不觉得"反馈系统"存在——他们只觉得这幅画卷是活的。手指触到画面时，墨迹在触碰处微微洇开。拖开一层画面时，纸页发出轻微的叹息。做出选择时，一声确定而不可撤销的"嗒"——像毛笔搁回砚台——在耳边确认：你的手指在这幅画上留下了不可磨灭的痕迹。反馈不是"通知"——反馈是画卷在你指尖下的呼吸。

## Detailed Design

### Core Rules

**规则 1 — 事件→反馈映射表**

| 交互事件 (来源: #11) | 视觉反馈 (调用: #9) | 音频反馈 (调用: #3) | 时机 |
|---------------------|-------------------|-------------------|------|
| OnHoverEnter(obj) | L1→L2 脉动 (物件墨点) | 无 (悬停无声——不干扰氛围) | 光标移到物件上 |
| OnHoverExit(obj) | L2→L1 回退 | 无 | 光标离开物件 |
| OnInteract(Touch) | L2→L3 内光闪烁 (0.3s) | `sfx_touch_generic` (纸页轻触声) | 玩家点击物件 |
| OnInteract(Drag start) | 物件跟随鼠标 + 拖痕出现 | `sfx_drag_start` (纸页掀动声) | 玩家开始拖拽 |
| OnDragComplete | L3 内光闪烁 + 物件停驻 | `sfx_drag_complete` (纸页落下声) | 拖拽超过阈值 |
| OnDragCancel | 物件弹回原位 (spring-back 动画) | `sfx_drag_cancel` (轻柔回弹) | 拖拽不足阈值释放 |
| OnChoiceSelected(choiceId) | 墨点 L3 内光 → 墨点从朱砂红变为深墨色 (已选) | `sfx_choice_confirm` (毛笔搁砚声) | 玩家确认选择 |
| OnChoiceHover(choiceId) | L1→L2 脉动 (选项墨点) | `sfx_hover_tick` (极轻的滴答) | 光标悬停在选项上 |
| OnRevealObject | 物件出现动画 + L3 闪光 | `sfx_reveal` (纸页展开声) | 隐藏物件被揭示 |
| OnShowText | 无 (文本自身是反馈) | `sfx_text_appear` (轻微墨笔声) | 文本浮层出现 |

**规则 2 — 反馈优先级**

若多个反馈事件同时发生（如快速连续点击），优先级规则：
1. 选择确认 > 拖拽完成 > 交互触发 > 悬停
2. 同类事件：新事件打断旧事件的视觉反馈（但音频不中断——短音效播放完成）
3. 过渡期间 (FadeOut/FadeIn): 所有反馈事件被抑制——不播放音频，不触发微动画

**规则 3 — 防抖**

同一物件的 OnInteract 在 0.3s 内不重复触发视觉反馈（交互系统 #11 的防抖已阻止重复交互——本系统的防抖是兜底保护）。防止双击闪烁。

**规则 4 — 实现方式**

```csharp
public class InteractionFeedback : MonoBehaviour
{
    void OnEnable()
    {
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
        SceneManager.OnFragmentTransitionStarted += SuppressFeedback;
        SceneManager.OnFragmentTransitioned += RestoreFeedback;
    }
}
```

无 Update()——纯事件驱动。无内部状态（除 `_feedbackSuppressed: bool`）。

**规则 5 — MVP 音频资产**

MVP 需要 7 个短音效 (<1s 每个)，由音频系统 (#3) 管理：

| Audio Key | 描述 | 时长 |
|-----------|------|------|
| `sfx_touch_generic` | 指尖轻触宣纸 | ~0.2s |
| `sfx_drag_start` | 纸页掀动——轻微纤维拉伸 | ~0.3s |
| `sfx_drag_complete` | 纸页落下——柔软而确定 | ~0.3s |
| `sfx_drag_cancel` | 纸页弹回——快速柔软 | ~0.2s |
| `sfx_choice_confirm` | 毛笔轻搁砚台——"嗒" | ~0.4s |
| `sfx_hover_tick` | 极轻的墨滴滴落 | ~0.1s |
| `sfx_reveal` | 旧纸页展开——纤维呼吸 | ~0.5s |
| `sfx_text_appear` | 毛笔在纸上写第一笔 | ~0.2s |

### States and Transitions

无状态机。唯一状态标志: `_feedbackSuppressed: bool`——过渡期间抑制所有反馈。

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **交互系统 (#11)** | OnHoverEnter/Exit, OnInteract, OnDragStart/Complete/Cancel, OnChoiceSelected/Hover, OnRevealObject, OnShowText | 所有交互事件的来源 |
| 上游 | **场景管理 (#6)** | OnFragmentTransitionStarted, OnFragmentTransitioned | 过渡期间抑制反馈 |
| 下游 | **微动画 (#9)** | PlayTriggered(animationId), L2/L3 发光控制 | 视觉反馈的执行 |
| 下游 | **音频系统 (#3)** | PlaySFX(audioKey) | 音频反馈的执行 |

## Formulas

本系统不含公式。反馈映射是 1:1 的事件→响应表——见规则 1。

## Edge Cases

- **快速连续点击同一物件 (双击)**: 交互系统防抖阻止第二次 OnInteract——本系统只收到一次事件。不需要额外保护。
- **过渡期间悬停在物件上**: OnHoverEnter 可能恰好在 FadeOut 开始时触发 → `_feedbackSuppressed = true` → 微动画和音频均不触发。
- **音频加载失败**: sfx 加载失败 (Addressables 错误) → PlaySFX 静默降级——不播放但也不报错。视觉反馈不受影响。
- **拖拽过程中碎片切换**: 交互系统规则 5 阻断拖拽中的碎片切换——此场景不会发生。

## Dependencies

### 硬依赖

| 系统 | 性质 | 接口 |
|------|------|------|
| **交互系统 (#11)** | 硬依赖 — 所有交互事件的来源 | 9 个交互事件 |
| **微动画 (#9)** | 硬依赖 — 视觉反馈的执行 | PlayTriggered, L2/L3 控制 |
| **音频系统 (#3)** | 硬依赖 — 音频反馈的执行 | PlaySFX(audioKey) |

### 软依赖

| 系统 | 性质 | 接口 |
|------|------|------|
| **场景管理 (#6)** | 软依赖 — 过渡抑制 | OnFragmentTransitionStarted/Transitioned |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 交互反馈防抖间隔 | 0.3s | 0.2–0.5s | 同一物件点击反馈的最小间隔 |
| sfx 音量比 | 0.7 | 0.3–1.0 | 反馈音效相对于主音量的比例 (SFXVolume × sfxVolumeRatio) |

## Visual/Audio Requirements

本系统不产生自有视觉/音频资产。视觉反馈委托微动画 (#9)，音频反馈委托音频系统 (#3)。音效资产列表见规则 5——由音频总监制作。

## UI Requirements

无自有 UI。所有视觉反馈在画面层渲染（微动画 L1-L3），不走 UI Document。

## Acceptance Criteria

- **GIVEN** 玩家光标移到物件上 (OnHoverEnter)，**WHEN** 反馈系统处理事件，**THEN** 微动画 #9 的 L1→L2 脉动启动。无音频播放。光标样式变为手型。

- **GIVEN** 玩家点击物件 (OnInteract, Touch)，**WHEN** 反馈系统处理事件，**THEN** 微动画 L2→L3 内光闪烁 (0.3s) + `sfx_touch_generic` 播放。0.3s 内再次点击同一物件不重复触发视觉反馈。

- **GIVEN** 玩家拖拽物件超过阈值 (OnDragComplete)，**WHEN** 反馈系统处理事件，**THEN** L3 内光闪烁 + `sfx_drag_complete` 播放。物件停在最终位置。

- **GIVEN** 玩家在选择面板中确认选择 (OnChoiceSelected)，**WHEN** 反馈系统处理事件，**THEN** 选项墨点从朱砂红变为深墨色 + `sfx_choice_confirm` 播放。

- **GIVEN** 碎片过渡进行中 (FadeOut/FadeIn)，**WHEN** 任何交互事件触发，**THEN** `_feedbackSuppressed = true` → 视觉和音频反馈均不触发。

## Open Questions

- **sfx 资产的制作优先级**: MVP 需要 7 个短音效。是否可从免费音效库中选取临时资产进行 playtest，正式资产在 Vertical Slice 中录制？建议 MVP 用临时音效——核心循环的反馈手感是关键，但不需要最终音质。(Owner: audio-director)
