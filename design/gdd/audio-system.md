# 音频系统 (Audio System)

> **Status**: In Design
> **Author**: 用户 + audio-director + gameplay-programmer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 4 (画卷中有呼吸) — 间接支撑——声音是"呼吸感"的听觉维度

## Overview

音频系统是《回响》中所有声音的统一管理器。它封装 Unity Audio Mixer 的分组路由、音量控制和 Snapshot 过渡能力，为上层系统（交互反馈、场景管理、UI 框架）提供一致的声音播放接口——包括环境音（Ambience）、氛围音乐（Music）和交互音效（SFX）三种音频通道。音频系统不包含任何音频创作逻辑——它不决定"什么声音在什么时候播放"，只提供"如何播放、如何混合、如何过渡"的基础能力。

在技术层面，它是一个薄服务层：Audio Mixer 的 Master → SFX / Music / Ambience 三组路由结构，Addressables 通过 `Shared_Audio` 组加载共享音频资产，每章专属音频通过对应的 Art_Ch 组按需加载。所有音量控制通过 Exposed Parameters 暴露给设置菜单，音乐过渡通过 Snapshot 系统实现交叉淡出。

## Player Fantasy

在《回响》中，声音不是"播放"出来的——它是渗出来的。像毛笔落在宣纸上，墨从落笔处向四周晕开，每一种声音就这样渗入一段记忆的沉默。低沉的弦乐是浓墨，浸透悲伤的底色；清亮的风铃是枯笔，只在欢愉的边缘轻轻擦过。

风穿过画面边缘的竹叶，雨打在卷轴未展开的远处——这不是"背景音乐"，这是这幅画卷在呼吸。它在你踏入之前就在呼吸，在你离开后也不会停。只是你在场时，它的呼吸变得更清晰，像一个人感觉到注视时微微调整了坐姿。你听不到"音效结束"的瞬间——声音像墨一样，从来不真正消失，只是越渗越淡，直到成为下一段记忆的底色。在这座声音的砚台前，沉默不是空白——它是留白，和你眼前的画卷一样，未画之处最有声。

## Detailed Design

### Core Rules

**规则 1 — Audio Mixer 分组结构**：四级路由层级。

```
Master (根组)
├── SFX (子组)           — 交互音效通道
├── Music (子组)          — 氛围音乐通道
│   ├── Music_A (孙组)    — 音乐交叉淡出源 A
│   └── Music_B (孙组)    — 音乐交叉淡出源 B
└── Ambience (子组)       — 环境音通道
```

- SFX 组：所有池化的 SFX AudioSource 路由至此（10 个，规则 5）
- Music 组：不直接输出音频——仅承载共享的 Music 效果（EQ、Ducking）。两个孙组各挂一个专用 AudioSource
- Music_A / Music_B：各一个 AudioSource，持久存在。Snapshot 控制哪组被听到（规则 4）
- Ambience 组：一个持久 AudioSource，单层环境音（同一时间只有一个环境音）

AudioSource 分配：
- 2 个专用于 Music（`_musicSourceA`、`_musicSourceB`）— 持久，不销毁
- 10 个池化用于 SFX — 初始化时预热，播放完毕后回池
- 1 个持久用于 Ambience — 持久，不销毁

**规则 2 — 音频通道类型与命名约定**：MVP 阶段三种通道，不含语音。

| 通道 | 用途 | clipKey 前缀 | 示例 |
|------|------|-------------|------|
| SFX | 交互音效、UI 音效、一次性事件 | `sfx_` | `sfx_ui_hover_01`, `sfx_interact_touch_01` |
| Music | 章节/区域的背景音乐（循环） | `mus_` | `mus_ch01_memory_theme`, `mus_menu_title` |
| Ambience | 持续环境音（风、雨、墨韵白噪） | `amb_` | `amb_ch01_ink_resonance`, `amb_env_cave_drip` |

clipKey 直接映射到 Addressables 资源 Key。语音（Voice/Dialogue）预留给 Vertical Slice——届时在 Mixer 中新增 Dialog 子组，新增 `vo_` 前缀。

