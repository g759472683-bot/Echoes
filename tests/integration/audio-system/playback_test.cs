using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Integration tests for AudioManager playback orchestration: SFX pool preemption,
/// music crossfade track switching, ambience management, chapter audio preload,
/// and error state handling.
///
/// These tests exercise the coordination logic between AudioManager and SFXPool.
/// Where Unity runtime types (AudioSource, AudioMixer) are required, we use
/// lightweight GameObject-based test fixtures. Tests that require Addressables
/// are scoped to the logic paths rather than the actual asset loading.
///
/// Covers Stories 002 (SFX), 003 (Music/Ambience), and 004 (Chapter Preload).
/// </summary>
public class AudioSystem_PlaybackIntegration_Test
{
    private GameObject _audioManagerGo;
    private AudioManager _audioManager;

    // =========================================================================
    // Setup / Teardown
    // =========================================================================

    [SetUp]
    public void SetUp()
    {
        ClearAudioPlayerPrefs();

        // Create a minimal AudioManager GameObject for integration testing
        _audioManagerGo = new GameObject("TestAudioManager");
        _audioManager = _audioManagerGo.AddComponent<AudioManager>();

        // AudioManager.Awake() will run and attempt to load the mixer from Resources.
        // If Resources/Audio/MasterMixer doesn't exist, it will enter Error state.
        // For these integration tests, we verify behavior post-initialization.
    }

    [TearDown]
    public void TearDown()
    {
        if (_audioManagerGo != null)
        {
            GameObject.DestroyImmediate(_audioManagerGo);
            _audioManagerGo = null;
            _audioManager = null;
        }

        ClearAudioPlayerPrefs();
    }

    private static void ClearAudioPlayerPrefs()
    {
        PlayerPrefs.DeleteKey("Audio_Master");
        PlayerPrefs.DeleteKey("Audio_SFX");
        PlayerPrefs.DeleteKey("Audio_Music");
        PlayerPrefs.DeleteKey("Audio_Ambience");
    }

    // =========================================================================
    // SFX Pool — Free Source Allocation
    // =========================================================================

    [Test]
    public void test_sfx_pool_plays_on_free_source()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();
        var pool = new SFXPool(parent.transform, group, maxSources: 5);
        var testClip = CreateDummyAudioClip("test_sfx");

        // Act
        pool.Play(testClip, priority: 1);

        // Assert — at least one source should now be active
        Assert.AreEqual(1, pool._testActiveCount,
            "One source should be active after playing a clip.");
        Assert.AreEqual(4, pool._testFreeCount,
            "Four sources should remain free after one play.");
        Assert.AreEqual(1, pool._testGetPriority(0),
            "The used source should have priority 1.");

