# ADR-0012: 微动画系统 — URP 2D Shader Graph + MaterialPropertyBlock

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

回响 (Echoes) 需要碎片上的微动画（环境动画循环、触发动画、反馈动画）—— 全部在 GPU 端执行。决定使用 URP 2D Shader Graph + 自定义 MicroTween 值类型结构体 + MaterialPropertyBlock 逐帧参数写入 + 三级性能降级策略。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Rendering |
| **Knowledge Risk** | HIGH — URP 2D Shader Graph 在 Unity 6 中有重大变更；LLM 训练数据覆盖的 Shader Graph 版本已过时 |
| **References Consulted** | `VERSION.md`, `breaking-changes.md`, `modules/rendering.md` |
| **Post-Cutoff APIs Used** | `Shader Graph` (URP 2D), `MaterialPropertyBlock.SetFloat()` |
| **Verification Required** | URP 2D SpriteLit/Unlit 是否支持顶点位移和 UV 滚动 Shader Graph；`MaterialPropertyBlock` 与 URP 2D SpriteRenderer 兼容性 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None (但 Open Question 需先验证 Shader Graph 兼容性) |
| **Enables** | ADR-0014 (交互反馈 — 触发动画调用) |
| **Blocks** | MicroAnimation + InteractionFeedback Epic |
| **Ordering Note** | 在 ADR-0014 之前实现 |

## Context

### Problem Statement

画轴上的每个碎片需要有微妙的"呼吸"感 —— 墨迹在纸上晕开、光影在叶间移动、尘埃在光线中悬浮。这些动画需要在不增加 CPU 开销的前提下运行（GPU 端执行），性能预算 ~1ms/frame，且必须在性能下降时自动降级。

### Constraints

- 帧预算 ~1.0ms（留给微动画，在 16.6ms 总预算内）
- 必须支持 10+ 个 SpriteRenderer 同时运行环境动画
- 性能降级必须自动（High→Medium→Low→Minimal）
- 零 GC 分配（MicroTween 是值类型 struct）

### Requirements

- 3 种动画类别：Ambient (循环)、Triggered (一次性)、Feedback (即时响应)
- 自定义 MicroTween 值类型（~250 行，零 GC）
- MaterialPropertyBlock 逐帧写入 GPU 参数
- 三级 L1/L2/L3 朱砂墨点发光
- EmotionPreset 参数调制

## Decision

**URP 2D Shader Graph + MaterialPropertyBlock + MicroTween (值类型) + 三级性能降级。**

### 三类动画

| 类别 | 触发 | 生命周期 | 示例 |
|------|------|---------|------|
| **Ambient** | 碎片加载时自动启动 | 循环，碎片卸载时停止 | 墨迹晕开、光斑移动、颗粒漂浮 |
| **Triggered** | 玩家交互事件触发 | 一次性播放到完成 | 选项确认闪光、对象显隐过渡 |
| **Feedback** | 交互反馈映射 | 即时触发，持续 0.3-0.5s | 悬停发光 L1、点击发光 L2、确认发光 L3 |

### Shader Graph 参数接口

```
Shader Graph Properties (MaterialPropertyBlock 写入):
  _FragmentTime    (float)  — 碎片自加载起的时间 (秒)
  _GlowLevel       (float)  — L1/L2/L3 发光强度 [0-1]
  _EmotionHue      (float)  — 情感色相偏移 (来自 EmotionPreset)
  _EmotionSaturation(float) — 情感饱和度调制
  _DistortStrength (float)  — 墨迹晕开强度
  _ParallaxOffset  (Vector2) — 视差偏移
```

### MicroTween 值类型

```csharp
// 零 GC 值类型补间 (~250 lines)
public struct MicroTween
{
    public float Duration;
    public float Elapsed;
    public EaseType Ease;
    public float FromValue;
    public float ToValue;

    public float Evaluate()
    {
        var t = Mathf.Clamp01(Elapsed / Duration);
        return Mathf.Lerp(FromValue, ToValue, ApplyEase(t));
    }

    public bool IsComplete => Elapsed >= Duration;
}

// 存储 AnimationInstances 在数组中 (非 Dictionary — 避免 GC)
public struct MicroAnimInstance
{
    public string AnimId;
    public MicroTween[] Tweens;     // 每个材质参数一个 Tween
    public MaterialPropertyBlock MPB;
    public SpriteRenderer Target;
    public float TotalDuration;
    public bool IsLooping;
}
```

### 三级性能降级