**规则 3 — 音频资产压缩与加载类型**：

| 资产类型 | 时长 | 压缩格式 | 加载类型 | 原因 |
|---------|------|---------|---------|------|
| SFX | <3s | ADPCM | DecompressOnLoad | 零解码延迟，内存极小（0.5s ADPCM 单声道 ≈ 80KB） |
| Ambience | 10–60s | Vorbis | CompressedInMemory | 内存可控，解码开销可接受 |
| Music | 2–5min | Vorbis | Streaming | 流式从磁盘读取，近零内存占用 |

**Streaming 约束**：Streaming 的 Music Clip 必须存在于不卸载的 Addressables 组中。`Shared_Audio` 始终驻留——Streaming 的音乐放于此组。`Audio_Ch` 组中的音乐使用 CompressedInMemory（因为章节切换时会卸载该组）。

**规则 4 — 音乐交叉淡出机制**：双 AudioSource + Snapshot 切换。

实现流程：
- `_musicSourceA` 路由到 `Music_A` 子组，`_musicSourceB` 路由到 `Music_B` 子组
- `_activeMusicSource` 字段追踪当前播放源（A 或 B）
- Mixer 中定义两个 Snapshot：
  - `Snapshot_MusicA`：Music_A 音量 = 0dB，Music_B 音量 = -80dB
  - `Snapshot_MusicB`：Music_B 音量 = 0dB，Music_A 音量 = -80dB
- `PlayMusic(clipKey, fadeTime)` 流程：
  1. 如果 clipKey 与当前相同 → 返回（幂等）
  2. 在非活跃源上加载新 clip，`LoadAudioData()` 确保就绪
  3. 非活跃源 `Play()` — 从 clip 起点开始
  4. `_activeSnapshot.TransitionTo(_targetSnapshot, fadeTime)` 执行交叉淡出
  5. 交换 `_activeMusicSource` 引用
  6. 淡出完成后停止旧源播放
- 如果当前无音乐播放：直接设置活跃源 clip + Play，跳过交叉淡出

**规则 5 — SFX 池化与复音策略**：对象池模式，避免运行时实例化/Destroy 的 GC 压力。

| 参数 | 值 | 说明 |
|------|-----|------|
| 池容量 | 10 | 同时可播放的最大 SFX 数 |
| 预热时机 | 初始化阶段 | `Initialize()` 中创建全部 10 个 AudioSource |
| 路由 | SFX Mixer 组 | 通过 Inspector 拖入 `outputAudioMixerGroup` 赋值 |

优先级窃取规则（池满时）：
1. 优先窃取播放进度最接近结束的（剩余 < 0.1s）
2. 若无接近结束的，窃取音量最低的
3. 若音量相同，窃取最早开始播放的

`PlaySFX(clipKey, worldPosition)` 流程：
1. 从池中获取空闲 AudioSource；若无空闲则执行优先级窃取
2. 设置 `source.clip = clip`，调用 `clip.LoadAudioData()` 确保就绪
3. 若 worldPosition 非空：`spatialBlend = 1.0f`，位置设为 worldPosition
4. 若 worldPosition 为空：`spatialBlend = 0.0f`（纯 2D UI 音效）
5. `source.Play()` → 播放结束后自动回池

**规则 6 — 音频优先级与闪避**：三层优先级 SFX > Music > Ambience。通过 Audio Mixer 的 Duck Volume 效果实现。

| 条件 | 行为 | 参数 |
|------|------|------|
| SFX 播放中 | Music 组 -10dB，Ambience 组 -6dB | Attack: 50ms, Release: 500ms |
| 无 SFX 播放 | Music 和 Ambience 恢复完整音量 | Duck Volume 自动释放 |

Release 时间 500ms 确保闪避恢复像"墨晕"般自然——不突兀回落。

**规则 7 — 音量控制架构**：四个线性音量参数暴露给设置菜单，`const string` 定义参数名防拼写错误。

