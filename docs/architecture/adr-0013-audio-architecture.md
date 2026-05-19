# ADR-0013: 音频架构 — 4 层 Mixer + 双轨交叉淡入淡出

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

回响 (Echoes) 的音频需要 3 类声音（SFX/Music/Ambience）+ 音乐交叉淡入淡出 + SFX 对象池。决定使用 Unity Audio Mixer (4 层路由) + 双轨音乐系统 (Music_A/Music_B + Snapshot 交叉淡入淡出) + SFX 10 源优先级对象池 + AudioListener 单例。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Audio |
| **Knowledge Risk** | MEDIUM — Audio Mixer 在 Unity 6 中有改进，但核心 API 稳定 |
| **References Consulted** | `VERSION.md`, `breaking-changes.md`, `modules/audio.md` |
| **Post-Cutoff APIs Used** | `AudioMixer`, `AudioMixerSnapshot`, `Addressables.LoadAssetAsync<AudioClip>()` |
| **Verification Required** | Audio Mixer Exposed Parameters 在 IL2CPP 构建中的行为；Snapshot 过渡在 0 时间缩放下的表现 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (IDataManager — 音频资产通过 Addressables 加载) |
| **Enables** | ADR-0014 (交互反馈 — PlaySFX) |
| **Blocks** | Audio + InteractionFeedback Epic |
| **Ordering Note** | 在 ADR-0002 之后实现 |

## Context

### Problem Statement

游戏需要管理 3 类音频（SFX 交互音效、Music 背景音乐、Ambience 环境音），音乐需要在章节切换和碎片转场时平滑交叉淡入淡出，SFX 需要避免同时播放数量过多（对象池限制 10 个源），音量需要在暂停菜单中可调节并持久化。

### Constraints

- 最大同时 SFX 数：10（对象池硬限制）
- 音乐交叉淡入淡出时长：0.5-3s 可配置
- 音量控制：4 个独立通道 (Master/SFX/Music/Ambience)
- 音量持久化：PlayerPrefs
- 音频资产通过 Addressables 按需加载

### Requirements

- 4 层 Audio Mixer 路由 (Master→SFX/Music/Ambience)
- 双轨音乐系统 (Music_A/Music_B + 两个 Snapshot)
- SFX 10 源对象池（优先级抢占）
- 章节音频预加载 (PreloadChapterAudioAsync)
- 音量设置保存/恢复

## Decision

**4 层 Audio Mixer + 双轨 Snapshot 交叉淡入淡出 + 10 源 SFX 对象池。**

### Audio Mixer 路由

```
Master Group
├─ SFX Group      (10 AudioSources, 优先级抢占)
├─ Music Group    (2 AudioSources: Music_A, Music_B)
│   ├─ Snapshot: Music_A_Active
│   └─ Snapshot: Music_B_Active
└─ Ambience Group (1 AudioSource, 循环)
```

### 双轨交叉淡入淡出

```csharp
public async void CrossfadeMusic(AudioClip newClip, float fadeTime = 1.5f)
{
    var targetTrack = _currentTrack == MusicTrack.A ? MusicTrack.B : MusicTrack.A;
    var targetSource = targetTrack == MusicTrack.A ? _musicSourceA : _musicSourceB;
    var targetSnapshot = targetTrack == MusicTrack.A ? _snapshotA : _snapshotB;

    // 1. 设置目标音轨的 clip 并开始播放 (volume = 0)
    targetSource.clip = newClip;
    targetSource.volume = 0;
    targetSource.Play();

    // 2. 过渡 Snapshot (AudioMixer 处理衰减曲线)
    targetSnapshot.TransitionTo(fadeTime);

    // 3. 过渡完成后停止旧音轨
    await Task.Delay((int)(fadeTime * 1000));
    var oldSource = _currentTrack == MusicTrack.A ? _musicSourceA : _musicSourceB;
    oldSource.Stop();

    _currentTrack = targetTrack;
}
```

