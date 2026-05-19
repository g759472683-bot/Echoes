using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// Integration tests for locale persistence — verifies that
/// LocalizationManager correctly implements ILocaleProvider and
/// integrates with SaveOrchestrator's CollectSaveData / RestoreSaveData.
///
/// Covers all 4 acceptance criteria from Story 004 (Locale Persistence Integration).
/// </summary>
public class locale_persistence_test
{
    // =========================================================================
    // Mock Backend (shared across tests)
    // =========================================================================

    private sealed class MockLocalizationBackend : ILocalizationBackend
    {
        public bool IsInitialized { get; private set; }
        public string SelectedLocale { get; private set; } = "zh-Hans";
        public string[] AvailableLocales { get; set; } = { "zh-Hans", "en" };
        public int SetLocaleCallCount;

        public event Action<string, string, string> MissingTranslationEntry;

        public Task InitializeAsync()
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public string GetLocalizedString(string tableRef, string entryRef)
        {
            return $"[[{tableRef}:{entryRef}]]";
        }

        public string[] GetAvailableLocales() => AvailableLocales;

        public void SetSelectedLocale(string localeCode)
        {
            SelectedLocale = localeCode;
            SetLocaleCallCount++;
        }

        public string GetSelectedLocaleCode() => SelectedLocale;
    }

    // =========================================================================
    // Mock systems for SaveOrchestrator (same pattern as orchestration_test)
    // =========================================================================

    private sealed class MockAudioAccessor : IAudioSettingsAccessor
    {
        public float GetVolume(string channel) => 0.5f;
        public void SetVolume(string channel, float value) { }
    }

    private sealed class MockChangeOverlay : IChangeOverlayPersistence
    {
        public Dictionary<string, string> GetPersistableOverlay() => new();
        public void RestoreFromSave(Dictionary<string, string> o) { }
    }

    private sealed class MockCrossChapterFlags : ICrossChapterFlagPersistence
    {
        public Dictionary<string, bool> GetPersistableFlags() => new();
        public void RestoreFromSave(Dictionary<string, bool> f) { }
    }

    private sealed class MockEndingTracker : IEndingTriggerPersistence
    {
        public string[] GetTriggeredIds() => Array.Empty<string>();
        public void RestoreFromSave(string[] ids) { }
    }

    private sealed class MockPlayTime : IPlayTimeTracker
    {
        public int ElapsedSeconds => 100;
    }

    private sealed class MockChapterManager : IChapterSaveRestore
    {
        public string CurrentChapterKey => "ch01";
        public string CurrentFragmentId => "frag_01";
        public int CurrentFragmentIndex => 0;
        public string[] GetCompletedChapters() => Array.Empty<string>();
        public string[] GetUnlockedChapters() => Array.Empty<string>();
        public Task LoadAndRestore(SaveData data) => Task.CompletedTask;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static (
        SaveOrchestrator orchestrator,
        LocalizationManager localeManager,
        MockLocalizationBackend backend
    ) CreateOrchestratorWithLocale()
    {
        var backend = new MockLocalizationBackend();
        var localeManager = new LocalizationManager(backend);
        var audio = new MockAudioAccessor();
        var change = new MockChangeOverlay();
        var cross = new MockCrossChapterFlags();
        var ending = new MockEndingTracker();
        var playTime = new MockPlayTime();
        var chapter = new MockChapterManager();

        var orchestrator = new SaveOrchestrator(
            localeManager, audio, change, cross, ending, playTime, chapter);

        return (orchestrator, localeManager, backend);
    }

    private static SaveData CreateSaveData(string localeCode)
    {
        return new SaveData
        {
            Version = 1,
            Timestamp = "2026-05-17T12:00:00Z",
            LocaleCode = localeCode,
            PlayTimeSeconds = 200,
            CurrentChapterKey = "ch01",
            CurrentFragmentId = "frag_01",
            CompletedChapters = Array.Empty<string>(),
            UnlockedChapters = Array.Empty<string>(),
            ChangeOverlay = new Dictionary<string, string>(),
            CrossChapterFlags = new Dictionary<string, bool>(),
            MasterVolume = 0.5f,
            SFXVolume = 0.5f,
            MusicVolume = 0.5f,
            AmbienceVolume = 0.5f,
            TriggeredEndingConditionIds = Array.Empty<string>()
        };
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationManager.ResetStaticState();
    }

    // =========================================================================
    // AC-1: Save and restore preserves locale
    // =========================================================================

    [Test]
    public async Task test_save_and_restore_preserves_english_locale()
    {
        // Arrange
        var (orchestrator, localeManager, backend) = CreateOrchestratorWithLocale();
        await localeManager.InitializeAsync();
        localeManager.SetLocale("en");

        // Collect — should capture "en"
        var saveData = orchestrator.CollectSaveData();
        Assert.That(saveData.LocaleCode, Is.EqualTo("en"));

        // Now simulate new session — switch back to default
        backend.SelectedLocale = "zh-Hans";

        // Act — restore from save data
        saveData.Checksum = SaveChecksum.ComputeChecksum(saveData);
        await orchestrator.RestoreSaveData(saveData);

        // Assert — locale restored to en
        Assert.That(backend.SelectedLocale, Is.EqualTo("en"));
    }

