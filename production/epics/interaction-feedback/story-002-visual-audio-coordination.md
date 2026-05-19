# Story 002: 视觉+音频反馈协调（MicroAnimationManager + AudioManager）

> **Epic**: 交互反馈系统 (InteractionFeedback)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/interaction-feedback.md`
**Requirement**: `TR-interaction-feedback-001` (full 10 events), `TR-interaction-feedback-002` (full mapping)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0014: 交互反馈映射表 + ADR-0012: 微动画系统 + ADR-0013: 音频系统
**ADR Decision Summary**: 在 Story 001 的事件订阅和映射表基础上，实现完整的视觉+音频协调层。每个事件处理函数调用 MicroAnimationManager（SetGlowLevel/PlayTriggered/PlayFeedback）和 AudioManager（PlaySFX）。音频加载失败时静默降级——视觉反馈不受影响。8 个 MVP 音效资产通过 Addressables 加载。优先级抢占时：视觉中断，音频不中断（短音效播放完成）。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: AudioManager.PlaySFX 是 async void（fire-and-forget）。MicroAnimationManager 调用是同步的（CPU 侧写入 MPB）。Addressables 加载音效资产的异步模式——首次播放可能有延迟（~50ms）。

**Control Manifest Rules (Presentation Layer)**:
- Required: MicroTween value type (zero GC) for visual feedback timing — source: ADR-0012
- Required: 10-source SFX priority pool — source: ADR-0013
- Required: Volume via Exposed Parameters + PlayerPrefs — source: ADR-0013

---

## Acceptance Criteria

*From GDD `design/gdd/interaction-feedback.md`, scoped to this story:*

- [ ] GIVEN 玩家点击物件 (OnInteract)，WHEN 反馈系统处理，THEN MicroAnimationManager.PlayTriggered("L3_flash") 触发 0.3s 内光闪烁 → AudioManager.PlaySFX("sfx_touch_generic") 播放 0.2s 纸页轻触声。视觉和音频同时开始。音频播放完成不可中断。

- [ ] GIVEN 玩家拖拽物件超过阈值 (OnDragComplete)，WHEN 反馈处理，THEN L3 内光闪烁 (MicroAnimationManager.PlayTriggered) + sfx_drag_complete (0.3s 纸页落下声) 同时触发。物件停在最终位置。

- [ ] GIVEN 玩家在选择面板确认选择 (OnChoiceSelected)，WHEN 反馈处理，THEN MicroAnimationManager.SetGlowLevel(objectId, L3_InnerGlow) → 墨点从朱砂红变为深墨色 (0.4s tween) → AudioManager.PlaySFX("sfx_choice_confirm") 播放毛笔搁砚声。优先级 10——抢占任何正在执行的视觉反馈。

- [ ] GIVEN 音效资产加载失败（Addressables 错误），WHEN OnInteract 触发，THEN PlaySFX 静默降级——不播放音效但不报错、不抛异常。视觉反馈（SetGlowLevel/PlayTriggered）不受影响——正常执行。

- [ ] GIVEN 过渡期间 (FadeOut/FadeIn) 任何交互事件触发，WHEN _feedbackSuppressed = true，THEN 微动画和音频均不触发——立即 return。过渡完成后正常恢复。

---

## Implementation Notes

*Derived from ADR-0014 + ADR-0012 + ADR-0013:*

### 完整事件处理函数实现

```csharp
// OnHoverEnter → L2 breathing pulse, no audio
void HandleHoverEnter(string objectId, string interactionType)
{
    if (_feedbackSuppressed) return;
    if (IsDebounced(objectId, "OnHoverEnter")) return;
    if (!TryClaimFeedback(2)) return;

    MicroAnimationManager.Instance.SetGlowLevel(objectId, GlowLevel.L2_Breathing);
}

// OnHoverExit → L2→L1 fallback
void HandleHoverExit(string objectId, string interactionType)
{
    if (_feedbackSuppressed) return;
    // No priority check — hover exit always runs to clean up state

    MicroAnimationManager.Instance.SetGlowLevel(objectId, GlowLevel.L1_Static);
    ReleaseFeedback();
}

// OnInteract (Touch) → L3 flash + touch SFX
void HandleInteract(string objectId, string interactionType)
{
    if (_feedbackSuppressed) return;
    if (IsDebounced(objectId, "OnInteract")) return;
    if (!TryClaimFeedback(5)) return;

    MicroAnimationManager.Instance.PlayTriggered("L3_flash", objectId, 0.3f,
        onComplete: () => ReleaseFeedback());
    AudioManager.Instance.PlaySFX("sfx_touch_generic"); // fire-and-forget
}