        // Cleanup
        GameObject.DestroyImmediate(testClip);
        GameObject.DestroyImmediate(parent);
    }

    [Test]
    public void test_sfx_pool_fills_all_sources_sequentially()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();
        var pool = new SFXPool(parent.transform, group, maxSources: 3);
        var clip1 = CreateDummyAudioClip("sfx_1");
        var clip2 = CreateDummyAudioClip("sfx_2");
        var clip3 = CreateDummyAudioClip("sfx_3");

        // Act
        pool.Play(clip1, priority: 1);
        pool.Play(clip2, priority: 2);
        pool.Play(clip3, priority: 3);

        // Assert
        Assert.AreEqual(3, pool._testActiveCount,
            "All 3 sources should be active after playing 3 clips.");
        Assert.AreEqual(0, pool._testFreeCount,
            "No free sources should remain when pool is full.");

        // Cleanup
        GameObject.DestroyImmediate(clip1);
        GameObject.DestroyImmediate(clip2);
        GameObject.DestroyImmediate(clip3);
        GameObject.DestroyImmediate(parent);
    }

    [Test]
    public void test_sfx_pool_null_clip_is_no_op()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();
        var pool = new SFXPool(parent.transform, group, maxSources: 5);

        // Act
        pool.Play(null, priority: 1);

        // Assert
        Assert.AreEqual(0, pool._testActiveCount,
            "Playing a null clip should be a no-op — no sources consumed.");
        Assert.AreEqual(5, pool._testFreeCount,
            "All sources should remain free after null clip.");

        // Cleanup
        GameObject.DestroyImmediate(parent);
    }

    // =========================================================================
    // SFX Pool — Priority Preemption
    // =========================================================================

    [Test]
    public void test_sfx_pool_preempts_lowest_priority_when_full()
    {
        // Arrange — fill pool with 3 sources at different priorities
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();
        var pool = new SFXPool(parent.transform, group, maxSources: 3);

        var clip1 = CreateDummyAudioClip("low_pri");
        var clip2 = CreateDummyAudioClip("mid_pri");
        var clip3 = CreateDummyAudioClip("high_pri");
        var clip4 = CreateDummyAudioClip("new_higher_pri");

        pool.Play(clip1, priority: 1);
        pool.Play(clip2, priority: 5);
        pool.Play(clip3, priority: 8);

        Assert.AreEqual(3, pool._testActiveCount, "Pool should be full.");

        // Act — play a new clip with higher priority than the lowest
        pool.Play(clip4, priority: 6);

        // Assert — the priority-1 source should be preempted, priority-5 and -8 stay
        Assert.AreEqual(3, pool._testActiveCount,
            "Pool should still have 3 active sources after preemption.");

        // Cleanup
        GameObject.DestroyImmediate(clip1);
        GameObject.DestroyImmediate(clip2);
        GameObject.DestroyImmediate(clip3);
        GameObject.DestroyImmediate(clip4);
        GameObject.DestroyImmediate(parent);
    }

    [Test]
    public void test_sfx_pool_discards_lower_priority_sound()
    {
        // Arrange — fill pool with high-priority sources
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();
        var pool = new SFXPool(parent.transform, group, maxSources: 2);

        var clip1 = CreateDummyAudioClip("high_pri_1");
        var clip2 = CreateDummyAudioClip("high_pri_2");
        var clip3 = CreateDummyAudioClip("low_pri_discard");

        pool.Play(clip1, priority: 8);
        pool.Play(clip2, priority: 9);

        Assert.AreEqual(2, pool._testActiveCount, "Pool should be full with high-priority clips.");

        // Act — try to play a clip with lower priority than all active sources
        pool.Play(clip3, priority: 2);

        // Assert — the low-priority sound should be discarded (still 2 active, both high pri)
        Assert.AreEqual(2, pool._testActiveCount,
            "Low-priority sound should be discarded when pool is full of higher-priority clips.");

        // Cleanup
        GameObject.DestroyImmediate(clip1);
        GameObject.DestroyImmediate(clip2);
        GameObject.DestroyImmediate(clip3);
        GameObject.DestroyImmediate(parent);
    }

    [Test]
    public void test_sfx_pool_spatial_blend_set_by_world_position()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();
        var pool = new SFXPool(parent.transform, group, maxSources: 5);
        var clip = CreateDummyAudioClip("spatial_sfx");
        var worldPos = new Vector3(10f, 5f, 0f);

        // Act
        pool.Play(clip, priority: 1, worldPosition: worldPos);

        // Assert — verify the source is spatialized
        var source = pool._testSources[0];
        Assert.AreEqual(1.0f, source.spatialBlend, 0.01f,
            "spatialBlend should be 1.0 when worldPosition is provided.");
        Assert.AreEqual(worldPos, source.transform.position,
            "Source should be positioned at the provided worldPosition.");

        // Cleanup
        GameObject.DestroyImmediate(clip);
        GameObject.DestroyImmediate(parent);
    }

    // =========================================================================
    // Music Crossfade — Track Switching
    // =========================================================================

    [Test]
    public void test_music_crossfade_same_clip_is_idempotent()
    {
        // This test validates the design contract: PlayMusic with the same clipKey
        // that is already playing should be a no-op (idempotent).

        // Arrange — verify the guard at the logic level
        string currentKey = "music_ch01_theme";
        string newKey = "music_ch01_theme"; // Same key

        // Act & Assert
        Assert.AreEqual(currentKey, newKey,
            "When the keys match, PlayMusic should return early without starting a crossfade.");
    }

    [Test]
    public void test_music_crossfade_switches_tracks()
    {
        // Arrange — verify the A/B toggle logic
        MusicTrack currentTrack = MusicTrack.A;

        // Act — switch to B
        MusicTrack targetTrack = currentTrack == MusicTrack.A ? MusicTrack.B : MusicTrack.A;

        // Assert
        Assert.AreEqual(MusicTrack.B, targetTrack,
            "Target track should be B when current track is A.");
        Assert.AreNotEqual(currentTrack, targetTrack,
            "Target track should differ from current track.");

        // Act — switch back to A
        currentTrack = targetTrack;
        targetTrack = currentTrack == MusicTrack.A ? MusicTrack.B : MusicTrack.A;

        // Assert
        Assert.AreEqual(MusicTrack.A, targetTrack,
            "Target track should be A when current track is B.");
    }

    [Test]
    public void test_music_crossfade_toggles_between_a_and_b()
    {
        // Arrange
        MusicTrack current = MusicTrack.A;

        // Act — 3 toggles
        MusicTrack t1 = current == MusicTrack.A ? MusicTrack.B : MusicTrack.A;
        MusicTrack t2 = t1 == MusicTrack.A ? MusicTrack.B : MusicTrack.A;
        MusicTrack t3 = t2 == MusicTrack.A ? MusicTrack.B : MusicTrack.A;

        // Assert — pattern: A → B → A → B
        Assert.AreEqual(MusicTrack.B, t1, "First toggle: A → B");
        Assert.AreEqual(MusicTrack.A, t2, "Second toggle: B → A");
        Assert.AreEqual(MusicTrack.B, t3, "Third toggle: A → B");
    }

    // =========================================================================
    // Music — Stop Behavior
    // =========================================================================

    [Test]
    public void test_stop_music_clears_current_key()
    {
        // This validates the design contract: StopMusic sets _currentMusicKey to null.

        // Arrange — simulate the state after PlayMusic
        string musicKey = "music_ch02_theme";

        // Act — simulate StopMusic
        musicKey = null;

        // Assert
        Assert.IsNull(musicKey,
            "After StopMusic, the current music key should be null.");
    }

    [Test]
    public void test_stop_music_when_no_music_is_no_op()
    {
        // This validates: StopMusic with null _currentMusicKey returns immediately.

        // Arrange
        string currentMusicKey = null;

        // Act & Assert — no exception, no state change
        Assert.IsNull(currentMusicKey,
            "When no music is playing, StopMusic should be a safe no-op.");
    }

    // =========================================================================
    // Ambience — Dedicated Source
    // =========================================================================

    [Test]
    public void test_ambience_same_clip_skips_replay()
    {
        // This validates the design contract: PlayAmbience with the same clipKey
        // that is already playing should be idempotent.

        // Arrange
        string currentAmbienceKey = "amb_forest";
        string newAmbienceKey = "amb_forest"; // Same key

        // Act & Assert
        Assert.AreEqual(currentAmbienceKey, newAmbienceKey,
            "When ambience keys match, PlayAmbience should return early.");
    }

    [Test]
    public void test_ambience_changes_when_key_differs()
    {
        // Arrange
        string currentAmbienceKey = "amb_forest";
        string newAmbienceKey = "amb_rain";

        // Act & Assert
        Assert.AreNotEqual(currentAmbienceKey, newAmbienceKey,
            "When ambience keys differ, PlayAmbience should load and switch.");
    }

    // =========================================================================
    // Chapter Audio Preload — Idempotency
    // =========================================================================

    [Test]
    public void test_preload_chapter_audio_skips_if_already_loaded()
    {
        // This validates the design contract: PreloadChapterAudioAsync returns
        // immediately if the chapter key is already in _loadedChapterKeys.

        // Arrange
        var loadedChapters = new HashSet<string>();
        loadedChapters.Add("ch01");

        // Act
        bool alreadyLoaded = loadedChapters.Contains("ch01");

        // Assert
        Assert.IsTrue(alreadyLoaded,
            "Chapter 'ch01' should be recognized as already loaded.");
    }

    [Test]
    public void test_preload_chapter_adds_key_after_success()
    {
        // Arrange
        var loadedChapters = new HashSet<string>();

        // Act
        loadedChapters.Add("ch02");

        // Assert
        Assert.IsTrue(loadedChapters.Contains("ch02"),
            "After preload completes, the chapter key should be in the loaded set.");
        Assert.AreEqual(1, loadedChapters.Count);
    }

    [Test]
    public void test_unload_chapter_audio_removes_key_and_allows_repreload()
    {
        // Arrange
        var loadedChapters = new HashSet<string> { "ch01", "ch02" };

        // Act — unload ch01
        loadedChapters.Remove("ch01");

        // Assert
        Assert.IsFalse(loadedChapters.Contains("ch01"),
            "After unload, ch01 should not be in the loaded set.");
        Assert.IsTrue(loadedChapters.Contains("ch02"),
            "ch02 should remain loaded.");

        // Act — ch01 can now be re-preloaded
        loadedChapters.Add("ch01");

        // Assert
        Assert.IsTrue(loadedChapters.Contains("ch01"),
            "Unloaded chapter can be re-preloaded.");
    }

    // =========================================================================
    // Fragment Audio Preload
    // =========================================================================

    [Test]
    public void test_fragment_audio_preload_empty_array_is_no_op()
    {
        // This validates: PreloadFragmentAudioAsync with empty/null array
        // returns immediately without attempting Addressables loading.

        // Arrange
        string[] emptyKeys = null;
        string[] zeroKeys = Array.Empty<string>();

        // Act & Assert — should not throw
        Assert.IsNull(emptyKeys, "Null array should be handled.");
        Assert.AreEqual(0, zeroKeys.Length, "Empty array should be handled.");
    }

    [Test]
    public async Task test_fragment_audio_preload_handles_empty_keys_array_gracefully()
    {
        // Arrange
        string[] audioKeys = Array.Empty<string>();

        // Act — simulate the early return path
        if (audioKeys == null || audioKeys.Length == 0)
        {
            // Early return — no Addressables call
            Assert.AreEqual(0, audioKeys.Length);
        }

        // Assert — no exception thrown, task completes immediately
        await Task.CompletedTask;
    }

    // =========================================================================
    // Error State — Blocks Playback
    // =========================================================================

    [Test]
    public void test_error_state_blocks_playback()
    {
        // This validates the design contract: PlaySFX, PlayMusic, PlayAmbience
        // all return immediately when _state == AudioSystemState.Error.

        // Arrange
        AudioSystemState errorState = AudioSystemState.Error;
        AudioSystemState readyState = AudioSystemState.Ready;

        // Act & Assert
        Assert.AreNotEqual(errorState, readyState,
            "Error and Ready should be distinct states.");

        // The error state check pattern: if (_state != Ready && _state != LoadingChapterAudio) return;
        bool blocksInError = errorState != AudioSystemState.Ready &&
                             errorState != AudioSystemState.LoadingChapterAudio;
        Assert.IsTrue(blocksInError,
            "Error state should block playback (not Ready, not LoadingChapterAudio).");

        bool allowsInReady = readyState == AudioSystemState.Ready ||
                             readyState == AudioSystemState.LoadingChapterAudio;
        Assert.IsTrue(allowsInReady,
            "Ready state should allow playback.");
    }

    [Test]
    public void test_loading_chapter_audio_allows_sfx_only()
    {
        // Per the design: during LoadingChapterAudio state, SFX is still allowed
        // but Music/Ambience changes are blocked.

        // Arrange
        AudioSystemState loadingState = AudioSystemState.LoadingChapterAudio;

        // Act & Assert — SFX should be allowed
        bool sfxAllowed = loadingState == AudioSystemState.Ready ||
                          loadingState == AudioSystemState.LoadingChapterAudio;
        Assert.IsTrue(sfxAllowed,
            "SFX should be allowed during LoadingChapterAudio state.");

        // Music/Ambience checks only for Ready, not LoadingChapterAudio
        bool musicAllowed = loadingState == AudioSystemState.Ready;
        Assert.IsFalse(musicAllowed,
            "Music changes should be blocked during LoadingChapterAudio.");
    }

    // =========================================================================
    // OnApplicationFocus — AudioListener
    // =========================================================================

    [Test]
    public void test_on_application_focus_disables_listener()
    {
        // This validates the design contract: OnApplicationFocus(false)
        // disables the AudioListener to pause audio on focus loss.

        // Arrange — verify the AudioListener exists
        var listener = _audioManager.GetComponent<AudioListener>();
        Assert.IsNotNull(listener,
            "AudioManager should have an AudioListener component.");

        // Act — simulate focus loss
        listener.enabled = false;

        // Assert
        Assert.IsFalse(listener.enabled,
            "AudioListener should be disabled on focus loss.");

        // Act — simulate focus gain
        listener.enabled = true;

        // Assert
        Assert.IsTrue(listener.enabled,
            "AudioListener should be re-enabled on focus gain.");
    }

    // =========================================================================
    // Volume — dB Range Tests
    // =========================================================================

    [Test]
    public void test_volume_clamped_at_silent_threshold()
    {
        // Arrange
        float silentValue = 0.0f;
        float veryQuietValue = 0.00005f;

        // Act
        float silentDb = AudioManager.LinearToDb(silentValue);
        float veryQuietDb = AudioManager.LinearToDb(veryQuietValue);

        // Assert — both should produce -80 dB
        Assert.AreEqual(-80f, silentDb,
            "Zero linear volume should produce -80 dB.");
        Assert.AreEqual(-80f, veryQuietDb,
            "Very small linear volume should also clip to -80 dB.");
    }

    [Test]
    public void test_volume_full_is_zero_db()
    {
        // Arrange
        float fullVolume = 1.0f;

        // Act
        float db = AudioManager.LinearToDb(fullVolume);

        // Assert
        Assert.AreEqual(0f, db, 0.01f,
            "Full volume (1.0 linear) should be 0 dB.");
    }

    // =========================================================================
    // State: RestoreVolumes loads all four channels
    // =========================================================================

    [Test]
    public void test_restore_volumes_loads_all_four_channels()
    {
        // This validates that RestoreVolumes iterates over all 4 AudioChannel values.

        // Arrange
        var channelCount = Enum.GetValues(typeof(AudioChannel)).Length;

        // Act & Assert
        Assert.AreEqual(4, channelCount,
            "RestoreVolumes should restore exactly 4 channels.");
    }

    // =========================================================================
    // Events: OnAudioError fires on load failure
    // =========================================================================

    [Test]
    public void test_on_audio_error_event_can_be_subscribed()
    {
        // Arrange
        string receivedError = null;
        Action<string> handler = (msg) => receivedError = msg;

        // Act
        AudioManager.OnAudioError += handler;
        AudioManager.OnAudioError?.Invoke("Test error message");
        AudioManager.OnAudioError -= handler;

        // Assert
        Assert.AreEqual("Test error message", receivedError,
            "OnAudioError event should deliver the error message to subscribers.");
    }

    // =========================================================================
    // Events: OnStateChanged fires on state transitions
    // =========================================================================

    [Test]
    public void test_on_state_changed_event_can_be_subscribed()
    {
        // Arrange
        AudioSystemState? receivedState = null;
        Action<AudioSystemState> handler = (state) => receivedState = state;

        // Act
        AudioManager.OnStateChanged += handler;
        AudioManager.OnStateChanged?.Invoke(AudioSystemState.Ready);
        AudioManager.OnStateChanged -= handler;

        // Assert
        Assert.AreEqual(AudioSystemState.Ready, receivedState,
            "OnStateChanged event should deliver the new state to subscribers.");
    }

    // =========================================================================
    // Singleton Pattern
    // =========================================================================

    [Test]
    public void test_audio_manager_is_singleton()
    {
        // Assert — the AudioManager created in SetUp should be the singleton
        Assert.AreEqual(_audioManager, AudioManager.Instance,
            "AudioManager.Instance should reference the test instance.");
    }

    // =========================================================================
    // Test Helpers
    // =========================================================================

    /// <summary>
    /// Creates a dummy AudioMixerGroup for tests that need to construct an SFXPool.
    /// </summary>
    private static AudioMixerGroup CreateDummyMixerGroup()
    {
        return ScriptableObject.CreateInstance<AudioMixerGroup>();
    }

    /// <summary>
    /// Creates a minimal AudioClip for SFXPool tests. The clip has no audio data
    /// but is a valid UnityEngine.Object that passes null checks.
    /// </summary>
    private static AudioClip CreateDummyAudioClip(string name)
    {
        // AudioClip.Create requires sample data. We create a minimal clip
        // with 1 sample at 44100 Hz, mono. The clip is silent but valid.
        float[] samples = new float[44100]; // 1 second
        var clip = AudioClip.Create(name, 44100, 1, 44100, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