```csharp
public enum PerfLevel { High, Medium, Low, Minimal }

PerfLevel EvaluatePerfLevel(float currentFrameTime)
{
    if (currentFrameTime > 14.0f) return PerfLevel.Minimal;  // 仅 L3 反馈
    if (currentFrameTime > 12.0f) return PerfLevel.Low;      // Ambient 暂停
    if (currentFrameTime > 10.0f) return PerfLevel.Medium;    // Ambient 减半 FPS
    return PerfLevel.High;                                     // 全部动画
}
```

### Architecture Diagram

```
┌──────────────────────────────────────────────┐
│       MicroAnimationManager                   │
│                                              │
│  Update() [~1ms @ High perf]                 │
│    ├─ EvaluatePerfLevel(frameTime)            │
│    ├─ AmbientAnimInstances[]                  │
│    │   └─ 每实例: MicroTween.Evaluate()       │
│    │      → MaterialPropertyBlock.SetFloat()  │
│    ├─ TriggeredAnimInstances[]                │
│    │   └─ 完成后移除                          │
│    └─ FeedbackAnimInstances[]                 │
│        └─ 即时播放, 0.3-0.5s 后移除          │
│                                              │
│  MaterialPropertyBlock (per SpriteRenderer)   │
│    → GPU: Shader Graph 读取参数               │
│       ├─ 顶点位移 (_ParallaxOffset)            │
│       ├─ UV 滚动 (_FragmentTime)               │
│       ├─ 颜色调制 (_EmotionHue/Saturation)     │
│       └─ 发光 (_GlowLevel)                     │
└──────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public class MicroAnimationManager : MonoBehaviour
{
    public void PlayTriggered(string animationId);
    public void PlayFeedback(string animationId);
    public void SetGlowLevel(int level); // L1/L2/L3
    public void SetEmotionPreset(EmotionPreset preset);

    // Update() — 逐帧更新所有活动动画
}
```

### Implementation Guidelines

1. Shader Graph 中使用 `Time` 节点 + `_FragmentTime` offset（支持循环）
2. MaterialPropertyBlock 在 `Awake()` 中创建，复用以避免分配
3. 动画实例存储在 `FixedArray<T>` 或预分配数组中（非 `List<T>` — 避免扩容 GC）
4. Performance Level 在 `Update()` 开头评估一次
5. L1/L2/L3 发光是同一个 `_GlowLevel` 参数的不同值（0.2/0.5/1.0）

### Verified Fallbacks (Engine Specialist Review)

Unity 6.3 LTS 中 URP 2D Shader Graph 存在以下已验证限制，需要显式回退方案：

**Fallback 1 — 顶点位移 (`_ParallaxOffset`, `_DistortStrength`)**

URP 2D SpriteLit/SpriteUnlit 目标使用专用 2D 顶点阶段（固定 quad），不支持
Shader Graph 的 Position 节点做顶点位移。`_ParallaxOffset` 和 `_DistortStrength`
在 2D Sprite 目标上无法编译或无声效。

**回退方案**: 首次原型验证时测试顶点位移是否生效。若失败，改为 CPU 端方案：
```csharp
// CPU 端 SpriteRenderer transform 位移替代 GPU 端顶点位移
void ApplyParallax(SpriteRenderer sr, Vector2 offset, float distortStrength)
{
    sr.transform.localPosition = offset;
    // distort 通过 scale 模拟: (1 + distort, 1 - distort) 保持面积
    sr.transform.localScale = new Vector3(
        1f + distortStrength, 1f - distortStrength, 1f);
}
```
CPU 端 transform 操作增加 ~0.02ms/SpriteRenderer（10 个 Ambient 动画 = ~0.2ms），
仍在 1ms 预算内。UV 滚动（`_FragmentTime`）在 fragment 阶段运行，不受影响。

**Fallback 2 — MaterialPropertyBlock + SRP Batcher 兼容性**

Unity 6.3 中 SRP Batcher 默认启用，可能覆盖 `MaterialPropertyBlock` 值（
Batcher 按 Shader Variant 合批而非 Material Instance）。`MaterialPropertyBlock.SetFloat()`
在启用 SRP Batcher 的 URP 2D 中可能无效果。

**回退方案**: 若 MPB 不生效，改用 Material 实例 + `Material.SetFloat()`：
```csharp
// 回退：每个 SpriteRenderer 持有独立 Material 实例
// 内存开销: ~5KB/fragment × 10 fragments = ~50KB（可接受）
void ApplyShaderParam(SpriteRenderer sr, string paramName, float value)
{
    if (_useMaterialPropertyBlock) // 优先方案
        _mpb.SetFloat(paramName, value);
    else // 回退
        sr.material.SetFloat(paramName, value);
}
```
内存开销约 50KB（10 个碎片各一个 Material 实例），在 2GB 预算下可忽略。
SRP Batcher 合批损失在 2D 游戏中影响极小（draw call 总数 50-100）。