// OnDragStart → drag trail + drag start SFX
void HandleDragStart(string objectId)
{
    if (_feedbackSuppressed) return;
    if (!TryClaimFeedback(6)) return;

    MicroAnimationManager.Instance.PlayTriggered("drag_trail", objectId);
    AudioManager.Instance.PlaySFX("sfx_drag_start");
}

// OnDragComplete → L3 flash + drag complete SFX
void HandleDragComplete(string objectId)
{
    if (_feedbackSuppressed) return;
    if (!TryClaimFeedback(8)) return;

    MicroAnimationManager.Instance.PlayTriggered("L3_flash", objectId, 0.3f,
        onComplete: () => ReleaseFeedback());
    AudioManager.Instance.PlaySFX("sfx_drag_complete");
}

// OnDragCancel → spring-back + cancel SFX
void HandleDragCancel(string objectId)
{
    if (_feedbackSuppressed) return;
    // No priority gate — cancel visual always plays

    MicroAnimationManager.Instance.PlayTriggered("spring_back", objectId, 0.3f);
    AudioManager.Instance.PlaySFX("sfx_drag_cancel");
    ReleaseFeedback();
}

// OnChoiceSelected → L3 inner glow → ink color change + confirm SFX
void HandleChoiceSelected(string choiceId)
{
    if (_feedbackSuppressed) return;
    if (!TryClaimFeedback(10)) return; // Highest priority

    MicroAnimationManager.Instance.SetGlowLevel(choiceId, GlowLevel.L3_InnerGlow);
    // After 0.4s tween to dark ink:
    MicroAnimationManager.Instance.PlayTriggered("ink_to_dark", choiceId, 0.4f,
        onComplete: () => ReleaseFeedback());
    AudioManager.Instance.PlaySFX("sfx_choice_confirm");
}

// OnChoiceHover → L2 pulse on option dot + hover tick
void HandleChoiceHover(string choiceId)
{
    if (_feedbackSuppressed) return;
    if (IsDebounced(choiceId, "OnChoiceHover")) return;
    if (!TryClaimFeedback(3)) return;

    MicroAnimationManager.Instance.SetGlowLevel(choiceId, GlowLevel.L2_Breathing);
    AudioManager.Instance.PlaySFX("sfx_hover_tick");
}

// OnRevealObject → reveal animation + L3 flash + reveal SFX
void HandleRevealObject(string objectId)
{
    if (_feedbackSuppressed) return;
    if (!TryClaimFeedback(7)) return;

    MicroAnimationManager.Instance.PlayTriggered("object_reveal", objectId, 0.5f,
        onComplete: () => ReleaseFeedback());
    AudioManager.Instance.PlaySFX("sfx_reveal");
}

// OnShowText → text appear (no visual feedback — text itself is feedback) + text SFX
void HandleShowText(string textRef)
{
    if (_feedbackSuppressed) return;
    // No priority check — text SFX always plays

    AudioManager.Instance.PlaySFX("sfx_text_appear");
}
```

### 音频静默降级

```csharp
// In AudioManager.PlaySFX:
public async void PlaySFX(string audioKey)
{
    try
    {
        var clip = await Addressables.LoadAssetAsync<AudioClip>(audioKey).Task;
        if (clip != null)
        {
            var source = GetAvailableSource(); // SFX priority pool
            source.PlayOneShot(clip, SFXVolume * _sfxVolumeRatio);
        }
    }
    catch (Exception)
    {
        // Silent degrade — audio failure never blocks visual feedback
        // Log in debug build only
#if DEBUG
        Debug.LogWarning($"[AudioManager] Failed to load SFX: {audioKey}");
#endif
    }
}
```

### MVP 音效资产清单

| Audio Key | 描述 | 时长 | Addressables Key |
|-----------|------|------|-----------------|
| sfx_touch_generic | 指尖轻触宣纸 | ~0.2s | sfx_touch_generic |
| sfx_drag_start | 纸页掀动 | ~0.3s | sfx_drag_start |
| sfx_drag_complete | 纸页落下 | ~0.3s | sfx_drag_complete |
| sfx_drag_cancel | 纸页弹回 | ~0.2s | sfx_drag_cancel |
| sfx_choice_confirm | 毛笔搁砚声 | ~0.4s | sfx_choice_confirm |
| sfx_hover_tick | 墨滴滴落 | ~0.1s | sfx_hover_tick |
| sfx_reveal | 纸页展开 | ~0.5s | sfx_reveal |
| sfx_text_appear | 毛笔第一笔 | ~0.2s | sfx_text_appear |

### 优先级抢占——视觉中断、音频不中断

```csharp
// Visual feedback: interruptible
void InterruptVisual(string objectId)
{
    // Cancel current micro-animation for this object
    MicroAnimationManager.Instance.StopAllForObject(objectId);
    _currentFeedbackPriority = 0;
}