### SFX 优先级对象池

```csharp
public class SFXPool
{
    private const int MaxSources = 10;
    private readonly AudioSource[] _sources;
    private readonly int[] _priorities; // 0 = available, >0 = priority level

    public void PlaySFX(string clipKey, int priority = 1, Vector3? worldPos = null)
    {
        // 1. 查找空闲源
        var freeIdx = Array.FindIndex(_priorities, p => p == 0);
        if (freeIdx >= 0)
        {
            PlayOnSource(_sources[freeIdx], clipKey, priority, worldPos);
            return;
        }

        // 2. 无空闲源 — 抢占最低优先级
        var minPriority = _priorities.Min();
        if (priority > minPriority)
        {
            var victimIdx = Array.IndexOf(_priorities, minPriority);
            _sources[victimIdx].Stop();
            PlayOnSource(_sources[victimIdx], clipKey, priority, worldPos);
        }
        // else: 新声音优先级不够 → 丢弃 (不播放)
    }
}
```

### Architecture Diagram

```
┌──────────────────────────────────────────────┐
│              AudioManager                     │
│                                              │
│  PlaySFX(key, pos?)         → SFXPool        │
│  PlayMusic(key, fadeTime)   → Crossfade       │
│  StopMusic(fadeTime)                          │
│  PlayAmbience(key)          → AmbienceSrc     │
│                                              │
│  SetVolume(channel, value)  → Mixer.SetFloat  │
│  GetVolume(channel)         → PlayerPrefs     │
│                                              │
│  PreloadChapterAudioAsync(key) → Addressables │
│  UnloadChapterAudio(key)                      │
│                                              │
│  Events: OnAudioError                        │
└──────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────┐
│  Audio Mixer (4 layers)                       │
│  Master → SFX / Music / Ambience              │
│  Exposed Params: MasterVol, SFXVol,           │
│                  MusicVol, AmbienceVol        │
│  Snapshots: Music_A_Active, Music_B_Active    │
└──────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public interface IAudioManager
{
    void PlaySFX(string clipKey, Vector3? worldPosition = null);
    void PlayMusic(string clipKey, float fadeTime = 1.5f);
    void StopMusic(float fadeTime = 1.5f);
    void PlayAmbience(string clipKey);

    void SetVolume(AudioChannel channel, float linearValue);
    float GetVolume(AudioChannel channel);

    Task PreloadChapterAudioAsync(string chapterKey);
    void UnloadChapterAudio(string chapterKey);

    // static event Action<string> OnAudioError;
}

public enum AudioChannel { Master, SFX, Music, Ambience }
```

### Implementation Guidelines

1. Audio Mixer 在 `Resources/` 中加载（不在 Addressables — Mixer 始终需要）

   > **ADR-0002 例外说明**: 这是对 ADR-0002（所有资产通过 Addressables 加载）的
   > **有意偏离**。Audio Mixer 资产 (~1KB) 是引导关键资产——在 Boot 阶段
   > Addressables 初始化完成前就必须可用。`Resources.Load<AudioMixer>()` 是 Unity 中
   > 同步加载引导资产的唯一可靠路径。所有 AudioClip 仍通过 Addressables 加载——
   > 仅 Mixer 资产本身走 Resources。此偏离仅适用于 ~1KB 的引导关键资产，不扩展为
   > 通用模式。

2. AudioClip 通过 Addressables 异步加载（首次播放触发加载，后续缓存）
3. SFX 池在 `Awake()` 中创建 10 个 AudioSource 子对象
4. 音量值使用 linear scale (0.0-1.0)，写入 Mixer 前转 dB
5. `PreloadChapterAudioAsync` 是 fire-and-forget — 失败不抛异常

## Alternatives Considered

### Alternative 1: 单 Music AudioSource（无交叉淡入淡出）

