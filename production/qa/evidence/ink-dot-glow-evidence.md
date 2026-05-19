# Story 004 -- Ink Dot Glow & Chapter Animation Presets -- Evidence

> **Story**: MicroAnimationSystem Epic, Story 004 (L1/L2/L3 vermilion ink dot glow + chapter animation config)
> **Date**: 2026-05-19
> **Type**: Visual/Feel (manual verification, no automated tests required)

## Evidence Checklist

### AC-1: L2 Breathing Pulse on Hover

**GIVEN** player hovers over an interactable object (OnHoverEnter),
**WHEN** the feedback system triggers an L2 animation,
**THEN** the vermilion ink dot next to the object enters an L2 breathing pulse
(opacity 100% ↔ 75% ↔ 100%, 2.5s sine wave period). On mouse leave, the
ink dot returns to L1 static state.

- [ ] Hover over an interactable object
  - **Expected**: Ink dot begins breathing pulse — opacity oscillates between 100% and 75% over ~2.5s
  - **Expected**: Pulse is smooth, sinusoidal (not linear, not stepped)
  - **Shader parameter**: `_GlowLevel` oscillates between 0.175 and 0.5
  - **Code path**: `SetGlowLevel(objectId, GlowLevel.L2_Breathing)` → `StartBreathingGlow()`
- [ ] Move mouse away from the object
  - **Expected**: Ink dot returns to static L1 state immediately
  - **Expected**: No residual pulsing after mouse leave
  - **Code path**: `SetGlowLevel(objectId, GlowLevel.L1_Static)` → `SetShaderGlowForObject(objectId, L1_STATIC_GLOW=0.2f, hueShift=0f)`

**Setup**:
1. Open a MemoryFragment scene with interactable objects
2. Ensure `MicroAnimationManager` is in the scene (singleton, DontDestroyOnLoad)
3. Ensure interactable objects have `SpriteRenderer` registered as shader animations via `StartShaderAnimation()`
4. Wire `InteractionManager.OnHoverEnter` → call `SetGlowLevel(objectId, GlowLevel.L2_Breathing)`
5. Wire `InteractionManager.OnHoverExit` → call `SetGlowLevel(objectId, GlowLevel.L1_Static)`

---

### AC-2: L3 Inner Glow on Choice

**GIVEN** player clicks an object and triggers a ChoiceGroup,
**WHEN** the choice panel is displayed,
**THEN** the object enters L3 inner glow state — color shifts warm
(color temperature +300K equivalent), saturation +5-10%. After the choice
is completed, the L3 effect fades out over 0.5s.

- [ ] Click an interactable object that triggers a ChoiceGroup
  - **Expected**: Ink dot glows with warm inner light immediately
  - **Expected**: Hue shifts warm (equivalent to +300K color temperature)
  - **Shader parameters**: `_GlowLevel` = 1.0, `_EmotionHue` = 0.08
  - **Code path**: `SetGlowLevel(objectId, GlowLevel.L3_InnerGlow)` → `SetShaderGlowForObject()` + `StartCoroutine(FadeGlowToL1())`
- [ ] Complete the choice (select an option)
  - **Expected**: L3 glow fades to L1 static over approximately 0.5s
  - **Expected**: Transition is smooth, no abrupt pop
  - **Code path**: `FadeGlowToL1` waits `0.5f` seconds, then calls `SetGlowLevel(objectId, GlowLevel.L1_Static)`

**Setup**:
1. Same setup as AC-1
2. Wire `InteractionManager.OnClick` / choice trigger → call `SetGlowLevel(objectId, GlowLevel.L3_InnerGlow)`
3. Wire choice completion callback → the coroutine handles the 0.5s fade automatically

---

### AC-3: L1 Static Ink Dot (Default State)

**GIVEN** a fragment has an interactable object that is NOT being hovered,
**WHEN** the fragment is displayed,
**THEN** a static vermilion ink dot is shown next to the object with the
following visual spec:
- Diameter: 4-6px at native resolution
- Color: #A03828 (Vermilion Ink: R=160, G=56, B=40)
- Texture: brush stroke ink dot (not a geometric circle)
- The ink dot does NOT glow — no bloom, no neon, no outer halo

