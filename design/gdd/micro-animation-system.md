# 微动画系统 (Micro-Animation System)

> **Status**: Designed (pending review)
> **Author**: 用户 + technical-artist + art-director + creative-director
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 4 (画卷中有呼吸) — 直接支撑——微动画是"静中之动"的物理实现

## Overview

微动画系统是《回响》中 Pillar 4（画卷中有呼吸）的物理引擎。它定义了记忆画卷中"静中之动"的全部可能性——物件在触碰前微微发光、风吹过树叶的摇曳、水面上光影的缓慢移动、尘埃在光线中浮沉。没有它，每一幅记忆画卷都是静态插图——精美，但没有呼吸。

在技术层面，微动画系统是一个轻量的动画调度器：它不创建复杂的 Animator 状态机，而是管理一组预定义的微动画类型（循环环境动画 + 一次性触发动画 + 交互反馈动画），在碎片切换时通过场景管理器的 `OnFragmentTransitioned` 事件自动启动，在碎片离开时停止。每个动画类型映射到具体的 Unity 实现（Transform 补间、Sprite 切换、材质属性动画、UI Toolkit transition），并有明确的性能预算——60fps 下每碎片动画预算 < 2ms。

## Player Fantasy

每一个记忆碎片都有一颗还在跳动的心脏——你看不见它，但能感觉到它的脉搏。水面上的光在缓慢移动，纸的边缘微微卷起，尘埃在你"走过"的地方轻轻扬起。这些微小的运动不是装饰——它们是这段记忆**还在那里**的证据。

玩家感受到的是一种**温柔的警觉**——这幅画在呼吸，意味着它活着，也意味着它在消逝。越温暖的记忆，动画越柔和饱满；越遗憾的记忆，动画越迟滞残缺。微动画是记忆的脉搏计——当你在不同碎片之间穿行，脉搏的节奏也在变化。你触碰一个物件、改变一段记忆——脉搏随之变快或变慢。你不用理解动画系统——你只需感受：**有些记忆的心跳比你想象的更脆弱**。

## Detailed Design

### Core Rules

**规则 1 — 三种动画类别**

| 类别 | 触发 | 持续时间 | 可见度 | 用途 |
|------|------|---------|--------|------|
| **循环环境动画 (Ambient)** | 碎片进入时自动启动 | 始终运行 | 需凝视 3-5s 发现 | 风、水、光、尘——画卷的"呼吸" |
| **一次性触发动画 (Triggered)** | 玩家交互触发 | 0.6–2.5s | 可注意但不抢眼 | 花瓣落、窗开、墨晕——记忆的回应 |
| **交互反馈动画 (Feedback)** | 悬停/点击 | 持续/0.15s | 1-2s 内必须能发现 | 物件发光/颤动——"这里可以碰" |

**规则 2 — MicroAnimationCatalog：动画类型定义的全局资产**

```
MicroAnimationCatalog (ScriptableObject, 全局唯一)
├── AmbientAnimDef[]          — 循环环境动画定义
│   ├── DefId: "leaf_sway"
│   ├── Category: Ambient
│   ├── Implementation: Shader_VertexDisplace | Tween_Loop | SpriteSwap
│   ├── DefaultSpeed: 0.3
│   ├── DefaultAmplitude: 0.02
│   ├── DefaultEasing: EaseType.SineInOut
│   ├── ShaderPropertyName: "_SwayOffset"  — (Shader类)
│   └── ShaderPropertyRange: [0.0, 1.0]    — (Shader类)
│
├── TriggeredAnimDef[]        — 一次性触发动画定义
│   ├── DefId: "petal_fall"
│   ├── Implementation: TweenSequence | Shader_Progress | SpriteSwap
│   ├── Duration: 2.5s
│   ├── Easing: EaseType.EaseOutCubic
│   └── TweenSteps[] (TweenSequence类)
│
└── FeedbackAnimDef[]         — 交互反馈动画定义
    ├── DefId: "object_glow"
    ├── Implementation: Shader_MaterialPulse | Tween_OneShot | UIToolkit_Transition
    ├── Duration: 循环 (Shader类) / 0.15s (Tween类)
    └── ShaderPropertyName: "_GlowIntensity"
```

