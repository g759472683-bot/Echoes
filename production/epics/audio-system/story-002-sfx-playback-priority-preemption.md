# Story 002: SFX Playback + Priority Preemption

| Field | Value |
|-------|-------|
| **Story ID** | audio-system/story-002 |
| **Epic** | Audio System (#3) |
| **Layer** | Foundation |
| **Type** | Integration |
| **ADR** | ADR-0013 |
| **Status** | Complete |
| **Created** | 2026-05-19 |
| **Completed** | 2026-05-19 |

## Description

Implement the SFXPool priority-based AudioSource pool and wire it to AudioManager.PlaySFX(). When the pool is full, lower-priority sounds are preempted. The pool does NOT handle Addressables loading — AudioManager loads clips and passes AudioClip references to the pool.

## Acceptance Criteria

- [x] **AC-1**: SFXPool creates exactly maxSources (10) AudioSource children on construction
- [x] **AC-2**: Play() uses the first free source (priority 0 or not isPlaying)
- [x] **AC-3**: When pool is full, Play() preempts the source nearest to completion (< 0.1s remaining)
- [x] **AC-4**: When no near-completion source exists, Play() preempts the lowest-priority source (only if incoming priority is higher)
- [x] **AC-5**: When incoming priority is <= lowest active priority, the sound is discarded
- [x] **AC-6**: Playing a null AudioClip is a no-op (no source consumed)
- [x] **AC-7**: Spatial blend is set to 1.0 when worldPosition is provided, 0.0 when null
- [x] **AC-8**: Source transform.position is set to worldPosition when provided
- [x] **AC-9**: AudioManager.PlaySFX(clipKey) is fire-and-forget (async loading, no await)
- [x] **AC-10**: AudioManager.PlaySFX returns immediately when state is not Ready or LoadingChapterAudio
- [x] **AC-11**: Cached AudioClips play immediately; uncached clips play after async Addressables load
- [x] **AC-12**: Existing caller signature maintained: PlaySFX(string audioKey) works (Vector3? worldPosition is optional)

## Implementation Notes

- **Files Created**: `src/core/SFXPool.cs` (~210 lines)
- **Files Modified**: `src/core/AudioManager.cs` — PlaySFX, PlaySFXDeferred, LoadClipAsync methods
- **Key Design**: Pool owns AudioSource lifecycle only. AudioManager owns Addressables loading + caching.
- **Preemption order**: 1) Near completion (< 0.1s) 2) Lowest priority 3) Earliest started
- **Fire-and-forget**: PlaySFX starts async load but does not return a Task

## Test Evidence

- `tests/unit/audio-system/sfx_pool_test.cs` — Pool creation (capacity, child naming, priorities), minimum source enforcement, preemption priority array verification, null clip handling
- `tests/integration/audio-system/playback_test.cs` — Free source allocation, sequential fill, null clip no-op, spatial blend by worldPosition, preemption when full, discard lower priority, Error state blocks playback, LoadingChapterAudio allows SFX

## Completeness

All 12 acceptance criteria met. SFXPool implements 3-tier preemption (near-completion > lowest priority > earliest started). AudioManager.PlaySFX maintains backward compatibility with existing callers.
