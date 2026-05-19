# Story 003: 性能降级 + EmotionPreset 参数调制

> **Epic**: 微动画系统 (MicroAnimationSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/micro-animation-system.md`
**Requirement**: `TR-micro-animation-004`, `TR-micro-animation-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0012: 微动画系统
**ADR Decision Summary**: 三级性能降级（High→Medium→Low）基于帧时间阈值自动切换：High (<14ms frame) 全动画 60fps；Medium (14-20ms) 环境动画降至 30fps；Low (>20ms) 仅保留交互反馈动画。EmotionPreset 通过 _EmotionHue/_EmotionSaturation 调制动画参数——温暖/遗憾/恐惧三种情绪各有不同的速度/幅度/颜色偏移。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 帧时间检测使用 Time.unscaledDeltaTime；降级在下一帧生效（不立即切换以防止抖动）

**Control Manifest Rules (Feature Layer)**:
- Required: 降级静默触发——不弹出通知 — source: ADR-0012
- Forbidden: 不在当前帧立即恢复全动画——下个碎片切换时恢复

---

## Acceptance Criteria

*From GDD `design/gdd/micro-animation-system.md`, scoped to this story:*

- [ ] GIVEN 帧时间持续超过 14ms（Medium 降级），WHEN 下一帧 tick 运行，THEN 循环环境动画的更新频率降至每 2 帧一次（30fps）。交互反馈动画保持 60fps。不弹出通知。

- [ ] GIVEN 帧时间超过 20ms（Low 降级），WHEN tick 执行，THEN 环境动画暂停——仅交互反馈动画继续运行。恢复时在下个碎片切换时自动回到 High。

- [ ] GIVEN 当前碎片的主情感标签是 "Sadness"（遗憾），WHEN EmotionPreset.Apply("sadness") 执行，THEN speedMultiplier=0.6, amplitudeMultiplier=0.7, colorShift 饱和度 -5% + 边缘模糊 +2px。

- [ ] GIVEN 情感标签系统 (#10) 未就绪，WHEN EmotionPreset 参数不可用，THEN Manager 使用默认（温暖）参数集——speedMultiplier=0.8, amplitudeMultiplier=1.3, 无颜色偏移。

---

## Implementation Notes

*Derived from ADR-0012 Implementation Guidelines:*

### Performance Degradation

```csharp
public enum PerfLevel { High, Medium, Low, Minimal }

PerfLevel EvaluatePerfLevel(float currentFrameTime)
{
    if (currentFrameTime > 20.0f) return PerfLevel.Low;      // Ambient paused, feedback only
    if (currentFrameTime > 14.0f) return PerfLevel.Medium;   // Ambient at 30fps
    return PerfLevel.High;                                    // Full animation 60fps
}
```

Degradation behavior:
- **High**: All animations at 60fps — normal
- **Medium**: Ambient loop animations tick every 2nd frame (30fps); Triggered + Feedback at 60fps
- **Low**: Ambient paused entirely; Triggered + Feedback at 60fps
- **Minimal**: Only feedback (click confirm) animations — integrated GPU detection

Recovery: auto-restore on next fragment transition — NOT mid-frame (prevents oscillation).

### EmotionPreset Parameter Modulation

```csharp
[Serializable]
public class EmotionPreset
{
    public string EmotionCategory;   // "warmth", "sadness", "fear"
    public float SpeedMultiplier;    // 0.8 / 0.6 / 2.0
    public float AmplitudeMultiplier; // 1.3 / 0.7 / 1.5
    public IntervalPattern Interval; // regular / irregular(skipRate) / burst(cluster,gap)
    public ColorShiftDef ColorShift; // saturation ±%, blur px, contrast %
}

// Applied during animation start:
void ApplyEmotionPreset(AmbientAnimDef def, EmotionPreset preset)
{
    float finalSpeed = def.DefaultSpeed * preset.SpeedMultiplier;
    float finalAmplitude = def.DefaultAmplitude * preset.AmplitudeMultiplier;
    // Write to MPB or MicroTween parameters
}
```

Default preset (when emotional tag system not ready): warmth (speed=0.8, amplitude=1.3, no color shift).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: MicroAnimationManager Update loop
- Story 002: GPU shader animation + MPB implementation
- Story 004: L1/L2/L3 墨点发光
- 情感标签系统 (#10): 主情感标签查询
- 集成显卡检测（Minimal level — use SystemInfo.graphicsDeviceType detection）

---

## QA Test Cases

- **AC-1**: Medium degradation
  - Given: Frame time consistently 16ms (>14ms)
  - When: Update() tick runs
  - Then: Ambient loop animations tick every other frame; Feedback anims at full rate; no UI notification
  - Edge cases: Frame time oscillates 13ms↔15ms → hysteresis: degrade after 3 consecutive >14ms frames

- **AC-2**: Low degradation
  - Given: Frame time consistently 22ms (>20ms)
  - When: Update() tick runs
  - Then: Ambient animations paused; Feedback animations still at 60fps; recovery on next fragment transition
  - Edge cases: Rapid perfLevel changes → no mid-frame switching, use 3-frame averaging

- **AC-3**: Emotion preset — sadness
  - Given: Current fragment's dominant tag is "Sadness"; sadness preset loaded
  - When: Ambient animation starts
  - Then: speed=def.Speed × 0.6; amplitude=def.Amplitude × 0.7; MPB saturation=-5%, blur=+2px
  - Edge cases: Preset missing for emotion category → fall back to default (warmth)

- **AC-4**: Default preset fallback
  - Given: EmotionalTagSystem not available (null or not initialized)
  - When: Ambient animation starts
  - Then: Default "warmth" preset applied (speed×0.8, amplitude×1.3, no color shift)
  - Edge cases: EmotionPreset[] array empty → hardcoded defaults (speed=1.0, amplitude=1.0)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/micro-animation/perf_degradation_emotion_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (Manager Update loop); Story 002 (GPU animation pipeline); 情感标签 Story 002 (GetDominantTag API)
- Unlocks: Story 004 (L1/L2/L3 glow uses emotion-modulated parameters)