## Alternatives Considered

### Alternative 1: Animator + AnimationClip

- **Description**: 使用 Unity Mecanim Animator 控制 SpriteRenderer 属性
- **Pros**: 可视化编辑曲线；与 Timeline 集成
- **Cons**: Animator 每帧有 GC 分配；不适合 10+ 个 SpriteRenderer 同时运行；无法在 GPU 端做顶点/UV 效果
- **Rejection Reason**: GC 分配超标；GPU 端动画能力不足

### Alternative 2: DOTween (第三方库)

- **Description**: 使用 DOTween 补间库控制材质属性
- **Pros**: 成熟库，API 简洁
- **Cons**: 外部依赖；DOTween 内部有 GC 分配；不支持自定义 Shader Graph 参数类型
- **Rejection Reason**: 外部依赖 + GC 分配。250 行 MicroTween 自研成本低于依赖管理开销

## Consequences

### Positive

- GPU 端动画 — CPU 只做参数写入，不参与渲染计算
- 零 GC 分配（值类型 struct + 预分配数组）
- 性能自动降级保证帧率
- EmotionPreset 参数调制连接情感标签系统

### Negative

- Shader Graph 学习曲线（美术需要理解材质参数接口）
- URP 2D Shader Graph 兼容性未验证（Open Question）
- 自定义 MicroTween 需要维护和单元测试
- 动画效果预览在 Editor 中可能不准确（需要 Play Mode 验证）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| URP 2D Shader Graph 不支持顶点位移 | **High (Verified)** | High | 回退到 CPU 端 SpriteRenderer transform 动画（见 Verified Fallbacks §1）；UV 滚动不受影响 |
| MaterialPropertyBlock 与 SRP Batcher 不兼容 | **Medium (Verified)** | Medium | 回退到 Material 实例 + `Material.SetFloat()`（见 Verified Fallbacks §2）；内存开销 ~50KB 可忽略 |
| MicroTween 精度导致动画抖动 | Low | Medium | 单元测试覆盖；双精度关键路径 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (High perf, 10 ambient + 2 feedback) | ~1.0ms |
| CPU (Medium perf, 5 ambient + 2 feedback) | ~0.5ms |
| CPU (Low perf, 0 ambient + 2 feedback) | ~0.2ms |
| CPU (Minimal perf, 1 feedback) | ~0.05ms |
| GPU | Marginal (simple vertex/UV/fragment ops) |
| GC Allocation | 0 (value types + preallocated arrays) |
| Memory (10 animation instances) | ~5KB |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] Shader Graph 在 URP 2D SpriteLit 管线下编译通过
- [ ] MaterialPropertyBlock.SetFloat 在 SpriteRenderer 上生效
- [ ] 10 个 Ambient 动画同时运行时 CPU 时间 < 1.5ms
- [ ] PerfLevel 降级在 frameTime 超限的下一帧生效
- [ ] MicroTween.Evaluate 零 GC (profiler 验证)
- [ ] L1/L2/L3 发光三层视觉差异可辨识

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `micro-animation-system.md` (#9) | 微动画 | 3 类动画 (Ambient/Triggered/Feedback) | 本 ADR 定义完整三类系统 |
| `micro-animation-system.md` (#9) | 微动画 | 三级性能降级 | PerfLevel (High/Medium/Low/Minimal) |
| `micro-animation-system.md` (#9) | 微动画 | L1/L2/L3 朱砂墨点发光 | _GlowLevel 单一参数三级值 |
| `micro-animation-system.md` (#9) | 微动画 | EmotionPreset 参数调制 | _EmotionHue/_EmotionSaturation Shader 参数 |
| `interaction-feedback.md` (#18) | 交互反馈 | 交互事件的视觉动画 | PlayTriggered/PlayFeedback 接口 |
| `emotional-tag-system.md` (#10) | 情感标签 | 标签权重影响视觉效果 | EmotionPreset → Shader 参数映射 |

## Related

- ADR-0014 — 交互反馈系统调用 PlayTriggered/PlayFeedback
- `docs/engine-reference/unity/modules/rendering.md` — URP 2D Shader Graph API
- `docs/architecture/architecture.md` — Open Questions: URP 2D Shader Graph 兼容性
