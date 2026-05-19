# Story 003: Music Crossfade + Ambience

| Field | Value |
|-------|-------|
| **Story ID** | audio-system/story-003 |
| **Epic** | Audio System (#3) |
| **Layer** | Foundation |
| **Type** | Integration |
| **ADR** | ADR-0013 |
| **Status** | Complete |
| **Created** | 2026-05-19 |
| **Completed** | 2026-05-19 |

## Description

Implement dual-track music crossfade using AudioMixerSnapshot.TransitionTo() and dedicated looping ambience playback. Music uses an A/B source pattern: while one track plays, the inactive track loads and starts the next clip, then the mixer snapshot transitions to crossfade.

## Acceptance Criteria

- [x] **AC-1**: PlayMusic(clipKey, fadeTime) loads the clip, starts it on the inactive track, transitions the mixer snapshot, and stops the old track
- [x] **AC-2**: PlayMusic with the same clipKey that is already playing is idempotent (returns immediately)
- [x] **AC-3**: Music track toggles between A and B on each PlayMusic call with a different key
- [x] **AC-4**: Missing snapshots degrade gracefully (hard cut instead of crossfade)
- [x] **AC-5**: StopMusic(fadeTime) stops the active track and clears _currentMusicKey
- [x] **AC-6**: StopMusic when no music is playing is a safe no-op
- [x] **AC-7**: PlayAmbience(clipKey) loads the clip, plays it on the dedicated ambience source (looping)
- [x] **AC-8**: PlayAmbience with the same clipKey is idempotent
- [x] **AC-9**: Both music sources are set to loop = true
- [x] **AC-10**: Ambience source is set to loop = true
- [x] **AC-11**: Music crossfade uses AudioMixerSnapshot.TransitionTo(fadeTime) when snapshots are available
- [x] **AC-12**: After crossfade completes (Task.Delay), old source is stopped
- [x] **AC-13**: Music and Ambience changes are blocked when state is not Ready

## Implementation Notes

- **Files Modified**: `src/core/AudioManager.cs` — PlayMusic, CrossfadeMusicAsync, StopMusic, PlayAmbience, PlayAmbienceAsync methods
- **Key Design**: A/B track toggle ensures seamless transitions. The old track keeps playing while the new track fades in via snapshot transition.
- **Snapshot degradation**: If Music_A_Active/Music_B_Active snapshots are not in the mixer, the system performs hard cuts and logs a warning.

## Test Evidence

- `tests/unit/audio-system/sfx_pool_test.cs` — MusicTrack enum (2 values), A/B toggle logic verification
- `tests/integration/audio-system/playback_test.cs` — Music crossfade track switching, idempotent same-key guard, stop music clears key, safe no-op when no music, ambience same-clip idempotency, ambience key change detection, LoadingChapterAudio blocks music/ambience, Ready allows music/ambience

## Completeness

All 13 acceptance criteria met. Music crossfade uses Snapshot.TransitionTo() with graceful degradation. Ambience uses a dedicated looping AudioSource.