- Catalog 在 Boot 场景加载一次，全局常驻
- 动画参数属于技术资产，独立于叙事数据

**规则 3 — MemoryFragment 中的动画引用**

MemoryFragment SO 新增 `AmbientAnimInstances` 字段（追加到数据模型 GDD #8）：

| 字段 | C# 类型 | 必填 | 说明 |
|------|---------|------|------|
| `AnimDefId` | `string` | 是 | 引用 MicroAnimationCatalog 中的 DefId |
| `TargetObjectId` | `string` | 否 | 动画作用的目标物件。为空则作用于基础插图 |
| `SpeedOverride` | `float?` | 否 | 覆盖默认速度。null = 使用 Catalog 默认值 |
| `AmplitudeOverride` | `float?` | 否 | 覆盖默认幅度 |

InteractiveObject.OnInteract 的 AnimationId 引用 Catalog 中的 TriggeredAnimDef。

**规则 4 — MicroAnimationManager：运行时单例**

```csharp
public class MicroAnimationManager : MonoBehaviour
{
    Dictionary<string, List<ActiveAnimation>> _activeAnimations;
    List<MicroTween> _activeTweens;           // 值类型结构体，零堆分配
    List<MicroTweenLoop> _activeLoops;
    Dictionary<string, List<ShaderAnimEntry>> _shaderDrivenAnims;
    ObjectPool<SpriteRenderer> _particlePool;
}
```

- 单例 MonoBehaviour，驻留 Game 场景。每帧 Update 统一 tick 所有活跃 MicroTween
- 自定义 `MicroTween` 值类型——`struct MicroTween { float start, end, duration, elapsed; EaseType ease; Action<float> onUpdate; Action onComplete; }`
- 不引入 DOTween/LeanTween——自定义实现 ~250 行，零外部依赖，零 GC 分配

**规则 5 — GPU/CPU 动画分工**