| channel | Exposed Parameter | 默认值 | PlayerPrefs Key |
|---------|-------------------|--------|-----------------|
| `"master"` | `MasterVolume` | 1.0 (0dB) | `Audio_Master` |
| `"sfx"` | `SFXVolume` | 1.0 (0dB) | `Audio_SFX` |
| `"music"` | `MusicVolume` | 1.0 (0dB) | `Audio_Music` |
| `"ambience"` | `AmbienceVolume` | 1.0 (0dB) | `Audio_Ambience` |

Linear → dB 转换（带 Log10(0) 防护）：
```
dB = (linear <= 0.0001f) ? -80f : Mathf.Log10(linear) * 20f
linear = Mathf.Pow(10f, dB / 20f)
```

`SetVolume` 时同时设置 Mixer 参数 + 写入 PlayerPrefs（不等应用退出——防崩溃丢设置）。

**规则 8 — Addressables 音频资产加载**：沿用数据管理 GDD 的三态就绪模型，但由 AudioManager 内部追踪。

| 状态 | 含义 | 行为 |
|------|------|------|
| Cached | AudioClip 在内存中 | 同步播放 |
| Loading | 加载进行中 | Play 调用 await Task，完成后播放 |
| NotRequested | 未开始加载 | 自动启动加载，进入 Loading |

Asset 分组（Audio_Ch01-04 为新增组）：

| Addressables 组 | 音频内容 | 加载时机 | 卸载时机 |
|-----------------|---------|----------|----------|
| `Shared_Audio` | 共享 SFX（UI 音效）、基础环境音、Streaming 音乐 | 启动时 | 游戏退出 |
| `Audio_Ch01-04` | 章节专属音乐（CompressedInMemory）、章节专属环境音 | 章节入口预加载 | 章节退出 |

- Shared_Audio 中的所有 clip 在 Initializing 阶段全量预加载（20-30 个短音效 < 5MB）
- 并发请求同一 clip → 返回同一个 Task 引用（与数据管理 GDD 规则 9 一致）
- 所有 Addressables 加载包裹在 try/catch 中（Unity 6.2+ 加载失败抛异常）
- 加载失败 → 包装为 `AudioLoadException(clipKey, innerException)`

**规则 9 — AudioListener 管理**：持久单 AudioListener。

- AudioListener 挂载在 AudioManager 的 GameObject 上（`DontDestroyOnLoad`）
- 不在任何场景 Camera 上保留 AudioListener
- 场景加载时 AudioManager 扫描并禁用场景中的额外 AudioListener

**规则 10 — AudioMixerGroup Inspector 赋值**：所有 Mixer 组引用通过 Inspector 拖入序列化字段，不通过 `FindMatchingGroups` 字符串查找——避免拼写导致的静默失败。

### States and Transitions

| 状态 | 描述 | 触发 |
|------|------|------|
| **Uninitialized** | AudioManager 尚未创建 | 引擎启动 |
| **Initializing** | 加载 Audio Mixer + Shared_Audio + 创建 SFX 池 + 恢复音量 | Uninitialized → 自动 |
| **Ready** | Shared_Audio 就绪，可处理所有播放请求 | 初始化成功 |
| **LoadingChapterAudio** | 后台加载 Audio_Ch 组音频 | Ready 下调用 PreloadChapterAudioAsync() |
| **Error** | 关键资产加载失败 | 任意状态的致命异常 |

**状态转换**：
- Uninitialized → Initializing（自动）
- Initializing → Ready（成功）/ → Error（Shared_Audio 或 Mixer 失败）
- Ready ↔ LoadingChapterAudio（预加载开始/完成）
- Any → Error（致命故障）

