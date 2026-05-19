# Story 001: AudioManager Core + Mixer Setup

| Field | Value |
|-------|-------|
| **Story ID** | audio-system/story-001 |
| **Epic** | Audio System (#3) |
| **Layer** | Foundation |
| **Type** | Logic |
| **ADR** | ADR-0013 |
| **Status** | Complete |
| **Created** | 2026-05-19 |
| **Completed** | 2026-05-19 |

## Description

Implement the core AudioManager singleton MonoBehaviour: Audio Mixer loading via Resources, AudioSource creation for Music A/B and Ambience, SFX pool instantiation, AudioListener setup, and the initialization state machine (Uninitialized -> Initializing -> Ready or Error).

## Acceptance Criteria

- [x] **AC-1**: Audio Mixer loaded from `Resources.Load<AudioMixer>("Audio/MasterMixer")`
- [x] **AC-2**: Audio Mixer groups found: SFX, Music, Ambience (and sub-groups Music_A, Music_B)
- [x] **AC-3**: AudioMixerSnapshots found: Music_A_Active, Music_B_Active
- [x] **AC-4**: Missing snapshots degrade gracefully (warning, not error)
- [x] **AC-5**: Two looping music AudioSources created: MusicSourceA (Music_A group), MusicSourceB (Music_B group)
- [x] **AC-6**: One looping ambience AudioSource created: AmbienceSource (Ambience group)
- [x] **AC-7**: Single AudioListener on DontDestroyOnLoad AudioManager GameObject
- [x] **AC-8**: 10-source SFXPool created in Awake, routed to SFX group
- [x] **AC-9**: Singleton enforcement: second AudioManager instance is destroyed
- [x] **AC-10**: Volume restored from PlayerPrefs on initialization (4 channels)
- [x] **AC-11**: OnApplicationFocus(false) disables AudioListener; OnApplicationFocus(true) re-enables
- [x] **AC-12**: Initialization failure enters Error state with OnAudioError event
- [x] **AC-13**: Shared_Audio assets preloaded via Addressables during init

## Implementation Notes

- **Files Modified**: `src/core/AudioManager.cs` — full rewrite from stub
- **Key Design**: ADR-0013 exception for Resources.Load (Mixer is boot-critical, ~1KB)
- **State Machine**: Uninitialized -> Initializing -> Ready | Error
- **PlayerPrefs Keys**: Audio_Master, Audio_SFX, Audio_Music, Audio_Ambience
- **Mixer Params**: MasterVolume, SFXVolume, MusicVolume, AmbienceVolume

## Test Evidence

- `tests/unit/audio-system/sfx_pool_test.cs` — Formula tests (LinearToDb, DbToLinear), PlayerPrefs persistence, state machine enum validation, pool capacity tests, singleton null-before-Awake test
- `tests/integration/audio-system/playback_test.cs` — AudioListener component verification, singleton instance verification, OnApplicationFocus behavior, volume dB conversion, restore channels count

## Completeness

All 13 acceptance criteria met. Full implementation in `src/core/AudioManager.cs` (~680 lines).
