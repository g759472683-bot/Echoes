# Story 004: L1/L2/L3 朱砂墨点发光 + 章节动画配置

> **Epic**: 微动画系统 (MicroAnimationSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Visual/Feel
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/micro-animation-system.md`
**Requirement**: `TR-micro-animation-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0012: 微动画系统
**ADR Decision Summary**: L1/L2/L3 朱砂墨点发光系统——单一 _GlowLevel 参数三级值（0.2/0.5/1.0）。L1 静态邀请（4-6px 朱砂墨点 #A03828）；L2 呼吸脉动（opacity 100%↔75%，2.5s 正弦周期）；L3 物件内光（色温 +300K，饱和度 +5-10%）。绝对禁止外发光、霓虹色、几何圆形光晕、纯白高光、>1Hz 闪烁。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Shader Graph _GlowLevel + _EmotionHue + MaterialPropertyBlock；墨点使用手写体纹理 SpriteRenderer（非 UI Toolkit）

**Control Manifest Rules (Feature Layer)**:
- Required: 墨点色调 #A03828 朱砂红，笔触纹理 — source: ADR-0012/GDD

---

## Acceptance Criteria

*From GDD `design/gdd/micro-animation-system.md`, scoped to this story:*

- [ ] GIVEN 玩家悬停在一个可交互物件上 (OnHoverEnter)，WHEN 反馈系统触发 L2 动画，THEN 物件旁的朱砂墨点进入 L2 呼吸脉动（opacity 100%↔75%↔100%，2.5s 周期正弦）。鼠标离开后墨点恢复 L1 静态状态。

- [ ] GIVEN 玩家点击一个物件并触发了 ChoiceGroup，WHEN 选择面板展示，THEN 物件进入 L3 内光状态——颜色向暖端偏移（色温 +300K），饱和度 +5-10%。选择完成后 L3 效果在 0.5s 内淡出。

- [ ] GIVEN 一个碎片有可交互物件但未被悬停，WHEN 碎片展示，THEN 物件旁显示 L1 静态朱砂墨点（直径 4-6px，颜色 #A03828，笔触纹理）。墨点不发光——不是霓虹光。

- [ ] GIVEN 暮年章（Ch03）碎片，WHEN 环境动画运行，THEN 帧率 6-8fps，循环周期 1.0-1.5s，运动幅度 2-4px——明显慢于童年章动画。

---

## Implementation Notes

*Derived from GDD rules 9-10 + ADR-0012:*

### L1/L2/L3 Glow System

```csharp
public enum GlowLevel { L1_Static = 0, L2_Breathing = 1, L3_InnerGlow = 2 }

// L1: Static ink dot — _GlowLevel = 0.2
// L2: Breathing pulse — _GlowLevel = 0.2 + 0.3 * sin(time * 2π / 2.5s)
// L3: Inner glow — _GlowLevel = 1.0, _EmotionHue warm shift

public void SetGlowLevel(string objectId, GlowLevel level)
{
    switch (level)
    {
        case GlowLevel.L1_Static:
            _mpb.SetFloat("_GlowLevel", 0.2f);
            _mpb.SetFloat("_EmotionHue", 0f); // No shift
            break;
        case GlowLevel.L2_Breathing:
            // L2 managed by looping MicroTween — sine wave 2.5s period
            StartBreathingGlow(objectId);
            break;
        case GlowLevel.L3_InnerGlow:
            _mpb.SetFloat("_GlowLevel", 1.0f);
            _mpb.SetFloat("_EmotionHue", 0.08f); // +300K equivalent
            // After 0.5s, fade back to L1 via MicroTween
            FadeGlowToL1(objectId, 0.5f);
            break;
    }
}
```

### Ink Dot Visual Spec (L1)

- Color: Vermilion Ink `#A03828`
- Diameter: 4-6px
- Texture: brush stroke (ink dot, not geometric circle)
- Opacity: 85% (allows underlying painting to show through)

**ABSOLUTELY FORBIDDEN**: outer glow, neon colors, geometric circular halos, pure white highlights, >1Hz blinking.

### Chapter Animation Config Presets

| Parameter | Childhood (Ch01) | Youth (Ch02) | Twilight (Ch03) |
|-----------|-----------------|-------------|-----------------|
| FPS | 10–12 | 8–10 | 6–8 |
| Cycle Period (s) | 0.5–0.8 | 0.8–1.0 | 1.0–1.5 |
| Motion Amplitude (px) | 4–12 | 3–8 | 2–4 |
| Easing | ElasticOut | Smooth InOut | EaseIn dominant |
| Still-to-Motion Ratio | 85:15 | 90:10 | 95:5 |

```csharp
[Serializable]
public class ChapterAnimationPreset
{
    public string ChapterKey;
    public int TargetFPS;
    public float MinCyclePeriod;
    public float MaxCyclePeriod;
    public float MinAmplitude;
    public float MaxAmplitude;
    public EaseType DefaultEasing;
    public float StillMotionRatio; // fraction of time in motion
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001-003: Core animation infrastructure
- 交互反馈 (#18): OnHoverEnter/OnInteract event → calls SetGlowLevel
- 章节动画配置集的存储位置（Catalog vs ChapterDefinition — Open Question）
- 手绘墨点 sprite asset 创建（美术资产——使用临时圆点占位）

---

## QA Test Cases

#### Manual Verification Steps

- **AC-1**: L2 breathing pulse on hover
  - Setup: Enter fragment with interactive object; move mouse over object
  - Verify: Vermilion ink dot next to object starts pulsing — opacity fades 100%↔75% over ~2.5s sine wave. No neon glow. No geometric halo.
  - Pass condition: Breathing is visible but subtle; mouse leaves → dot returns to static L1 state within 0.3s

- **AC-2**: L3 inner glow on click
  - Setup: Click interactive object that triggers ChoiceGroup
  - Verify: Object color shifts slightly warm (+300K equivalent); saturation increases subtly (+5-10%). After choice completes, glow fades out over 0.5s.
  - Pass condition: L3 feels like "light passing through pigment" — not a flashlight. Fade-out is smooth.

- **AC-3**: L1 static ink dot
  - Setup: Enter any fragment with interactive objects; don't hover
  - Verify: Small vermilion-red dot (4-6px) with brush-stroke texture next to each interactive object. Dot is static — no pulsing, no glow.
  - Pass condition: Dots are visible enough to invite interaction (3s rule) but don't break immersion

- **AC-4**: Chapter animation pacing difference
  - Setup: Enter Ch01 fragment, then enter Ch03 fragment
  - Verify: Ch01 animations feel youthful/spring-like (10-12fps, elastic easing, 4-12px motion). Ch03 animations feel twilight/winter-like (6-8fps, slower cycles, 2-4px subtle motion).
  - Pass condition: Pacing difference is perceptible when comparing side-by-side; transitions within chapter are gradual, not abrupt

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: `production/qa/evidence/ink-dot-glow-evidence.md` — must exist with sign-off

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (MicroAnimationManager); Story 003 (EmotionPreset modulation); 交互反馈 Story 002 (calls SetGlowLevel on hover/click)
- Unlocks: 交互系统 (#11) integration testing
