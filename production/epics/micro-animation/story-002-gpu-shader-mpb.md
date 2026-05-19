# Story 002: GPU Shader 动画 + MaterialPropertyBlock

> **Epic**: 微动画系统 (MicroAnimationSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/micro-animation-system.md`
**Requirement**: `TR-micro-animation-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0012: 微动画系统
**ADR Decision Summary**: GPU 动画通过 URP 2D Shader Graph 材质属性 + MaterialPropertyBlock.SetFloat 逐帧写入参数。CPU 仅需每帧每动画一次 SetFloat 调用。包含两个 Verified Fallback：顶点位移不可用 → CPU Transform 替代；MPB 与 SRP Batcher 不兼容 → Material 实例替代。

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: CRITICAL — URP 2D Shader Graph 不支持 SpriteLit 目标的顶点位移。必须实现 Fallback 1（CPU Transform 替代）。必须测试 MPB.SetFloat 在 URP 2D + SRP Batcher 下的实际行为。

**Control Manifest Rules (Feature Layer)**:
- Required: MaterialPropertyBlock 在 Awake() 创建，复用避免分配 — source: ADR-0012
- Required: 实现两个 Verified Fallback 并在 Awake() 中检测切换 — source: ADR-0012

---

## Acceptance Criteria

*From GDD `design/gdd/micro-animation-system.md`, scoped to this story:*

- [ ] GIVEN 碎片定义了 "leaf_sway" (Shader_VertexDisplace)，WHEN 碎片展示 5 秒，THEN 树叶 Sprite 顶点持续位移（或 Fallback 1：Transform localPosition 正弦位移）。CPU 侧每帧仅 MaterialPropertyBlock.SetFloat 一次调用。

- [ ] GIVEN Shader Graph 使用 _FragmentTime / _GlowLevel / _EmotionHue / _EmotionSaturation 参数，WHEN Manager 每帧更新，THEN MaterialPropertyBlock.SetFloat 写入当前值——SpriteRenderer 渲染反映参数变化。

- [ ] GIVEN URP 2D SpriteLit 不支持顶点位移，WHEN MicroAnimationManager 在 Awake() 检测到不支持，THEN 自动切换到 Fallback 1（CPU Transform 替代）——无异常，LogWarning 记录。

- [ ] GIVEN MaterialPropertyBlock 与 SRP Batcher 不兼容，WHEN MPB.SetFloat 无效果，THEN 自动切换到 Fallback 2（Material 实例 + Material.SetFloat）——动画仍正常运行。

- [ ] GIVEN 一个碎片有 3 个 GPU Shader 动画 + 2 个 CPU Tween 动画，WHEN Update() 执行，THEN CPU 时间 <2ms（每碎片预算）。

---

## Implementation Notes

*Derived from ADR-0012 Implementation Guidelines + Verified Fallbacks:*

### Shader Graph Material Properties

```
Shader Graph Properties (written by MaterialPropertyBlock):
  _FragmentTime    (float)  — Time since fragment loaded (seconds)
  _GlowLevel       (float)  — L1/L2/L3 glow intensity [0-1]
  _EmotionHue      (float)  — Emotion-driven hue shift
  _EmotionSaturation(float) — Emotion-driven saturation modulation
  _DistortStrength (float)  — Ink spread intensity
  _ParallaxOffset  (Vector2) — Parallax displacement
```

### Per-Frame Update

```csharp
void UpdateShaderAnimations()
{
    foreach (var anim in _shaderAnims)
    {
        var mpb = anim.MaterialPropertyBlock; // Reused — created in Awake()
        float t = Time.time - anim.StartTime;
        
        mpb.SetFloat("_FragmentTime", t);
        mpb.SetFloat("_GlowLevel", anim.CurrentGlowLevel);
        mpb.SetFloat("_EmotionHue", anim.EmotionPreset.HueShift);
        mpb.SetFloat("_EmotionSaturation", anim.EmotionPreset.SaturationMod);
        
        anim.Target.SetPropertyBlock(mpb);
    }
}
```

### Fallback 1: CPU Transform for Vertex Displacement

```csharp
// If Shader Graph vertex displacement unsupported in URP 2D SpriteLit:
void ApplyParallaxFallback(SpriteRenderer sr, Vector2 offset, float distortStrength)
{
    sr.transform.localPosition = offset;
    // Distort simulated via scale: (1+distort, 1-distort) preserving area
    sr.transform.localScale = new Vector3(
        1f + distortStrength, 1f - distortStrength, 1f);
}
```

### Fallback 2: Material Instance for MPB Failure

```csharp
// If MaterialPropertyBlock ineffective with SRP Batcher:
void ApplyShaderParam(SpriteRenderer sr, string paramName, float value)
{
    if (_useMaterialPropertyBlock)
        _mpb.SetFloat(paramName, value);
    else
        sr.material.SetFloat(paramName, value); // Material instance fallback
}
```

Detection: test MPB by setting a known value on a test SpriteRenderer, reading back via `sr.material.GetFloat()` — if unchanged, switch to fallback.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: MicroAnimationCatalog + Manager skeleton + MicroTween
- Story 003: 性能降级 + OnFragmentTransitioned 动画启动/停止
- Story 004: L1/L2/L3 墨点发光视觉规格
- Shader Graph 资产的创建（本故事只需定义接口——具体 .shadergraph 由技术美术创建）
- URP 2D Renderer 配置

---

## QA Test Cases

- **AC-1**: Shader animation runs on GPU
  - Given: AmbientAnimDef "leaf_sway" (Shader_VertexDisplace, Speed=0.3, Amplitude=0.02)
  - When: Active for 5 seconds
  - Then: SpriteRenderer vertices displaced in sine wave (or Transform.position in fallback); MPB.SetFloat called once per frame
  - Edge cases: Speed=0 → static; Amplitude=0 → static

- **AC-2**: MaterialPropertyBlock parameter write
  - Given: Active shader animation with _GlowLevel varying 0→1 over 3s
  - When: 1.5s elapsed
  - Then: MPB.SetFloat("_GlowLevel", ~0.5) called; SpriteRenderer renders intermediate glow
  - Edge cases: Multiple animations on same SpriteRenderer → last MPB write wins for shared params

- **AC-3**: Fallback 1 — vertex displacement unsupported
  - Given: URP 2D SpriteLit does not support Shader Graph vertex displacement
  - When: Awake() detects incompatibility
  - Then: LogWarning logged; subsequent "leaf_sway" animations use CPU Transform fallback; visual result similar but CPU cost ~0.02ms higher per SpriteRenderer
  - Edge cases: Some animations are Shader_UVScroll (fragment stage, unaffected) — not switched to fallback

- **AC-4**: Fallback 2 — MPB with SRP Batcher
  - Given: MPB.SetFloat ineffective with SRP Batcher enabled
  - When: Detection test in Awake() shows no change
  - Then: Switch to Material instance fallback; ~50KB extra memory for 10 materials; LogWarning
  - Edge cases: SRP Batcher disabled in Project Settings → MPB works, fallback not used

- **AC-5**: Within performance budget
  - Given: 3 GPU shader animations + 2 CPU tween animations
  - When: Update() profiled
  - Then: CPU <2ms; no GC allocation from MPB reuse
  - Edge cases: 0 animations → Update early returns

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/micro-animation/gpu_shader_mpb_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (MicroAnimationManager Update loop + MicroTween)
- Unlocks: Story 003 (perf degradation needs working GPU pipeline); Story 004 (L1/L2/L3 glow uses _GlowLevel)