| GPU (Shader Graph 材质属性) | CPU (Transform 补间/C#) |
|---------------------------|------------------------|
| 树叶摇曳 (vertex displace) | 云朵漂移 (position + speed*dt) |
| 水面光影 (UV scroll) | 花瓣落下 (pos + rot sequence) |
| 物件发光/脉冲 (material property) | 尘埃浮沉 (sin Y displacement) |
| 墨迹晕开 (mask progress) | 点击反馈 (scale punch) |
| 色调渐变 (color lerp) | 物件出现/消失 (scale + fade) |
| 烛光明暗 (brightness noise) | — |

- GPU 动画：CPU 仅需每片段设一次 `MaterialPropertyBlock.SetFloat`
- CPU 动画：自定义 MicroTween 在 Update 中统一 tick

**规则 6 — 性能预算与降级**

| 等级 | 条件 | 策略 |
|------|------|------|
| **High** (默认) | Frame time ≤ 14ms | 全动画，60fps 更新 |
| **Medium** | Frame time > 14ms | 循环环境动画降至 30fps（每 2 帧 tick） |
| **Low** | Frame time > 20ms | 仅保留交互反馈动画；环境动画暂停 |
| **Minimal** | 检测到集成显卡 | 仅保留点击确认反馈 |

- 每碎片动画总预算 < 2ms。性能降级静默触发——不弹出通知

**规则 7 — OnFragmentTransitioned 集成**

```
OnFragmentTransitioned(chapterKey, fragmentId) 触发:
│
├── 1. StopAllForFragment(oldFragmentId)
│     ├── Cancel 所有活跃 MicroTween（调用 onComplete）
│     ├── 重置 MatPropBlock 到默认值
│     ├── 归还对象池粒子
│     └── 从 _activeAnimations 移除
│
├── 2. 读取新碎片 AmbientAnimInstances
│     ├── 遍历实例 → Catalog 查找 Def
│     ├── 合并参数 (Default × Override)
│     ├── Shader_* → 注册到 _shaderDrivenAnims
│     ├── Tween_Loop → 加入 _activeLoops
│     └── SpriteSwap → 启动协程
│
└── 3. _currentFragmentId = fragmentId
```

- 快速连续调用（双击物件）→ StopAll 空列表安全空操作 + Start 幂等跳过
- 动画在 FadeIn 开始前启动——玩家看到的第一帧就是"在呼吸"的完全体

**规则 8 — 情绪驱动的动画参数**

动画表现随记忆的情绪状态变化：

| 参数 | 温暖记忆 | 遗憾记忆 | 恐惧记忆 |
|------|---------|---------|---------|
| 速度 | 偏慢绵长 | 迟滞断续（漏拍感） | 骤起骤停 |
| 幅度 | 宽展饱满 | 收束局促 | 不可预测 |
| 流畅度 | 连续正弦 | 不规则间隔 | 高加速度 |
| 颜色偏移 | +5~10% 饱和度 | −5% 饱和度 + 边缘模糊 | 对比度飙升后速降 |
| 笔触对应 | 湿染 (wet wash) | 枯笔 (dry brush) | 墨溅 (ink splatter) |

- 情绪状态由情感标签系统 (#10) 的主标签驱动。参数配置集定义在 Catalog 的 `EmotionPreset[]` 中

**规则 9 — 物件发光：朱砂墨点，不是霓虹光**

| 层级 | 视觉 | 触发 |
|------|------|------|
| **L1 — 静态邀请** | 物件旁朱砂墨点 (Vermilion Ink `#A03828`)，直径 4-6px，笔触纹理 | 物件 DefaultState = Active |
| **L2 — 呼吸脉动** | 墨点 opacity 100%↔75%↔100%，2.5s 周期正弦。周围淡朱砂水渍晕染（opacity 8-12%） | 玩家鼠标悬停 |
| **L3 — 物件内光** | 物件颜色向暖端偏移（色温 +300K 等效），饱和度 +5-10%——颜料被光线穿透的质感 | 选择时刻 |

**绝对禁止**: 外发光 (outer glow)、霓虹色、几何圆形光晕、纯白高光、>1Hz 闪烁。

**规则 10 — 章节动画配置集**

| 参数 | 童年（春） | 青年（夏） | 暮年（冬） |
|------|-----------|-----------|-----------|
| 每碎片循环动画数 | 1–3 | 1–2 | 0–1 |
| 帧率 (fps) | 10–12 | 8–10 | 6–8 |
| 循环周期 (s) | 0.5–0.8 | 0.8–1.0 | 1.0–1.5 |
| 运动幅度 (px) | 4–12 | 3–8 | 2–4 |
| 缓动类型 | 弹性缓出 | 平滑缓入缓出 | 缓入为主 |
| 静动比 | 85:15 | 90:10 | 95:5 |
| 颜色偏移幅度 | ±10–15% 饱和度 | ±5–10% | ±3–5% |

- 同章内碎片越靠后动画越收敛——渐变而非突变
- 暮年章某些碎片仅有一个"色温在 30s 内缓慢偏移 2-3%"的动画——几乎看不见

**规则 11 — 静止的例外：结局与沉重碎片**

Pillar 4 "10 秒规则"的两个刻意例外：

1. **结局画面**: 卷轴边框收拢后——画面进入绝对静止。动结束了，记忆安息了
2. **重量级悲伤碎片**: 空房间、已故者的座位——静止本身就是动画，缺席本身就是存在
3. 每章节最多 1 个碎片可打破 10 秒规则（不含结局）。超过需设计师书面理由

### States and Transitions

| 状态 | 描述 | 触发 |
|------|------|------|
| **Idle** | 无碎片活跃。等待第一个 OnFragmentTransitioned | Boot 完成 |
| **Running** | 当前碎片环境动画运行中 | 碎片切换完成 |
| **PerformanceDegraded** | 降级模式——部分动画暂停或降帧 | 帧时间超过阈值 |
| **Paused** | 所有动画暂停 | 暂停菜单打开 / 场景过渡中 |

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **场景管理 (#6)** | `OnFragmentTransitioned(chapterKey, fragmentId)` | 碎片切换时启动/停止动画 |
| 上游 | **记忆碎片数据模型 (#8)** | `AmbientAnimInstances` 列表 + `InteractiveObject.OnInteract.AnimationId` | 读取动画引用 |
| 上游 | **情感标签系统 (#10)** | 当前碎片的主情感标签 → 情绪类型 | 选择情绪驱动的动画参数集 |
| 下游 | **记忆画卷交互系统 (#11)** | 悬停/点击事件 → 触发 Feedback 动画 | L2/L3 发光层级的触发 |

## Formulas

**缓动函数集**（MicroTween 支持的缓动类型）:

| 函数 | 用途 |
|------|------|
| `SineIn`, `SineOut`, `SineInOut` | 循环环境动画——平滑无冲击 |
| `EaseOutCubic` | 一次性触发动画——自然减速收尾 |
| `EaseOutElastic` | 点击确认反馈——回弹感 |
| `EaseOutBounce` | 物件落下（花瓣、叶子） |

**情绪-参数映射**（基于主情感标签的乘数）:

| 参数 | 温暖 | 遗憾 | 恐惧 |
|------|------|------|------|
| `speedMultiplier` | 0.8 | 0.6 | 2.0 |
| `amplitudeMultiplier` | 1.3 | 0.7 | 1.5 |
| `intervalPattern` | `regular` | `irregular(skipRate: 0.15)` | `burst(cluster: 2-3, gap: 2-5s)` |
| `colorShift` | saturation +8% | saturation -5%, blur +2px | contrast +20%, duration 0.3s |

**性能降级阈值**:

| 条件 | 等级 | 行为 |
|------|------|------|
| `frameTime ≤ 14ms` | High | 全动画 60fps |
| `14ms < frameTime ≤ 20ms` | Medium | 环境动画降至 30fps |
| `frameTime > 20ms` | Low | 仅保留交互反馈动画 |

**GPU 参数更新**: 每碎片每帧 `MaterialPropertyBlock.SetFloat("_FragmentTime", time)` — 1 次调用覆盖该碎片所有共享此 Block 的 Sprite。

## Edge Cases

- **如果碎片定义了 AmbientAnimInstances 但引用的 DefId 在 Catalog 中不存在**：编辑器验证报错"动画 DefId [id] 不存在于 MicroAnimationCatalog"。运行时跳过该实例，记录 Warning。

- **如果两个 AmbientAnimInstance 同时作用于同一 TargetObjectId**：允许叠加。但类型冲突时（两个 Shader 动画修改同一材质属性），后启动者覆盖前者。不抛异常。

- **如果触发动画播放中玩家快速切换到下一碎片**：StopAllForFragment 取消所有活跃 MicroTween——onComplete 不回调。触发动画关联的 ContentChange 由记忆变化追踪系统 (#12) 管理——与动画取消无关。

- **如果玩家在碎片过渡的 0.5s FadeOut 期间悬停在物件上**：过渡期间 Action Map 为 Inactive——输入事件不被处理。FadeIn 完成后才启用交互——此时才可能触发 L2 悬停动画。

- **如果性能降级从 Low 恢复到 High**：自动恢复全动画——下个碎片切换时重新启动环境动画。不在当前帧立即恢复（防止抖动）。

- **如果 MicroAnimationCatalog SO 在运行时被卸载**：不应发生——Catalog 标记为 `DontDestroyOnLoad`。如果发生（Addressables 异常），MicroAnimationManager 进入 Idle 状态，所有动画请求返回空操作。

- **如果 ParticlePool 耗尽**：MVP 阶段对象池容量为 20 个粒子。耗尽时分配新 SpriteRenderer（非池化）——记录 Warning"粒子池耗尽"，不阻塞动画。

- **如果 Shader Graph 材质属性在目标平台上不可用**：运行时检测 `SystemInfo.supportsComputeShaders`。若不可用，Shader 类动画降级为 CPU Tween 替代。若替代不存在——跳过该动画。

- **如果碎片没有任何 AmbientAnimInstances（空列表）**：合法——这是一个"完全静止"的碎片。若碎片既无动画又不在 10 秒规则例外列表中——编辑器验证警告。

- **如果 Application.runInBackground = true（编辑器后台）**：动画使用 `Time.deltaTime`——受 timescale 影响，暂停时自动停止（deltaTime = 0）。

## Dependencies

**硬依赖**:

| 系统 | 依赖性质 | 接口 |
|------|----------|------|
| **场景管理 (#6)** | 硬依赖 | `OnFragmentTransitioned(chapterKey, fragmentId)` — 碎片切换时启动/停止动画 |
| **记忆碎片数据模型 (#8)** | 硬依赖 | `AmbientAnimInstances`, `InteractiveObject.OnInteract.AnimationId` — 读取动画引用 |

**软依赖**:

| 系统 | 依赖性质 | 接口 |
|------|----------|------|
| **情感标签系统 (#10)** | 软依赖 | 主情感标签 → 情绪类型 — 选择情绪驱动的动画参数集。若 #10 未就绪，使用默认（温暖）参数集 |
| **记忆画卷交互系统 (#11)** | 软依赖 | 悬停/点击事件 — 触发 L2/L3 发光动画。若 #11 未就绪，L1 静态墨点仍正常显示 |

**下游系统**: 记忆画卷交互系统 (#11) — 依赖本系统提供物件发光和触发动画。

**双向一致性**: 场景管理 GDD (#6) Interactions 表中已列出"下游: 微动画 (#9)"——方向匹配 ✅

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 碎片动画总帧预算 | 2ms | 1–4ms | 超过则触发降级 |
| 高性能阈值 | 14ms frame time | 12–16ms | 低于此值 = High 模式 |
| 低性能阈值 | 20ms frame time | 16–24ms | 超过此值 = Low 模式 |
| 对象池容量 | 20 particles | 10–50 | 尘埃/花瓣粒子的最大同时数量 |
| 墨点发光周期 | 2.5s | 1.5–4.0s | L2 呼吸脉动的正弦周期 |
| 墨点直径 | 5px | 3–8px | L1 朱砂墨点的大小 |
| 触发动画最大并发数 | 3 | 1–5 | 同时播放的触发动画上限 |
| 10 秒规则例外/章 | 1 | 0–2 | 不含结局画面 |

## Visual/Audio Requirements

本系统的视觉输出已集成到 Core Rules 中（规则 9 — 物件发光设计，规则 10 — 章节动画配置集）。

**关键视觉规范**:
- 物件发光: 朱砂墨点 (#A03828)，笔触纹理，禁止外发光/霓虹/几何光晕
- 循环环境动画: 2-8px 位移，0.5-1.0s 周期，需凝视 3-5s 才能发现
- 触发动画: ≤画面 10% 区域，0.6-1.2s 持续，缓出收尾
- 结局画面: 绝对静止——卷轴边框收拢后所有动画停止

本系统不产生音频输出。所有音频反馈（触碰音效、过渡音效）由音频系统 (#3) 和交互反馈系统 (#18) 负责。

## UI Requirements

微动画系统本身不包含玩家可见 UI。交互反馈动画（物件发光/颤动）在画面层渲染——不使用 UI Toolkit。编辑器工具（MicroAnimationCatalog Inspector）属于开发工具。

## Acceptance Criteria

- **GIVEN** 玩家进入一个碎片（OnFragmentTransitioned 触发），**WHEN** 碎片有 2 个 AmbientAnimInstances，**THEN** 两个循环环境动画在 FadeIn 开始前启动。玩家看到的第一帧画面中有动画在运行。

- **GIVEN** 一个碎片定义了 "leaf_sway" 环境动画（Shader_VertexDisplace 类型），**WHEN** 碎片已展示 5 秒，**THEN** 树叶 Sprite 的顶点持续以正弦波位移——幅度和速度与 Catalog 定义一致。CPU 侧除 MaterialPropertyBlock.SetFloat 外无 per-frame 开销。

- **GIVEN** 玩家悬停在一个可交互物件上，**WHEN** 鼠标进入物件的碰撞区域，**THEN** 物件旁的朱砂墨点进入 L2 呼吸脉动（opacity 100%↔75%↔100%，2.5s 周期）。鼠标离开后墨点恢复 L1 静态状态。

- **GIVEN** 玩家点击一个物件并触发了 ChoiceGroup，**WHEN** 选择面板展示，**THEN** 物件进入 L3 内光状态——颜色向暖端偏移（色温 +300K），饱和度 +5-10%。选择完成后 L3 效果在 0.5s 内淡出。

- **GIVEN** 玩家正在碎片 A 中观看一个触发动画（petal_fall, 2.5s），**WHEN** 动画播放到 1.0s 时玩家触发碎片切换到 B，**THEN** 碎片 A 的所有活跃动画（包括 petal_fall）在过渡中被取消。碎片 B 的环境动画正常启动。无残留动画从 A 泄漏到 B。

- **GIVEN** 帧时间持续超过 14ms（Medium 降级），**WHEN** 下一帧 tick 运行，**THEN** 循环环境动画的更新频率降至每 2 帧一次（30fps）。交互反馈动画保持 60fps。不弹出通知。

- **GIVEN** MicroAnimationCatalog 中定义了 "object_glow" Feedback 动画（Shader_MaterialPulse），**WHEN** 多个碎片中的不同物件引用此 DefId，**THEN** 所有物件使用相同的脉冲周期和幅度——参数由 Catalog 统一控制。修改 Catalog 中的 DefaultSpeed 会影响所有引用碎片。

- **GIVEN** 一个碎片没有任何 AmbientAnimInstances 且被标记为"重量级悲伤碎片"，**WHEN** 玩家进入该碎片，**THEN** 画面完全静止。10 秒后不触发编辑器警告。碎片在其他方面功能正常（交互物件仍可触碰）。

- **GIVEN** 暮年章碎片的 AmbientAnimInstances 使用章节默认参数配置集，**WHEN** 循环动画运行，**THEN** 帧率 6-8fps，循环周期 1.0-1.5s，运动幅度 2-4px。明显慢于童年章的同等动画。

- **GIVEN** ParticlePool 的 20 个粒子全部在使用中，**WHEN** 需要第 21 个粒子用于尘埃动画，**THEN** 分配非池化的新 SpriteRenderer，记录 Warning 日志。游戏继续正常运行。

## Open Questions

- **Shader Graph vs 手写 HLSL**: 当前方案假设使用 Shader Graph（URP 2D SpriteLit 模板）。如果项目 URP 2D Renderer 使用 Sprite-Unlit-Default 且无法修改，Shader 动画部分需通过 MaterialPropertyBlock + ScriptableRenderFeature 实现。需要在架构阶段确认 URP 2D 的 Sprite 材质配置。（Owner: technical-artist + lead-programmer）

- **MemoryFragment SO 新增字段**: 本系统需要 MemoryFragment 新增 `AmbientAnimInstances` 列表字段。这需要更新记忆碎片数据模型 GDD (#8) 的 Schema。应在开始 #9 实现前同步更新 #8。（Owner: game-designer）

- **集成显卡检测**: Minimal 性能等级的触发条件"检测到集成显卡"在 Unity 6.3 中的具体检测方式——`SystemInfo.graphicsDeviceType` + `SystemInfo.graphicsDeviceVendor` 的可靠组合需要验证。（Owner: technical-artist）

- **MicroTween vs DOTween**: 当前选择自定义 MicroTween（零依赖）。如果团队标准化使用 DOTween 用于其他系统（如 UI 过渡），可替换为 DOTween 以统一动画管线——但会增加 Package Manager 依赖。在架构阶段做最终决定。（Owner: lead-programmer）

- **章节动画配置的存储位置**: 规则 10 的章节动画配置集（帧率、周期、幅度等）是存储在 MicroAnimationCatalog 中，还是存储在 ChapterDefinition SO 中？Catalog 方案让 technical-artist 独立调优；ChapterDefinition 方案让章节作者可覆盖。（Owner: game-designer + technical-artist）