- [ ] View a fragment with interactable objects in its default (non-hovered) state
  - **Expected**: Ink dot is visible next to each interactable object
  - **Expected**: Dot is small (4-6px), vermilion red (#A03828)
  - **Expected**: Dot has brush-stroke texture (hand-painted feel, not perfect circle)
  - **Expected**: No glow, no bloom, no outer light ring
  - **Expected**: Dot is static — no animation, no pulsing, no breathing
  - **Shader parameter**: `_GlowLevel` = 0.2 (nominal, no visible glow)

**Setup**:
1. Ensure the ink dot sprite asset is assigned to interactable objects
2. Artist must create the brush-stroke texture at specified color/size
3. Default state is L1 — no code calls needed (or explicit `SetGlowLevel(obj, GlowLevel.L1_Static)`)

**Visual spec constants** (documented in `MicroAnimationManager.cs`):
```csharp
public static readonly Color InkDotColor = new Color(0.627f, 0.220f, 0.157f, 1f); // #A03828
public const float L1_STATIC_GLOW = 0.2f;
```

---

### AC-4: Chapter 03 (Twilight) Chapter Animation Preset

**GIVEN** a Twilight chapter (Ch03) fragment,
**WHEN** ambient animation is running,
**THEN** the animation parameters are:
- Frame rate: 6-8fps (TargetFPS = 7)
- Cycle period: 1.0-1.5s
- Motion amplitude: 2-4px
- Noticeably slower than Childhood (Ch01: 11fps, 0.5-0.8s, 4-12px)

- [ ] Load a Ch03 (Twilight/Winter) fragment with ambient animation
  - **Expected**: Animation plays at ~7fps (noticeably slower than Ch01's ~11fps)
  - **Expected**: Cycle period is 1.0-1.5s (slower breathing than Ch01's 0.5-0.8s)
  - **Expected**: Motion range is 2-4px (narrower than Ch01's 4-12px)
  - **Code path**: `ChapterAnimationPreset.GetChapterPreset("ch03")`
- [ ] Compare side-by-side with Ch01 (Childhood/Spring) fragment
  - **Expected**: Ch01 is visibly faster, bouncier (EaseOutElastic)
  - **Expected**: Ch03 is visibly slower, more subdued (SineIn)
  - **Expected**: Ch03 `StillMotionRatio` = 0.95 (more still time vs. Ch01's 0.85)

**Setup**:
1. Call `var preset = ChapterAnimationPreset.GetChapterPreset("ch03");`
2. Use `preset.TargetFPS` (7) to control ambient animation tick rate
3. Use `preset.MinCyclePeriod`/`preset.MaxCyclePeriod` to randomize cycle duration
4. Use `preset.MinAmplitude`/`preset.MaxAmplitude` to set motion range
5. Use `preset.DefaultEasing` for the easing curve
6. Use `preset.StillMotionRatio` for the idle/moving time ratio

---

## Forbidden Visual Patterns (Verification)

The design spec explicitly prohibits these visual elements. Verify NONE of
them appear:

- [ ] No outer glow / bloom around the ink dot
- [ ] No neon-like colors (only vermilion #A03828)
- [ ] No geometric circular halos
- [ ] No pure white highlights on the dot
- [ ] No blinking faster than 1Hz (the breathing pulse is 0.4Hz — one full cycle per 2.5s)

---

## Files Modified / Created

| File | Action | Changes |
|---|---|---|
| `src/core/MicroAnimationManager.cs` | Modified | +GlowLevel enum, +_glowLevels Dictionary, +InkDotColor/Spec constants, +SetGlowLevel(), +SetShaderGlowForObject(), +StartBreathingGlow(), +FadeGlowToL1(), +OnUpdate in ActiveTween, +OnUpdate invocation in ProcessTweens |
| `src/core/MicroAnimationCatalog.cs` | Modified | +ChapterAnimationPreset class + GetChapterPreset() factory |
| `production/qa/evidence/ink-dot-glow-evidence.md` | Created | This file |

---

## Sign-Off

**Verified by**: __________________ (Lead Programmer / Art Director)
**Date**: __________________
**Result**: [ ] APPROVED / [ ] CHANGES REQUIRED

**Notes**:
