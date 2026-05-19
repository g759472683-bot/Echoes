# Story 004: Chapter Audio Preload + Error Handling

| Field | Value |
|-------|-------|
| **Story ID** | audio-system/story-004 |
| **Epic** | Audio System (#3) |
| **Layer** | Foundation |
| **Type** | Integration |
| **ADR** | ADR-0013 |
| **Status** | Complete |
| **Created** | 2026-05-19 |
| **Completed** | 2026-05-19 |

## Description

Implement chapter-level audio preloading via Addressables label groups, fragment-level audio preloading for scene transitions, clip cache deduplication, concurrent load sharing, and comprehensive error handling with the OnAudioError event.

## Acceptance Criteria

- [x] **AC-1**: PreloadChapterAudioAsync(chapterKey) loads all AudioClips in the "Audio_{chapterKey}" Addressables label group
- [x] **AC-2**: PreloadChapterAudioAsync is idempotent (returns immediately if chapter already loaded)
- [x] **AC-3**: PreloadChapterAudioAsync transitions state to LoadingChapterAudio during load, restores on completion
- [x] **AC-4**: PreloadChapterAudioAsync failure is non-fatal (logs warning, does not enter Error state)
- [x] **AC-5**: PreloadFragmentAudioAsync(string[] audioKeys) loads all specified clips and returns when done
- [x] **AC-6**: PreloadFragmentAudioAsync with null or empty array returns immediately (no-op)
- [x] **AC-7**: PreloadFragmentAudioAsync partial failure is non-fatal (logs warning)
- [x] **AC-8**: UnloadChapterAudio(chapterKey) removes the chapter from the loaded set
- [x] **AC-9**: UnloadChapterAudio for an unloaded chapter is a safe no-op
- [x] **AC-10**: Concurrent LoadClipAsync calls for the same key share one Addressables task (dedup)
- [x] **AC-11**: Successfully loaded clips are cached in _clipCache for immediate subsequent playback
- [x] **AC-12**: LoadClipAsync failure fires OnAudioError event
- [x] **AC-13**: Shared_Audio label group is preloaded during initialization
- [x] **AC-14**: OnAudioError event is null-safe (can be invoked with zero subscribers)
- [x] **AC-15**: OnStateChanged event fires on every state transition (Uninitialized -> Initializing -> Ready/Error -> LoadingChapterAudio -> prev)

## Implementation Notes

- **Files Modified**: `src/core/AudioManager.cs` — PreloadChapterAudioAsync, PreloadFragmentAudioAsync, UnloadChapterAudio, LoadClipAsync, PreloadSharedAudioAsync, EnterErrorState methods
- **Key Design**: LoadClipAsync uses a TaskCompletionSource dictionary to deduplicate concurrent loads. Chapter preload uses Addressables.LoadResourceLocationsAsync with label to find all assets in a group.
- **Error handling**: Preload failures are non-fatal (warn, don't block). Only initialization failures enter Error state. Individual clip load failures fire OnAudioError.

## Test Evidence

- `tests/unit/audio-system/sfx_pool_test.cs` — Concurrent load dedup logic, loaded chapter keys idempotency, unload chapter removes key, Error state distinct from Ready
- `tests/integration/audio-system/playback_test.cs` — Preload chapter skips if already loaded, preload adds key, unload removes key and allows re-preload, empty fragment array is no-op, Error state blocks playback, OnAudioError event delivery, OnStateChanged event delivery, LoadingChapterAudio state allows SFX only

## Completeness

All 15 acceptance criteria met. Chapter and fragment preload use Addressables with graceful degradation. Concurrent load dedup prevents redundant downloads. Error handling is non-fatal for preload failures with clear warnings.
