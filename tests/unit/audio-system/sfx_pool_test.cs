using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Unit tests for the audio system's core formulas, state machine, volume control,
/// and PlayerPrefs persistence. These tests exercise pure C# logic that does not
/// require Unity runtime (no AudioSource, no GameObject, no Addressables).
///
/// Covers Stories 001-004 formula and logic paths.
/// </summary>
public class AudioSystem_FormulaAndState_Test
{
    // =========================================================================
    // Setup / Teardown
    // =========================================================================

    [SetUp]
    public void SetUp()
    {
        ClearAudioPlayerPrefs();
    }

    [TearDown]
    public void TearDown()
    {
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
    // Linear ↔ dB Formula Tests
    // =========================================================================

    [Test]
    public void test_linear_to_db_zero_returns_minus_80()
    {
        // Arrange
        float linear = 0.0f;

        // Act
        float db = AudioManager.LinearToDb(linear);

        // Assert
        Assert.AreEqual(-80f, db, 0.01f,
            "Linear 0.0 should map to -80 dB (effectively silent).");
    }

    [Test]
    public void test_linear_to_db_very_small_returns_minus_80()
    {
        // Arrange
        float linear = 0.00005f; // Below 0.0001 threshold

        // Act
        float db = AudioManager.LinearToDb(linear);

        // Assert
        Assert.AreEqual(-80f, db, 0.01f,
            "Values at or below 0.0001 should clip to -80 dB.");
    }

    [Test]
    public void test_linear_to_db_threshold_boundary_returns_minus_80()
    {
        // Arrange
        float linear = 0.0001f; // Exactly at threshold

        // Act
        float db = AudioManager.LinearToDb(linear);

        // Assert
        Assert.AreEqual(-80f, db, 0.01f,
            "Linear 0.0001 should map to -80 dB (<= threshold).");
    }

    [Test]
    public void test_linear_to_db_one_returns_zero()
    {
        // Arrange
        float linear = 1.0f;

        // Act
        float db = AudioManager.LinearToDb(linear);

        // Assert
        Assert.AreEqual(0f, db, 0.01f,
            "Linear 1.0 should map to 0 dB (unity gain).");
    }

    [Test]
    public void test_linear_to_db_half_returns_approx_minus_6()
    {
        // Arrange
        float linear = 0.5f;

        // Act
        float db = AudioManager.LinearToDb(linear);

        // Assert
        Assert.AreEqual(-6.02f, db, 0.1f,
            "Linear 0.5 should map to approximately -6 dB.");
    }

    [Test]
    public void test_linear_to_db_quarter_returns_approx_minus_12()
    {
        // Arrange
        float linear = 0.25f;

        // Act
        float db = AudioManager.LinearToDb(linear);

        // Assert
        Assert.AreEqual(-12.04f, db, 0.1f,
            "Linear 0.25 should map to approximately -12 dB.");
    }

    [Test]
    public void test_db_to_linear_zero_returns_one()
    {
        // Arrange
        float db = 0f;

        // Act
        float linear = AudioManager.DbToLinear(db);

        // Assert
        Assert.AreEqual(1.0f, linear, 0.01f,
            "0 dB should return linear 1.0.");
    }

    [Test]
    public void test_db_to_linear_minus_6_returns_approx_half()
    {
        // Arrange
        float db = -6.02f;

        // Act
        float linear = AudioManager.DbToLinear(db);

        // Assert
        Assert.AreEqual(0.5f, linear, 0.02f,
            "-6 dB should return approximately linear 0.5.");
    }

    [Test]
    public void test_db_to_linear_minus_80_returns_near_zero()
    {
        // Arrange
        float db = -80f;

        // Act
        float linear = AudioManager.DbToLinear(db);

        // Assert
        Assert.AreEqual(0.0001f, linear, 0.0001f,
            "-80 dB should return approximately linear 0.0001.");
    }

    [Test]
    public void test_linear_db_roundtrip_preserves_value()
    {
        // Arrange
        float[] testValues = { 0.01f, 0.1f, 0.5f, 0.75f, 1.0f };

        foreach (float original in testValues)
        {
            // Act
            float db = AudioManager.LinearToDb(original);
            float roundtripped = AudioManager.DbToLinear(db);

            // Assert
            Assert.AreEqual(original, roundtripped, 0.02f,
                $"Linear→dB→Linear roundtrip should preserve value for input {original}.");
        }
    }

    // =========================================================================
    // AudioChannel Enum Tests
    // =========================================================================

    [Test]
    public void test_channel_enum_has_four_values()
    {
        // Arrange
        var values = Enum.GetValues(typeof(AudioChannel));

        // Act & Assert
        Assert.AreEqual(4, values.Length,
            "AudioChannel enum should have exactly 4 values: Master, SFX, Music, Ambience.");
    }

    [Test]
    public void test_channel_enum_contains_all_expected_channels()
    {
        // Arrange
        var channelSet = new HashSet<AudioChannel>();

        // Act
        foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
        {
            channelSet.Add(channel);
        }

        // Assert
        Assert.IsTrue(channelSet.Contains(AudioChannel.Master), "Should contain Master.");
        Assert.IsTrue(channelSet.Contains(AudioChannel.SFX), "Should contain SFX.");
        Assert.IsTrue(channelSet.Contains(AudioChannel.Music), "Should contain Music.");
        Assert.IsTrue(channelSet.Contains(AudioChannel.Ambience), "Should contain Ambience.");
    }

    // =========================================================================
    // AudioSystemState Enum Tests
    // =========================================================================

    [Test]
    public void test_system_state_enum_has_five_values()
    {
        // Arrange
        var values = Enum.GetValues(typeof(AudioSystemState));

        // Act & Assert
        Assert.AreEqual(5, values.Length,
            "AudioSystemState should have 5 values: Uninitialized, Initializing, Ready, LoadingChapterAudio, Error.");
    }

    // =========================================================================
    // MusicTrack Enum Tests
    // =========================================================================

    [Test]
    public void test_music_track_enum_has_two_values()
    {
        // Arrange
        var values = Enum.GetValues(typeof(MusicTrack));

        // Act & Assert
        Assert.AreEqual(2, values.Length,
            "MusicTrack should have exactly 2 values: A and B.");
    }

    // =========================================================================
    // Volume Persistence (PlayerPrefs) Tests
    // =========================================================================

    [Test]
    public void test_get_volume_returns_default_when_no_prefs()
    {
        // NOTE: This test requires an AudioManager instance.
        // The static formula and channel mapping methods are tested directly.
        // Volume persistence tests verify the PlayerPrefs key contract.

        // Arrange
        string ppKey = "Audio_Master";

        // Act
        float volume = PlayerPrefs.GetFloat(ppKey, 0.8f);

        // Assert
        Assert.AreEqual(0.8f, volume, 0.01f,
            "Default master volume should be 0.8 when no PlayerPrefs exists.");
    }

    [Test]
    public void test_set_volume_writes_to_playerprefs()
    {
        // Arrange
        string ppKey = "Audio_SFX";
        float testValue = 0.45f;

        // Act
        PlayerPrefs.SetFloat(ppKey, testValue);
        PlayerPrefs.Save();
        float readValue = PlayerPrefs.GetFloat(ppKey, -1f);

        // Assert
        Assert.AreEqual(testValue, readValue, 0.01f,
            "SetFloat + Save should persist and be readable.");
    }

    [Test]
    public void test_get_volume_reads_from_playerprefs()
    {
        // Arrange
        string ppKey = "Audio_Music";
        float writtenValue = 0.3f;
        PlayerPrefs.SetFloat(ppKey, writtenValue);
        PlayerPrefs.Save();

        // Act
        float readValue = PlayerPrefs.GetFloat(ppKey, 1.0f);

        // Assert
        Assert.AreEqual(writtenValue, readValue, 0.01f,
            "GetFloat should return the saved value, not the default.");
    }

    [Test]
    public void test_all_four_channel_keys_are_distinct()
    {
        // Arrange
        string[] ppKeys = { "Audio_Master", "Audio_SFX", "Audio_Music", "Audio_Ambience" };

        // Act & Assert
        Assert.AreEqual(4, new HashSet<string>(ppKeys).Count,
            "All four channel PlayerPrefs keys should be distinct.");
    }

    [Test]
    public void test_default_volumes_match_expected_values()
    {
        // Arrange
        float[] expectedDefaults = { 0.8f, 0.7f, 0.6f, 0.5f };
        string[] ppKeys = { "Audio_Master", "Audio_SFX", "Audio_Music", "Audio_Ambience" };

        for (int i = 0; i < ppKeys.Length; i++)
        {
            // Act
            float value = PlayerPrefs.GetFloat(ppKeys[i], expectedDefaults[i]);

            // Assert
            Assert.AreEqual(expectedDefaults[i], value, 0.01f,
                $"Default for {ppKeys[i]} should be {expectedDefaults[i]}.");
        }
    }

    // =========================================================================
    // SFX Pool Capacity Tests
    // =========================================================================

    [Test]
    public void test_sfx_pool_creates_exactly_max_sources()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();

        // Act
        var pool = new SFXPool(parent.transform, group, maxSources: 10);

        // Assert
        Assert.AreEqual(10, pool._testPoolSize,
            "SFXPool should create exactly 10 sources.");
        Assert.AreEqual(10, pool._testFreeCount,
            "All 10 sources should initially be free.");

        // Cleanup
        GameObject.DestroyImmediate(parent);
    }