**子状态**：
- **MasterMuted**（Ready 以下）：游戏窗口失焦 → `AudioListener.pause = true`。恢复焦点时恢复
- **ChapterAudioDegraded**（Ready 以下）：章节音频部分缺失 → 缺失项静音替代，已有项正常播放

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 下游 | **交互反馈** (#18) | `PlaySFX(clipKey, worldPosition)`, `PlayAmbience(clipKey)` | 物件触碰、选择确认、悬浮高亮音效。UI 音效也由 #18 转发——UI 框架 (#5) 不直接调用 AudioManager |
| 下游 | **场景管理** (#6) | `PlayMusic(clipKey, fadeTime)`, `StopMusic(fadeTime)`, `PreloadChapterAudioAsync(chapterKey)`, `UnloadChapterAudio(chapterKey)` | 章节过渡音乐交叉淡出，章节入口预加载 |
| 下游 | **主菜单** (#19) | `SetVolume(channel, volume)`, `GetVolume(channel)` | 设置界面四个音量滑块 |
| 上游 | **数据管理** (#2) | 提供 Addressables 组定义 | 非运行时依赖——音频系统直接用 Addressables API 加载。两者在组名约定上需协调 |

## Formulas

音频系统仅包含两条转换公式。

### Linear ↔ dB 转换

**变量定义**：
- `linear`：线性音量值，范围 [0.0, 1.0]，0 = 静音，1 = 最大
- `dB`：分贝值，范围 [-80, 0]，-80 = 静音，0 = 最大

**转换公式**：

```
// Linear → dB（写，slider → mixer parameter）
dB = (linear ≤ 0.0001) ? -80.0 : 20 × log₁₀(linear)

// dB → Linear（读，mixer parameter → slider display）
linear = 10^(dB / 20)
```

**防护说明**：`log₁₀(0) = -∞`。0.0001 的阈值对应约 -80dB——人耳感知的静音阈值。低于此值的 linear 输入统一 clamp 到 -80dB。

**示例**：
| linear | dB | 感知 |
|--------|-----|------|
| 1.0 | 0 | 最大音量 |
| 0.5 | -6.0 | 半响度（人耳感知的"一半"） |
| 0.1 | -20.0 | 安静 |
| 0.01 | -40.0 | 几乎听不见 |
| 0.0 | -80.0 | 静音 |

## Edge Cases

- **如果 Audio Mixer 资源文件缺失或损坏**：`Resources.Load<AudioMixer>` 返回 null → AudioManager 进入 Error 状态，广播 `OnAudioError` 事件，游戏可在静音状态下运行但持续显示音频错误图标。Shared_Audio 加载失败同理
- **如果玩家在音乐交叉淡出期间触发新的 PlayMusic 调用**：`TransitionTo` 从当前插值状态开始新过渡——快速连续调用可能产生可感知的不平滑。AudioManager 限制同一音乐源每秒最多 1 次过渡触发
- **如果 10 个 SFX 都在播放且第 11 个触发**：优先窃取播放进度最接近结束的；如果没有接近结束的，窃取音量最低的；如果音量相同，窃取最早的。被窃取的 AudioSource 立即停止并分配给新请求
- **如果 Streaming 音乐被放在 Audio_Ch 组中（卸载时会释放该组）**：Streaming AudioClip 在 Addressables 组卸载后失效 → 下一帧 AudioSource 静音或报错。通过 AudioManager 内部校验阻止此配置——Streaming clipKey 只从 Shared_Audio 加载
- **如果场景中额外存在 AudioListener**：场景加载时 AudioManager 扫描并禁用所有非自身的 AudioListener。如果漏网（如第三方插件 Camera），Unity 会报 Warning，音频仍然工作（选一个 Listener）
- **如果章节预加载与章节切换之间的时间极短（玩家快速跳过）**：`PreloadChapterAudioAsync` 可能未完成。场景管理器在章节过渡时 await 该 Task——如果已完成则立即返回，如果进行中则等待完成
- **如果同一帧内两个系统请求同一个尚未加载的音频 clip**：两个调用方收到同一个 Task 引用——不发起重复的 Addressables 请求。Task 完成时双方同时收到结果
- **如果游戏窗口失焦时 SFX 正在播放**：`AudioListener.pause = true` 暂停所有输出（Unity 行为：AudioListener.pause 影响所有 AudioSource）。恢复焦点时 `AudioListener.pause = false`，正在播放的 AudioSource 从暂停位置继续
- **如果 PlayerPrefs 中无音量设置（首次启动）**：四个通道默认值为 1.0 (0dB)。不需要显示任何提示——静默使用默认值
- **如果 AudioClip.LoadAudioData() 失败**：抛出 `AudioLoadException(clipKey)`，AudioManager 进入 Error 状态。关键音频（Shared_Audio）失败是硬错误；章节音频失败进入 ChapterAudioDegraded 子状态

## Dependencies

**硬依赖（系统无法运行）**：无。音频系统是 Foundation 层系统，无上游运行时依赖。

**设计协调依赖（Design Coordination）**：

| 系统 | 依赖性质 | 协调内容 |
|------|----------|----------|
| 数据管理 (#2) | Addressables 组命名约定 | Shared_Audio 组包含共享音频资产；需新增 Audio_Ch01-04 组 |

**下游系统（依赖本系统）**：

| 系统 | 依赖性质 | 接口 |
|------|----------|------|
| 交互反馈 (#18) | 硬依赖 | PlaySFX, PlayAmbience |
| 场景管理 (#6) | 硬依赖 | PlayMusic, StopMusic, PreloadChapterAudioAsync, UnloadChapterAudio |
| 主菜单 (#19) | 硬依赖 | SetVolume, GetVolume |

> **注意**: UI 框架 (#5) 不直接调用 AudioManager 的 PlaySFX——UI 音效请求通过交互反馈系统 (#18) 转发。

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| SFX Pool Size | 10 | 4–20 | 同时播放的最大 SFX 数。增加 = 更多复音但更多内存。叙事游戏极少需要 >10 |
| Music Crossfade Duration | 2.0s | 0.5–5.0s | 音乐曲目间交叉淡出时长。与"墨晕"审美一致——声音不应突然切换 |
| Ambience Crossfade Duration | 3.0s | 1.0–6.0s | 环境音之间的过渡时长。比音乐过渡更长——环境变化应是潜移默化的 |
| Duck Amount (Music) | -10dB | -6dB to -20dB | SFX 播放时 Music 组的闪避量 |
| Duck Amount (Ambience) | -6dB | -3dB to -12dB | SFX 播放时 Ambience 组的闪避量 |
| Duck Attack Time | 50ms | 20–200ms | 闪避启动速度。太快 = 突兀；太慢 = SFX 被掩盖 |
| Duck Release Time | 500ms | 200–1000ms | 闪避恢复速度。与"墨晕"风格一致——缓慢回升 |
| Master Volume | 1.0 (0dB) | 0.0–1.0 | 主音量默认值 |
| Music Volume | 1.0 (0dB) | 0.0–1.0 | 音乐音量默认值 |
| SFX Volume | 1.0 (0dB) | 0.0–1.0 | 音效音量默认值 |
| Ambience Volume | 1.0 (0dB) | 0.0–1.0 | 环境音音量默认值 |

## Visual/Audio Requirements

音频系统本身不产生视觉输出。其音频输出的质量标准：

- Music 交叉淡出必须无间隙（双源架构保证）——过渡期间不应听到静音或咔嗒声
- SFX 延迟：从 `PlaySFX` 调用到声音可闻 < 50ms（Cached 状态）。Loading 状态的首次播放延迟取决于 Addressables 加载速度
- 所有循环音效（Music、Ambience）必须在导入设置中勾选 "Loop"，确保无缝循环无接缝
- Perceived loudness 一致性：任意两个同通道的音频 clip 的 RMS 响度偏差在 ±3dB 以内（由音频资产制作保证，非系统运行时责任）

## UI Requirements

音频系统仅有一个间接 UI 需求：音量滑块。该滑块属于设置菜单（主菜单与菜单系统 #19）。音频系统暴露 `SetVolume(channel, volume)` 和 `GetVolume(channel)` 供设置菜单调用。

无独立音频 UI。

## Acceptance Criteria

- **GIVEN** 游戏启动，**WHEN** 引擎完成初始化，**THEN** AudioManager 自动加载 Audio Mixer 和 Shared_Audio 组全部音频资产，创建 10 个 SFX AudioSource 池，从 PlayerPrefs 恢复音量设置。Shared_Audio 加载时间 < 3 秒（20-30 个短文件 < 5MB）
- **GIVEN** AudioManager 处于 Ready 状态，**WHEN** 调用 `PlaySFX("sfx_ui_click_01")`，**THEN** 音效从池中 AudioSource 播放，Cached 状态下延迟 < 50ms。播放完毕后 AudioSource 回池
- **GIVEN** 当前播放 `mus_ch01_memory_theme`，**WHEN** 调用 `PlayMusic("mus_ch02_opening_theme", 2.0f)`，**THEN** 音乐在 2 秒内交叉淡出到目标曲目。过渡期间无静音间隙
- **GIVEN** AudioManager 处于 Ready 状态，**WHEN** 调用 `SetVolume("music", 0.5)`，**THEN** MusicVolume Exposed Parameter 设为约 -6dB。该值立即写入 PlayerPrefs 的 `Audio_Music` key
- **GIVEN** AudioManager 处于 Ready 状态，所有 10 个 SFX 都在播放中，**WHEN** 调用 `PlaySFX("sfx_new_01")`，**THEN** 最不关键（最早/最轻/最接近结束）的 SFX 被停止释放，新 SFX 在其池位上播放
- **GIVEN** 游戏窗口失焦，**WHEN** AudioListener.pause 设为 true，**THEN** 所有音频输出暂停。恢复焦点时从暂停位置继续
- **GIVEN** 章节 1 进行中，**WHEN** 场景管理调用 `PreloadChapterAudioAsync("ch2")`，**THEN** Audio_Ch02 组的音频资产开始后台加载。AudioManager 进入 LoadingChapterAudio 状态，Loading 状态下 Ready 状态的核心功能不受影响
- **GIVEN** 玩家在设置界面拖动音乐音量滑块到 0，**WHEN** 值为 0.0，**THEN** MusicVolume 设为 -80dB。`PlayMusic` 调用仍然成功但不产生可闻输出
- **GIVEN** Audio Mixer 资源文件缺失，**WHEN** 游戏启动并尝试加载，**THEN** AudioManager 进入 Error 状态，广播 `OnAudioError` 事件，游戏可继续运行但显示音频错误图标
- **GIVEN** 两个系统同时请求加载同一个尚未缓存的 clipKey，**WHEN** 第一个请求触发 `Addressables.LoadAssetAsync`，**THEN** 两个调用方收到同一个 Task 引用——不发起重复加载

## Revision Notes

- **W2 (2026-05-19)**: ✅ 已验证 ADR-0014 实现：UI 框架 (#5) 不直接调用 PlaySFX。所有 UI 音效请求通过交互反馈系统 (#18) 转发。GDD 第247行已正确记录此设计。

## Open Questions

- **语音（Voice/Dialogue）的通道架构**：MVP 不含语音，但 Vertical Slice 可能加入。如果加入，需在 Mixer 中新增 Dialog 子组，音频优先级需重新考虑（语音 > SFX > Music > Ambience）。当前 Music 组下方的双源结构是否扩展到 Dialog（即 Dialog_A / Dialog_B 用于对话交替）？
- **章节专属音乐是否使用 Streaming**：当前设计中 Streaming 音乐仅放 Shared_Audio（因为 Streaming clip 在组卸载后失效）。如果某章音乐特别长（>5min）且仅在该章播放，是否值得单独在 Audio_Ch 组中使用 CompressedInMemory？CompressedInMemory 的 2-3 分钟曲目内存开销约 5-10MB——在 PC 2GB 预算下可接受
- **Ambience 是否应支持多层**：当前 Ambience 是单层（同一时间一个环境音）。如果 Vertical Slice 需要复杂环境（如风声 + 雨声 + 远处雷声），是否需要多 Ambience 层（2-3 个同时播放的环境 AudioSource）？
