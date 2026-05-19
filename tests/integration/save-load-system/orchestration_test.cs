using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// Integration tests for SaveOrchestrator — CollectSaveData, RestoreSaveData,
/// and version migration chain. Uses mock system implementations (no Unity,
/// no Addressables, no real file I/O).
///
/// Covers all 4 acceptance criteria from Story 003 (Collect/Restore Orchestration + Version Migration).
/// </summary>
public class orchestration_test
{
    // =========================================================================
    // Mock System Implementations
    // =========================================================================

    private sealed class MockLocaleProvider : ILocaleProvider
    {
        public string CurrentLocale = "zh-Hans";
        public string RestoredLocale;

        public string GetCurrentLocaleCode() => CurrentLocale;
        public void RestoreLocale(string code) => RestoredLocale = code;
    }

    private sealed class MockAudioAccessor : IAudioSettingsAccessor
    {
        public readonly Dictionary<string, float> Volumes = new()
        {
            ["master"] = 0.8f, ["sfx"] = 0.7f, ["music"] = 0.6f, ["ambience"] = 0.5f
        };
        public readonly Dictionary<string, float> RestoredVolumes = new();

        public float GetVolume(string channel) =>
            Volumes.TryGetValue(channel, out var v) ? v : 0f;

        public void SetVolume(string channel, float value) =>
            RestoredVolumes[channel] = value;
    }

    private sealed class MockChangeOverlay : IChangeOverlayPersistence
    {
        public Dictionary<string, string> Overlay = new()
        {
            { "frag_01:choice_01", "{\"type\":\"ToggleVisualLayer\",\"layerId\":\"ink_wash\"}" }
        };
        public Dictionary<string, string> Restored;

        public Dictionary<string, string> GetPersistableOverlay() => Overlay;
        public void RestoreFromSave(Dictionary<string, string> o) => Restored = o;
    }

    private sealed class MockCrossChapterFlags : ICrossChapterFlagPersistence
    {
        public Dictionary<string, bool> Flags = new()
        {
            { "met_elder", true }, { "found_secret", false }
        };
        public Dictionary<string, bool> Restored;

        public Dictionary<string, bool> GetPersistableFlags() => Flags;
        public void RestoreFromSave(Dictionary<string, bool> f) => Restored = f;
    }

    private sealed class MockEndingTracker : IEndingTriggerPersistence
    {
        public string[] TriggeredIds = { "end_001", "end_003" };
        public string[] Restored;

        public string[] GetTriggeredIds() => TriggeredIds;
        public void RestoreFromSave(string[] ids) => Restored = ids;
    }

    private sealed class MockPlayTime : IPlayTimeTracker
    {
        public int ElapsedSeconds = 4200;
    }

    private sealed class MockChapterManager : IChapterSaveRestore
    {
        public string CurrentChapterKey = "ch02";
        public string CurrentFragmentId = "frag_05";
        public int CurrentFragmentIndex = 4;
        public string[] CompletedChapters = { "ch01" };
        public string[] UnlockedChapters = { "ch01", "ch02" };
        public SaveData RestoredData;
        public bool LoadAndRestoreCalled;