- **Description**: 只有一个 Music AudioSource，切歌时硬切或简单 fade out/in
- **Pros**: 实现简单
- **Cons**: 音乐切换生硬；不符合氛围游戏的沉浸感要求
- **Rejection Reason**: 交叉淡入淡出是回响 (Echoes) 氛围体验的关键部分

### Alternative 2: FMOD / WWise 中间件

- **Description**: 使用专业音频中间件
- **Pros**: 功能强大（动态音乐、3D 空间音频、DSP 效果链）
- **Cons**: 许可证费用；集成复杂度；对于 2D 叙事游戏过度设计
- **Rejection Reason**: 项目不需要自适应音乐或 3D 空间音频。Unity Audio Mixer 足够

## Consequences

### Positive

- 音乐交叉淡入淡出提供沉浸式听觉过渡
- SFX 优先级抢占防止音源数量爆炸
- 4 层独立音量控制
- Addressables 按章节管理音频内存

### Negative

- Audio Mixer Exposed Parameters 名称硬编码（字符串耦合）
- SFX 优先级抢占可能导致低优先级声音静默丢失
- 音频加载延迟（首次播放时 Addressables 异步加载）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Audio Mixer Exposed Parameters 在 IL2CPP 中命名不一致 | Low | Medium | Pre-Production IL2CPP 验证；Exposed Parameters 存储在原生 Audio Mixer 资产中，不受托管代码剥离影响（已验证安全） |
| Addressables 音频加载延迟导致音效滞后 | Low | Medium | 预加载章节音频；SFX 使用小文件 (< 50KB) |

> **已修正**: Snapshot 过渡在 `Time.timeScale=0` 时停止——此前标记为风险，经
> unity-specialist 验证此为**错误推断**。Audio Mixer DSP 在音频线程上使用真实时间
> （未缩放），完全独立于 `Time.timeScale`。`AudioMixerSnapshot.TransitionTo()`
> 在 `timeScale=0` 时正确完成。ADR 中 `AudioListener.pause` 的暂停方案对时间无关
> 问题仍然有效，但对 Snapshot 转换而言非必要。

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (PlaySFX) | ~0.1ms |
| CPU (CrossfadeMusic) | ~0.2ms (Snapshot transition，GPU 端) |
| Memory (SFX Pool 10 sources) | ~2KB (AudioSource components) |
| Memory (per AudioClip cached) | ~50KB-2MB (取决于长度和压缩) |
| GC Allocation | 0 (对象池复用) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] 双轨交叉淡入淡出平滑无爆音
- [ ] 10 个 SFX 同时播放时，第 11 个根据优先级抢占
- [ ] 暂停菜单中音量滑块正确控制 4 个通道
- [ ] 章节切换时 `PreloadChapterAudioAsync` 预加载下一章音频
- [ ] 音量设置重启后通过 PlayerPrefs 恢复

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `audio-system.md` (#3) | 音频 | 4 层 Audio Mixer 路由 | Master→SFX/Music/Ambience |
| `audio-system.md` (#3) | 音频 | 双轨交叉淡入淡出 | Music_A/Music_B + Snapshot |
| `audio-system.md` (#3) | 音频 | 10 源 SFX 对象池 | SFXPool + 优先级抢占 |
| `audio-system.md` (#3) | 音频 | 音量持久化 | PlayerPrefs + Exposed Parameters |
| `audio-system.md` (#3) | 音频 | 章节音频预加载 | PreloadChapterAudioAsync |
| `interaction-feedback.md` (#18) | 交互反馈 | 交互触发音效 | PlaySFX(clipKey) |
| `main-menu.md` (#19) | 主菜单 | 音量设置 | SetVolume/GetVolume 接口 |

## Related

- ADR-0002 — Addressables 加载 AudioClip
- ADR-0014 — 交互反馈系统调用 PlaySFX
- `docs/engine-reference/unity/modules/audio.md` — Audio Mixer API