    [Test]
    public async Task test_save_and_restore_preserves_chinese_locale()
    {
        // Arrange
        var (orchestrator, localeManager, backend) = CreateOrchestratorWithLocale();
        await localeManager.InitializeAsync();
        // zh-Hans is the default

        var saveData = orchestrator.CollectSaveData();
        Assert.That(saveData.LocaleCode, Is.EqualTo("zh-Hans"));

        // Simulate restore
        backend.SelectedLocale = "en"; // was changed by something
        saveData.Checksum = SaveChecksum.ComputeChecksum(saveData);
        await orchestrator.RestoreSaveData(saveData);

        Assert.That(backend.SelectedLocale, Is.EqualTo("zh-Hans"));
    }

    // =========================================================================
    // AC-2: GetCurrentLocaleCode returns correct BCP-47 identifier
    // =========================================================================

    [Test]
    public async Task test_get_current_locale_code_returns_zh_hans()
    {
        // Arrange
        var (_, localeManager, _) = CreateOrchestratorWithLocale();
        await localeManager.InitializeAsync();

        // Act
        var code = localeManager.GetCurrentLocaleCode();

        // Assert
        Assert.That(code, Is.EqualTo("zh-Hans"));
    }

    [Test]
    public async Task test_get_current_locale_code_returns_en()
    {
        // Arrange
        var (_, localeManager, _) = CreateOrchestratorWithLocale();
        await localeManager.InitializeAsync();
        localeManager.SetLocale("en");

        // Act
        var code = localeManager.GetCurrentLocaleCode();

        // Assert
        Assert.That(code, Is.EqualTo("en"));
    }

    // =========================================================================
    // AC-3: RestoreLocale calls SetLocale + fires OnLocaleChanged
    // =========================================================================

    [Test]
    public async Task test_restore_locale_calls_set_locale()
    {
        // Arrange
        var (_, localeManager, backend) = CreateOrchestratorWithLocale();
        await localeManager.InitializeAsync();

        // Act
        localeManager.RestoreLocale("en");

        // Assert
        Assert.That(backend.SelectedLocale, Is.EqualTo("en"));
    }

    [Test]
    public async Task test_restore_locale_fires_on_locale_changed()
    {
        // Arrange
        var (_, localeManager, _) = CreateOrchestratorWithLocale();
        await localeManager.InitializeAsync();

        string received = null;
        LocalizationManager.OnLocaleChanged += code => received = code;

        // Act
        localeManager.RestoreLocale("en");

        // Assert
        Assert.That(received, Is.EqualTo("en"));
    }

    // =========================================================================
    // AC-4: Legacy save (no LocaleCode) uses default zh-Hans
    // =========================================================================

    [Test]
    public async Task test_restore_null_locale_keeps_default()
    {
        // Arrange
        var (_, localeManager, backend) = CreateOrchestratorWithLocale();
        await localeManager.InitializeAsync();

        // Act
        localeManager.RestoreLocale(null);

        // Assert — locale unchanged (still default zh-Hans)
        Assert.That(backend.SelectedLocale, Is.EqualTo("zh-Hans"));
    }

    [Test]
    public async Task test_restore_empty_locale_keeps_default()
    {
        // Arrange
        var (_, localeManager, backend) = CreateOrchestratorWithLocale();
        await localeManager.InitializeAsync();
        localeManager.SetLocale("en");

        // Act
        localeManager.RestoreLocale("");

        // Assert — no change (empty = no locale in save, keep current)
        Assert.That(backend.SelectedLocale, Is.EqualTo("en"));
    }

    [Test]
    public async Task test_integration_collect_save_data_includes_locale_code()
    {
        // Arrange
        var (orchestrator, localeManager, _) = CreateOrchestratorWithLocale();
        await localeManager.InitializeAsync();
        localeManager.SetLocale("en");

        // Act
        var saveData = orchestrator.CollectSaveData();

        // Assert
        Assert.That(saveData.LocaleCode, Is.EqualTo("en"));
    }

    [Test]
    public async Task test_integration_restore_with_invalid_locale_falls_back()
    {
        // Arrange
        var (orchestrator, _, backend) = CreateOrchestratorWithLocale();
        var data = CreateSaveData("fr"); // unsupported locale
        data.Checksum = SaveChecksum.ComputeChecksum(data);

        // Act — RestoreSaveData will call RestoreLocale("fr")
        // which calls SetLocale("fr") which detects unavailability and falls back
        await orchestrator.RestoreSaveData(data);

        // Assert — falls back to zh-Hans
        Assert.That(backend.SelectedLocale, Is.EqualTo("zh-Hans"));
    }
}
