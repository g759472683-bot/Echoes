# Story 001: MicroAnimationCatalog + Manager 骨架 + MicroTween

> **Epic**: 微动画系统 (MicroAnimationSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/micro-animation-system.md`
**Requirement**: `TR-micro-animation-001`, `TR-micro-animation-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0012: 微动画系统
**ADR Decision Summary**: MicroAnimationCatalog SO（AmbientAnimDef/TriggeredAnimDef/FeedbackAnimDef 三类定义），MicroAnimationManager MonoBehaviour 单例（Update 统一 tick），MicroTween 值类型 struct（零 GC，5 种缓动函数），OnFragmentTransitioned 集成生命周期

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: URP 2D Shader Graph 兼容性未验证——本故事只涉及 CPU 侧架构，GPU 部分在 Story 002

**Control Manifest Rules (Feature Layer)**:
- Required: MicroTween 值类型 struct — 零 GC 分配 — source: ADR-0012
- Forbidden: Never 引入 DOTween/LeanTween 等外部 tween 库 — source: ADR-0012

---

## Acceptance Criteria

*From GDD `design/gdd/micro-animation-system.md`, scoped to this story:*

- [ ] GIVEN MicroAnimationCatalog 定义了 "leaf_sway" (Ambient, Shader_VertexDisplace, Speed=0.3, Amplitude=0.02)，WHEN Manager 查找 "leaf_sway"，THEN 返回完整的 AmbientAnimDef。Catalog 未加载时返回 null + LogWarning。

- [ ] GIVEN 一个 MicroTween 实例 (FromValue=0, ToValue=1, Duration=2s, Ease=SineInOut)，WHEN 经过 1.0s 后调用 Evaluate()，THEN 返回值在 0.5 附近（EaseInOut 对称）。2.0s 后 Evaluate() 返回 1.0，IsComplete = true。

- [ ] GIVEN 玩家进入一个碎片（OnFragmentTransitioned），WHEN 碎片有 2 个 AmbientAnimInstances，THEN Manager 在 FadeIn 开始前创建 2 个活跃动画实例。StopAllForFragment(oldFragmentId) 在启动新动画前完成。

- [ ] GIVEN 当前碎片有 10 个 Ambient 动画活跃，WHEN Manager.Update() 每帧 tick，THEN CPU 时间 <1ms（无 GC 分配——MicroTween 是值类型）。

---

## Implementation Notes

*Derived from ADR-0012 Implementation Guidelines:*

### MicroAnimationCatalog SO

```csharp
[CreateAssetMenu(menuName = "Echoes/MicroAnimationCatalog")]
public class MicroAnimationCatalog : ScriptableObject
{
    public AmbientAnimDef[] AmbientDefs;
    public TriggeredAnimDef[] TriggeredDefs;
    public FeedbackAnimDef[] FeedbackDefs;
    public EmotionPreset[] EmotionPresets;
}

[Serializable]
public class AmbientAnimDef
{
    public string DefId;
    public AnimationImpl Implementation; // Shader_VertexDisplace | Tween_Loop | SpriteSwap
    public float DefaultSpeed;
    public float DefaultAmplitude;
    public EaseType DefaultEasing;
}
```

### MicroAnimationManager

```csharp
public class MicroAnimationManager : MonoBehaviour
{
    private MicroAnimationCatalog _catalog;
    private List<ActiveAnimation> _activeAnimations;
    private List<MicroTween> _activeTweens;
    private List<MicroTweenLoop> _activeLoops;
    
    void Update()
    {
        float dt = Time.deltaTime;
        // Tick all active tweens
        for (int i = _activeTweens.Count - 1; i >= 0; i--)
        {
            var tween = _activeTweens[i];
            tween.Elapsed += dt;
            tween.OnUpdate?.Invoke(tween.Evaluate());
            if (tween.IsComplete)
            {
                tween.OnComplete?.Invoke();
                _activeTweens.RemoveAt(i);
            }
        }
    }
    
    void OnEnable() => SceneManager.OnFragmentTransitioned += HandleFragmentTransitioned;
    void OnDisable() => SceneManager.OnFragmentTransitioned -= HandleFragmentTransitioned;
}
```

### MicroTween Struct

```csharp
public struct MicroTween
{
    public float Duration;
    public float Elapsed;
    public EaseType Ease;
    public float FromValue;
    public float ToValue;
    public Action<float> OnUpdate;
    public Action OnComplete;
    
    public float Evaluate()
    {
        float t = Mathf.Clamp01(Elapsed / Duration);
        return Mathf.Lerp(FromValue, ToValue, ApplyEase(t));
    }
    
    public bool IsComplete => Elapsed >= Duration;
}
```

Supported easing: `SineIn`, `SineOut`, `SineInOut`, `EaseOutCubic`, `EaseOutElastic`, `EaseOutBounce`

### OnFragmentTransitioned Integration

```
OnFragmentTransitioned(chapterKey, fragmentId):
  1. StopAllForFragment(oldFragmentId) — cancel tweens, reset MPB
  2. Read new fragment AmbientAnimInstances → Catalog lookup → merge params
  3. Create active animation instances → start loops/tweens
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: GPU Shader 动画（Shader Graph + MaterialPropertyBlock）
- Story 003: 性能降级 + 情绪驱动动画参数
- Story 004: L1/L2/L3 墨点发光系统
- 记忆碎片数据模型 (#8): AmbientAnimInstances 字段添加到 MemoryFragment
- 场景管理 (#6): OnFragmentTransitioned event

---

## QA Test Cases

- **AC-1**: Catalog lookup
  - Given: Catalog has "leaf_sway" AmbientAnimDef
  - When: Manager looks up "leaf_sway"
  - Then: Returns def with Speed=0.3, Amplitude=0.02
  - Edge cases: DefId not found → null + LogWarning; Catalog null → null

- **AC-2**: MicroTween evaluation
  - Given: Tween From=0, To=1, Duration=2s, Ease=SineInOut
  - When: Elapsed=1.0s → Evaluate()
  - Then: Returns ~0.5 (midpoint of eased curve); Elapsed=2.0s → returns 1.0, IsComplete=true
  - Edge cases: Duration=0 → immediate completion; From=To → constant value

- **AC-3**: Fragment transition lifecycle
  - Given: Old fragment A with 2 active tweens; transitioning to fragment B with 2 AmbientAnimInstances
  - When: OnFragmentTransitioned fires
  - Then: A's tweens cancelled (OnComplete not called); B's 2 ambient animations started
  - Edge cases: Fragment B has 0 AmbientAnimInstances → no animations, no error

- **AC-4**: Performance — 10 ambient tweens
  - Given: 10 active MicroTween instances running in Update()
  - When: Profiler measures CPU allocation per frame
  - Then: Zero GC allocation; <1ms CPU time
  - Edge cases: 0 active tweens → Update skip early return

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/micro-animation/catalog_manager_tween_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 场景管理 Story 002 (OnFragmentTransitioned event); 记忆碎片数据模型 Story 001 (MemoryFragment SO structure)
- Unlocks: Story 002 (GPU animation adds Shader Graph support)