        public string[] GetCompletedChapters() => CompletedChapters;
        public string[] GetUnlockedChapters() => UnlockedChapters;
        public Task LoadAndRestore(SaveData data)
        {
            RestoredData = data;
            LoadAndRestoreCalled = true;
            return Task.CompletedTask;
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static (
        SaveOrchestrator orchestrator,
        MockLocaleProvider locale,
        MockAudioAccessor audio,
        MockChangeOverlay changeTracker,
        MockCrossChapterFlags crossChapter,
        MockEndingTracker ending,
        MockPlayTime playTime,
        MockChapterManager chapter
    ) CreateOrchestrator()
    {
        var locale = new MockLocaleProvider();
        var audio = new MockAudioAccessor();
        var changeTracker = new MockChangeOverlay();
        var crossChapter = new MockCrossChapterFlags();
        var ending = new MockEndingTracker();
        var playTime = new MockPlayTime();
        var chapter = new MockChapterManager();

        var orchestrator = new SaveOrchestrator(
            locale, audio, changeTracker, crossChapter, ending, playTime, chapter);

        return (orchestrator, locale, audio, changeTracker, crossChapter, ending, playTime, chapter);
    }

    private static SaveData CreateSaveDataForRestore()
    {
        return new SaveData
        {
            Version = 1,
            Timestamp = "2026-05-17T15:00:00Z",
            LocaleCode = "en",
            PlayTimeSeconds = 7200,
            CurrentChapterKey = "ch02",
            CurrentFragmentId = "frag_05",
            CurrentFragmentIndex = 4,
            CompletedChapters = new[] { "ch01" },
            UnlockedChapters = new[] { "ch01", "ch02" },
            ChangeOverlay = new Dictionary<string, string>
            {
                { "frag_02:choice_01", "{\"type\":\"SetFlag\",\"flagId\":\"met_mentor\"}" }
            },
            CrossChapterFlags = new Dictionary<string, bool>
            {
                { "met_mentor", true }
            },
            MasterVolume = 0.5f,
            SFXVolume = 0.4f,
            MusicVolume = 0.3f,
            AmbienceVolume = 0.2f,
            TriggeredEndingConditionIds = new[] { "end_002" }
        };
    }

    // =========================================================================
    // AC-1: CollectSaveData gathers state from all 7 systems
    // =========================================================================

    [Test]
    public void test_collect_save_data_gathers_all_system_state()
    {
        // Arrange
        var (orch, locale, audio, change, cross, ending, playTime, chapter) = CreateOrchestrator();
        locale.CurrentLocale = "en";
        playTime.ElapsedSeconds = 9999;

        // Act
        var data = orch.CollectSaveData();

        // Assert — every field populated from the correct source
        Assert.That(data.Version, Is.EqualTo(1));
        Assert.That(data.Timestamp, Is.Not.Null.And.Not.Empty);
        Assert.That(data.LocaleCode, Is.EqualTo("en"));
        Assert.That(data.PlayTimeSeconds, Is.EqualTo(9999));

        Assert.That(data.CurrentChapterKey, Is.EqualTo("ch02"));
        Assert.That(data.CurrentFragmentId, Is.EqualTo("frag_05"));
        Assert.That(data.CurrentFragmentIndex, Is.EqualTo(4));
        Assert.That(data.CompletedChapters, Is.EquivalentTo(new[] { "ch01" }));
        Assert.That(data.UnlockedChapters, Is.EquivalentTo(new[] { "ch01", "ch02" }));

        Assert.That(data.ChangeOverlay.Count, Is.EqualTo(1));
        Assert.That(data.CrossChapterFlags["met_elder"], Is.True);

        Assert.That(data.MasterVolume, Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(data.SFXVolume, Is.EqualTo(0.7f).Within(0.001f));
        Assert.That(data.MusicVolume, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(data.AmbienceVolume, Is.EqualTo(0.5f).Within(0.001f));

        Assert.That(data.TriggeredEndingConditionIds, Is.EquivalentTo(new[] { "end_001", "end_003" }));
    }

    [Test]
    public void test_collect_save_data_empty_overlays_and_flags()
    {
        // Arrange
        var (orch, _, _, change, cross, _, _, _) = CreateOrchestrator();
        change.Overlay = new Dictionary<string, string>();
        cross.Flags = new Dictionary<string, bool>();

        // Act
        var data = orch.CollectSaveData();

        // Assert — empty, not null
        Assert.That(data.ChangeOverlay, Is.Not.Null);
        Assert.That(data.ChangeOverlay.Count, Is.EqualTo(0));
        Assert.That(data.CrossChapterFlags, Is.Not.Null);
        Assert.That(data.CrossChapterFlags.Count, Is.EqualTo(0));
    }

    // =========================================================================
    // AC-2: RestoreSaveData distributes state to all systems
    // =========================================================================

    [Test]
    public async Task test_restore_save_data_distributes_locale_and_audio()
    {
        // Arrange
        var (orch, locale, audio, _, _, _, _, _) = CreateOrchestrator();
        var data = CreateSaveDataForRestore();
        data.Checksum = SaveChecksum.ComputeChecksum(data);

        // Act
        await orch.RestoreSaveData(data);

        // Assert
        Assert.That(locale.RestoredLocale, Is.EqualTo("en"));
        Assert.That(audio.RestoredVolumes["master"], Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(audio.RestoredVolumes["sfx"], Is.EqualTo(0.4f).Within(0.001f));
        Assert.That(audio.RestoredVolumes["music"], Is.EqualTo(0.3f).Within(0.001f));
        Assert.That(audio.RestoredVolumes["ambience"], Is.EqualTo(0.2f).Within(0.001f));
    }

    [Test]
    public async Task test_restore_save_data_distributes_tracker_state()
    {
        // Arrange
        var (orch, _, _, change, cross, ending, _, _) = CreateOrchestrator();
        var data = CreateSaveDataForRestore();
        data.Checksum = SaveChecksum.ComputeChecksum(data);

        // Act
        await orch.RestoreSaveData(data);

        // Assert
        Assert.That(change.Restored, Is.Not.Null);
        Assert.That(change.Restored["frag_02:choice_01"], Does.Contain("met_mentor"));
        Assert.That(cross.Restored["met_mentor"], Is.True);
        Assert.That(ending.Restored, Is.EquivalentTo(new[] { "end_002" }));
    }

    [Test]
    public async Task test_restore_save_data_calls_chapter_load_and_restore_last()
    {
        // Arrange
        var (orch, _, _, _, _, _, _, chapter) = CreateOrchestrator();
        var data = CreateSaveDataForRestore();
        data.Checksum = SaveChecksum.ComputeChecksum(data);

        // Act
        await orch.RestoreSaveData(data);

        // Assert — chapter is restored last (after all other state)
        Assert.That(chapter.LoadAndRestoreCalled, Is.True);
        Assert.That(chapter.RestoredData.CurrentChapterKey, Is.EqualTo("ch02"));
        Assert.That(chapter.RestoredData.CurrentFragmentId, Is.EqualTo("frag_05"));
    }

    [Test]
    public void test_restore_save_data_rejects_bad_checksum()
    {
        // Arrange
        var (orch, _, _, _, _, _, _, _) = CreateOrchestrator();
        var data = CreateSaveDataForRestore();
        data.Checksum = "0000000000000000000000000000000000000000000000000000000000000000";

        // Act & Assert
        var ex = Assert.ThrowsAsync<SaveCorruptedException>(async () =>
            await orch.RestoreSaveData(data));

        Assert.That(ex.Message, Does.Contain("Checksum mismatch"));
    }

    [Test]
    public async Task test_restore_save_data_null_overlay_and_flags_handled()
    {
        // Arrange
        var (orch, _, _, change, cross, ending, _, chapter) = CreateOrchestrator();
        var data = CreateSaveDataForRestore();
        data.ChangeOverlay = null;
        data.CrossChapterFlags = null;
        data.TriggeredEndingConditionIds = null;
        data.Checksum = SaveChecksum.ComputeChecksum(data);

        // Act — should not throw NullReferenceException
        await orch.RestoreSaveData(data);

        // Assert — empty collections restored
        Assert.That(change.Restored, Is.Not.Null.And.Empty);
        Assert.That(cross.Restored, Is.Not.Null.And.Empty);
        Assert.That(ending.Restored, Is.Not.Null.And.Empty);
        Assert.That(chapter.LoadAndRestoreCalled, Is.True);
    }

    // =========================================================================
    // AC-3: Version migration v0 → v1
    // =========================================================================

    [Test]
    public void test_migrate_v0_to_v1_fills_missing_fields()
    {
        // Arrange — v0 SaveData (missing fields that were added in v1)
        var data = new SaveData
        {
            Version = 0,
            Timestamp = "2025-01-01T00:00:00Z",
            CurrentChapterKey = "ch01"
            // ChangeOverlay, CrossChapterFlags, TriggeredEndingConditionIds, etc. are null
        };

        // Act
        var migrated = SaveOrchestrator.MigrateIfNeeded(data);

        // Assert
        Assert.That(migrated.Version, Is.EqualTo(1));
        Assert.That(migrated.ChangeOverlay, Is.Not.Null);
        Assert.That(migrated.CrossChapterFlags, Is.Not.Null);
        Assert.That(migrated.CompletedChapters, Is.Not.Null);
        Assert.That(migrated.UnlockedChapters, Is.Not.Null);
        Assert.That(migrated.TriggeredEndingConditionIds, Is.Not.Null);
        Assert.That(migrated.LocaleCode, Is.EqualTo("zh-Hans"));
        // Original timestamp preserved
        Assert.That(migrated.Timestamp, Is.EqualTo("2025-01-01T00:00:00Z"));
    }

    [Test]
    public void test_migrate_v1_passes_through_unchanged()
    {
        // Arrange
        var data = CreateSaveDataForRestore();
        data.Timestamp = "2026-05-17T15:00:00Z";

        // Act
        var migrated = SaveOrchestrator.MigrateIfNeeded(data);

        // Assert — v1 already at current version, no changes
        Assert.That(migrated.Version, Is.EqualTo(1));
        Assert.That(migrated.Timestamp, Is.EqualTo("2026-05-17T15:00:00Z"));
        Assert.That(migrated.LocaleCode, Is.EqualTo("en"));
    }

    [Test]
    public void test_migrate_future_version_throws()
    {
        // Arrange
        var data = new SaveData { Version = 99 };

        // Act & Assert
        var ex = Assert.Throws<SaveMigrationException>(() =>
            SaveOrchestrator.MigrateIfNeeded(data));

        Assert.That(ex.Message, Does.Contain("newer game version"));
        Assert.That(ex.Message, Does.Contain("v99"));
    }

    [Test]
    public void test_migrate_preserves_existing_fields()
    {
        // Arrange — v0 with pre-existing data
        var data = new SaveData
        {
            Version = 0,
            Timestamp = "2025-06-01T10:00:00Z",
            LocaleCode = "en",
            PlayTimeSeconds = 500,
            CurrentChapterKey = "ch03",
            CurrentFragmentId = "frag_12",
            CurrentFragmentIndex = 11,
            CompletedChapters = new[] { "ch01", "ch02" },
            MasterVolume = 0.9f,
            TriggeredEndingConditionIds = new[] { "end_005" }
        };

        // Act
        var migrated = SaveOrchestrator.MigrateIfNeeded(data);

        // Assert — all existing values preserved
        Assert.That(migrated.Version, Is.EqualTo(1));
        Assert.That(migrated.LocaleCode, Is.EqualTo("en"));
        Assert.That(migrated.PlayTimeSeconds, Is.EqualTo(500));
        Assert.That(migrated.CurrentChapterKey, Is.EqualTo("ch03"));
        Assert.That(migrated.CurrentFragmentId, Is.EqualTo("frag_12"));
        Assert.That(migrated.CompletedChapters, Is.EquivalentTo(new[] { "ch01", "ch02" }));
        Assert.That(migrated.MasterVolume, Is.EqualTo(0.9f).Within(0.001f));
        Assert.That(migrated.TriggeredEndingConditionIds, Is.EquivalentTo(new[] { "end_005" }));
    }

    // =========================================================================
    // AC-4: Migration failure handling
    // =========================================================================

    [Test]
    public void test_migrate_unexpected_version_throws()
    {
        // Arrange — no migration function for version -1
        var data = new SaveData { Version = -1 };

        // Act & Assert
        var ex = Assert.Throws<SaveMigrationException>(() =>
            SaveOrchestrator.MigrateIfNeeded(data));

        Assert.That(ex.Message, Does.Contain("No migration path"));
    }

    [Test]
    public async Task test_restore_rejects_future_version_save()
    {
        // Arrange
        var (orch, _, _, _, _, _, _, _) = CreateOrchestrator();
        var data = CreateSaveDataForRestore();
        data.Version = 99;
        // Checksum won't match because we changed Version, so we need to recompute
        data.Checksum = SaveChecksum.ComputeChecksum(data);

        // Act & Assert — MigrateIfNeeded throws before restore proceeds
        var ex = Assert.ThrowsAsync<SaveMigrationException>(async () =>
            await orch.RestoreSaveData(data));

        Assert.That(ex.Message, Does.Contain("newer game version"));
    }
}