// Audio: non-interruptible (short SFX play to completion)
// PlaySFX is fire-and-forget — no StopSFX call
```

- 新事件抢占时：视觉反馈中断（StopAllForObject + 新动画启动），音频继续播放完成
- 原因：短音效 (<0.5s) 中断会造成听觉断裂感——不如让其播放完成

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 事件订阅结构、FeedbackMappings SO、防抖逻辑、优先级框架、_feedbackSuppressed 门控
- 微动画 (#9): SetGlowLevel L1/L2/L3 实现、PlayTriggered 动画播放
- 音频系统 (#3): PlaySFX 实现、SFX 优先级池、Addressables 加载、音量控制
- 音效资产制作（8 个短音效）——由音频总监制作，MVP 可用临时免费音效占位

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: OnInteract — visual + audio simultaneous
  - Given: InteractionFeedback initialized; MicroAnimationManager + AudioManager mocks ready
  - When: OnInteract("obj_01", "Touch") fires
  - Then: PlayTriggered("L3_flash", "obj_01", 0.3f, ...) AND PlaySFX("sfx_touch_generic") called in same handler execution; both triggered before handler returns
  - Edge cases: AudioManager mock returns error → PlayTriggered still called; visual unaffected

- **AC-2**: OnDragComplete — visual + audio
  - Given: OnDragComplete("obj_01") fires
  - When: Handler executes
  - Then: PlayTriggered("L3_flash", 0.3s) + PlaySFX("sfx_drag_complete") called; priority 8 claimed; onComplete releases feedback priority
  - Edge cases: Drag cancel after start (DragStart→DragCancel) → spring_back plays, drag_complete never fires

- **AC-3**: OnChoiceSelected — priority 10 preemption
  - Given: OnInteract visual feedback (priority 5) is actively executing on "obj_01"
  - When: OnChoiceSelected("choice_A") fires
  - Then: TryClaimFeedback(10) succeeds; OnInteract visual interrupted via StopAllForObject("obj_01"); SetGlowLevel("choice_A", L3_InnerGlow) called; PlaySFX("sfx_choice_confirm") called; OnInteract's audio (if still playing) continues undisturbed
  - Edge cases: Two OnChoiceSelected in same frame → second preempts first

- **AC-4**: Audio load failure — silent degrade
  - Given: Addressables.LoadAssetAsync<AudioClip>("sfx_touch_generic") fails (not found or network error)
  - When: OnInteract fires
  - Then: PlaySFX catches exception silently; PlayTriggered still called normally; no exception propagates to InteractionFeedback; DEBUG build logs warning
  - Pass condition: Visual feedback executes normally despite audio failure; no crash; no user-facing error

- **AC-5**: Transition suppression — full inhibit
  - Given: _feedbackSuppressed = true (in transition)
  - When: Any interaction event fires (OnHoverEnter, OnInteract, etc.)
  - Then: Handler immediately returns at gate check; neither MicroAnimationManager nor AudioManager methods called
  - Edge cases: Event fires mid-handler execution (extremely rare — single-threaded Unity) → gate check is first line, catches it

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/interaction-feedback/visual_audio_coordination_test.cs` — must exist and pass

**Status**: [x] Created — tests/integration/interaction-feedback/visual_audio_coordination_test.cs (507 lines)

---

## Dependencies

- Depends on: Story 001 (event subscription + feedback mapping + priority + debounce); 微动画 Story 001 (MicroAnimationManager API: SetGlowLevel, PlayTriggered, StopAllForObject); 音频系统 Story 002 (AudioManager.PlaySFX + SFX priority pool)
- Unlocks: None (final story in InteractionFeedback epic)

---

## Completion Notes

**Completed**: 2026-05-19
**Criteria**: 5/5 auto-verified
**Deviations**: None
**Test Evidence**: tests/integration/interaction-feedback/visual_audio_coordination_test.cs — exists
**Files created**:
- src/core/AudioManager.cs — minimal stub (full impl in audio system epic, ADR-0013)
**Files modified**:
- src/core/MicroAnimationManager.cs — +PlayTriggered(animId, objectId, overrideDuration, onComplete), +PlayFeedback(animId, objectId), +StopAllForObject(objectId)
- src/core/InteractionFeedback.cs — full handler implementations with MicroAnimationManager + AudioManager calls
**Next**: /story-done story-002