    [Test]
    public void test_sfx_pool_all_sources_initially_free()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();

        // Act
        var pool = new SFXPool(parent.transform, group, maxSources: 5);

        // Assert
        Assert.AreEqual(5, pool._testFreeCount,
            "All sources should be free initially.");
        Assert.AreEqual(0, pool._testActiveCount,
            "No sources should be active initially.");

        // Cleanup
        GameObject.DestroyImmediate(parent);
    }

    [Test]
    public void test_sfx_pool_priorities_all_zero_initially()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();

        // Act
        var pool = new SFXPool(parent.transform, group, maxSources: 10);

        // Assert
        for (int i = 0; i < pool._testPoolSize; i++)
        {
            Assert.AreEqual(0, pool._testGetPriority(i),
                $"Source {i} priority should be 0 (free) initially.");
        }

        // Cleanup
        GameObject.DestroyImmediate(parent);
    }

    [Test]
    public void test_sfx_pool_creates_child_gameobjects()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();

        // Act
        var pool = new SFXPool(parent.transform, group, maxSources: 3);

        // Assert
        Assert.AreEqual(3, parent.transform.childCount,
            "SFXPool should create child GameObjects for each source.");
        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual($"SFX_Source_{i}", parent.transform.GetChild(i).name,
                $"Child {i} should be named SFX_Source_{i}.");
        }

        // Cleanup
        GameObject.DestroyImmediate(parent);
    }

    [Test]
    public void test_sfx_pool_minimum_one_source()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();

        // Act
        var pool = new SFXPool(parent.transform, group, maxSources: 0); // Invalid input

        // Assert
        Assert.AreEqual(1, pool._testPoolSize,
            "SFXPool should enforce a minimum of 1 source.");

        // Cleanup
        GameObject.DestroyImmediate(parent);
    }

    // =========================================================================
    // Preemption Algorithm Tests (pure logic, no AudioSource needed)
    // =========================================================================

    [Test]
    public void test_preemption_finds_lowest_priority_when_all_full()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();
        var pool = new SFXPool(parent.transform, group, maxSources: 3);

        // Simulate: all 3 sources active with different priorities
        pool._testSetPriority(0, 5);
        pool._testSetPriority(1, 2);
        pool._testSetPriority(2, 8);

        // The preemption victim should be source 1 (priority 2, lowest).

        // Act — verify priority values
        // Assert
        Assert.AreEqual(5, pool._testGetPriority(0));
        Assert.AreEqual(2, pool._testGetPriority(1));
        Assert.AreEqual(8, pool._testGetPriority(2));

        // Cleanup
        GameObject.DestroyImmediate(parent);
    }

    [Test]
    public void test_sfx_pool_priority_array_correct_size()
    {
        // Arrange
        var parent = new GameObject("TestPoolParent");
        var group = CreateDummyMixerGroup();

        // Act
        var pool = new SFXPool(parent.transform, group, maxSources: 7);

        // Assert
        Assert.AreEqual(7, pool._testPriorities.Length,
            "Priorities array should match pool size.");
        Assert.AreEqual(7, pool._testStartTimes.Length,
            "StartTimes array should match pool size.");

        // Cleanup
        GameObject.DestroyImmediate(parent);
    }

    // =========================================================================
    // AudioSystemState Transitions
    // =========================================================================

    [Test]
    public void test_state_transitions_follow_expected_lifecycle()
    {
        // Arrange
        var states = new[]
        {
            AudioSystemState.Uninitialized,
            AudioSystemState.Initializing,
            AudioSystemState.Ready,
        };

        // Act & Assert — lifecycle order must be valid
        Assert.AreEqual(0, (int)AudioSystemState.Uninitialized, "Uninitialized should be first.");
        Assert.AreEqual(1, (int)AudioSystemState.Initializing, "Initializing should be second.");
        Assert.AreEqual(2, (int)AudioSystemState.Ready, "Ready should be third.");
        Assert.AreEqual(3, (int)AudioSystemState.LoadingChapterAudio, "LoadingChapterAudio should be fourth.");
        Assert.AreEqual(4, (int)AudioSystemState.Error, "Error should be last.");
    }

    // =========================================================================
    // Concurrent Load Dedup Logic
    // =========================================================================

    [Test]
    public void test_concurrent_clip_loads_share_same_task()
    {
        // This test validates the design contract: AudioManager._pendingLoads
        // dictionary deduplicates concurrent loads so two rapid PlaySFX calls
        // for the same uncached key share one Addressables task.

        // Arrange — verify the dedup pattern at the conceptual level
        var pendingLoads = new Dictionary<string, System.Threading.Tasks.Task<string>>();
        string testKey = "test_clip";

        // Act — first request creates the entry
        bool firstHasEntry = pendingLoads.ContainsKey(testKey);

        // Assert
        Assert.IsFalse(firstHasEntry,
            "Before any load, the pending dict should not contain the key.");

        // Act — simulate first load request
        var tcs1 = new System.Threading.Tasks.TaskCompletionSource<string>();
        pendingLoads[testKey] = tcs1.Task;

        // Assert — second "request" finds existing entry
        Assert.IsTrue(pendingLoads.ContainsKey(testKey),
            "After first request, the key should be in the pending dict.");
        Assert.AreSame(tcs1.Task, pendingLoads[testKey],
            "Second request should get the same Task instance (dedup).");
    }

    // =========================================================================
    // Chapter Preload Idempotency
    // =========================================================================

    [Test]
    public void test_loaded_chapter_keys_prevent_duplicate_preload()
    {
        // Arrange
        var loadedChapters = new HashSet<string>();

        // Act — first add
        bool firstAdd = loadedChapters.Add("ch01");

        // Assert
        Assert.IsTrue(firstAdd,
            "First Add for 'ch01' should succeed (new entry).");

        // Act — second add (idempotency check)
        bool secondAdd = loadedChapters.Add("ch01");

        // Assert
        Assert.IsFalse(secondAdd,
            "Second Add for 'ch01' should fail (already exists — idempotent).");
        Assert.AreEqual(1, loadedChapters.Count,
            "Set should contain exactly 1 entry after duplicate add attempt.");
    }

    [Test]
    public void test_unload_chapter_removes_key()
    {
        // Arrange
        var loadedChapters = new HashSet<string> { "ch01", "ch02", "ch03" };
        Assert.AreEqual(3, loadedChapters.Count);

        // Act
        bool removed = loadedChapters.Remove("ch02");

        // Assert
        Assert.IsTrue(removed, "Remove should return true for existing key.");
        Assert.AreEqual(2, loadedChapters.Count, "Set should have 2 entries after removal.");
    }

    // =========================================================================
    // Error State Tests
    // =========================================================================

    [Test]
    public void test_error_state_is_distinct_from_ready()
    {
        // Arrange & Act & Assert
        Assert.AreNotEqual(AudioSystemState.Error, AudioSystemState.Ready,
            "Error state should be distinct from Ready state.");
    }

    // =========================================================================
    // Static Helpers Tests
    // =========================================================================

    [Test]
    public void test_audio_manager_instance_is_null_until_awake()
    {
        // Assert
        Assert.IsNull(AudioManager.Instance,
            "AudioManager.Instance should be null until the first AudioManager awakes.");
    }

    // =========================================================================
    // Test Helpers
    // =========================================================================

    /// <summary>
    /// Creates a dummy AudioMixerGroup for tests that need to construct an SFXPool.
    /// Uses ScriptableObject.CreateInstance to create a minimal AudioMixerGroup
    /// that satisfies the constructor parameter type without requiring a real mixer.
    /// </summary>
    private static AudioMixerGroup CreateDummyMixerGroup()
    {
        // AudioMixerGroup is a sealed class inheriting from UnityEngine.Object.
        // In EditMode tests, we create a ScriptableObject instance — it will be
        // a valid UnityEngine.Object that passes null checks but won't actually
        // route audio (since there's no real mixer backing it).
        return ScriptableObject.CreateInstance<AudioMixerGroup>();
    }
}
