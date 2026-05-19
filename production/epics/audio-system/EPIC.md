# Epic: Audio System

| Field | Value |
|-------|-------|
| **Epic ID** | audio-system |
| **Layer** | Foundation |
| **System** | AudioManager (#3) |
| **GDD** | audio-system.md |
| **ADR** | ADR-0013 (Audio Architecture) |
| **Stories** | 4/4 Complete |
| **Status** | Complete |
| **Created** | 2026-05-19 |
| **Completed** | 2026-05-19 |

## Description

Full audio playback system implementing ADR-0013's 4-layer Audio Mixer routing (Master > SFX / Music / Ambience), dual-track music crossfade via AudioMixerSnapshot, 10-source SFX priority pool, and Addressables-based AudioClip loading. This is the last unimplemented MVP Foundation system.

## Architecture Highlights

- **Audio Mixer** loaded via `Resources.Load<AudioMixer>("Audio/MasterMixer")` (ADR-0013 explicit exception to ADR-0002 — Mixer is boot-critical, ~1KB)
- **AudioClips** loaded via `Addressables.LoadAssetAsync<AudioClip>()` (follows ADR-0002)
- **SFX Pool**: 10 AudioSources, priority preemption (highest remaining > highest priority > earliest)
- **Music Crossfade**: Dual AudioSource (A/B) + `AudioMixerSnapshot.TransitionTo()`
- **Volume**: 4 channels (Master, SFX, Music, Ambience), linear 0.0-1.0 to dB conversion, persisted via PlayerPrefs
- **AudioListener**: Single, on DontDestroyOnLoad AudioManager GameObject

## Stories

| # | Story | Type | Status |
|---|-------|------|--------|
| 001 | AudioManager Core + Mixer Setup | Logic | Complete |
| 002 | SFX Playback + Priority Preemption | Integration | Complete |
| 003 | Music Crossfade + Ambience | Integration | Complete |
| 004 | Chapter Audio Preload + Error Handling | Integration | Complete |

## Dependencies

### Implemented Before
- ADR-0013 (Audio Architecture) — Accepted 2026-05-12
- ADR-0002 (Data Management) — Addressables infrastructure

### Implements For
- ADR-0014 (Interaction Feedback) — PlaySFX calls
- GameSceneManager — PreloadFragmentAudioAsync, PreloadChapterAudioAsync, PlayMusic, StopMusic

### Interfaces Implemented
- `IAudioManager` (src/core/IAudioManager.cs)

## Key Files

| File | Purpose |
|------|---------|
| `src/core/AudioManager.cs` | Main singleton MonoBehaviour (~680 lines) |
| `src/core/SFXPool.cs` | Priority-based AudioSource pool (~210 lines) |
| `tests/unit/audio-system/sfx_pool_test.cs` | Unit tests: formulas, state, persistence, pool capacity |
| `tests/integration/audio-system/playback_test.cs` | Integration tests: preemption, crossfade, ambience, preload |

## Acceptance Criteria (ADR-0013 Validation)

- [x] Dual-track crossfade with AudioMixerSnapshot.TransitionTo()
- [x] 10-source SFX pool with priority preemption
- [x] 4-channel volume control (Master/SFX/Music/Ambience)
- [x] Volume persistence via PlayerPrefs
- [x] Chapter audio preload via Addressables (PreloadChapterAudioAsync)
- [x] Fragment audio preload via Addressables (PreloadFragmentAudioAsync)
- [x] Error state with OnAudioError event
- [x] AudioListener pause on focus loss
- [x] `IAudioManager` interface fully implemented

## Verdict

Foundation layer audio system complete. All 4 stories implemented. All acceptance criteria met.
