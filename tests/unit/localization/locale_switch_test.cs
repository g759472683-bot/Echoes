using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// Unit tests for LocalizationManager — state machine, SetLocale,
/// OnLocaleChanged event, and lifecycle transitions.
///
/// Covers all 5 acceptance criteria from Story 002 (Runtime Locale Switching Engine).
/// </summary>
public class locale_switch_test
{
    // =========================================================================
    // Mock Backend
    // =========================================================================

    private sealed class MockLocalizationBackend : ILocalizationBackend
    {
        public bool IsInitialized { get; private set; }
        public string SelectedLocale { get; private set; } = "zh-Hans";
        public string[] AvailableLocales { get; set; } = { "zh-Hans", "en" };
        public bool InitThrows;

        private readonly Dictionary<string, Dictionary<string, string>> _tables = new();

        public event Action<string, string, string> MissingTranslationEntry;

        public Task InitializeAsync()
        {
            if (InitThrows)
                throw new InvalidOperationException("Simulated Addressables failure");

            IsInitialized = true;
            return Task.CompletedTask;
        }

        public string GetLocalizedString(string tableRef, string entryRef)
        {
            if (_tables.TryGetValue(tableRef, out var table)
                && table.TryGetValue(entryRef, out var value))
                return value;

            MissingTranslationEntry?.Invoke(tableRef, entryRef, SelectedLocale);
            return null;
        }

        public string[] GetAvailableLocales() => AvailableLocales;

        public void SetSelectedLocale(string localeCode)
        {
            SelectedLocale = localeCode;
        }

        public string GetSelectedLocaleCode() => SelectedLocale;

        public void RegisterTable(string tableRef, Dictionary<string, string> entries)
        {
            _tables[tableRef] = new Dictionary<string, string>(entries);
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static (LocalizationManager manager, MockLocalizationBackend backend) CreateManager()
    {
        var backend = new MockLocalizationBackend();
        var manager = new LocalizationManager(backend);
        return (manager, backend);
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationManager.ResetStaticState();
    }

    // =========================================================================
    // AC-1: Boot with default zh-Hans
    // =========================================================================

    [Test]
    public async Task test_initialize_transitions_to_ready()
    {
        // Arrange
        var (manager, backend) = CreateManager();

        // Act
        await manager.InitializeAsync();

        // Assert
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Ready));
        Assert.That(backend.IsInitialized, Is.True);
        Assert.That(backend.SelectedLocale, Is.EqualTo("zh-Hans"));
    }

    [Test]
    public async Task test_initialize_fires_on_locale_changed()
    {
        // Arrange
        var (manager, _) = CreateManager();
        string receivedLocale = null;
        LocalizationManager.OnLocaleChanged += code => receivedLocale = code;

        // Act
        await manager.InitializeAsync();

        // Assert
        Assert.That(receivedLocale, Is.EqualTo("zh-Hans"));
    }

    [Test]
    public async Task test_initialize_failure_enters_error_state()
    {
        // Arrange
        var (manager, backend) = CreateManager();
        backend.InitThrows = true;

        // Act
        await manager.InitializeAsync();

        // Assert
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Error));
    }

    [Test]
    public async Task test_double_initialize_is_ignored()
    {
        // Arrange
        var (manager, _) = CreateManager();
        await manager.InitializeAsync();

        // Act — second call should be ignored with warning
        await manager.InitializeAsync();

        // Assert — still Ready, not re-initializing
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Ready));
    }

    // =========================================================================
    // AC-2: Switch to English — UI refreshes same frame
    // =========================================================================

    [Test]
    public async Task test_set_locale_switches_to_english()
    {
        // Arrange
        var (manager, backend) = CreateManager();
        await manager.InitializeAsync();

        // Act
        manager.SetLocale("en");

        // Assert
        Assert.That(backend.SelectedLocale, Is.EqualTo("en"));
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Ready));
    }

    [Test]
    public async Task test_set_locale_fires_on_locale_changed()
    {
        // Arrange
        var (manager, _) = CreateManager();
        await manager.InitializeAsync();

        string receivedLocale = null;
        LocalizationManager.OnLocaleChanged += code => receivedLocale = code;

        // Act
        manager.SetLocale("en");

        // Assert
        Assert.That(receivedLocale, Is.EqualTo("en"));
    }

    [Test]
    public async Task test_set_locale_unavailable_falls_back_to_default()
    {
        // Arrange
        var (manager, backend) = CreateManager();
        backend.AvailableLocales = new[] { "zh-Hans", "en" };
        await manager.InitializeAsync();

        // Act — try to switch to unsupported locale
        manager.SetLocale("ja");

        // Assert — falls back to zh-Hans
        Assert.That(backend.SelectedLocale, Is.EqualTo("zh-Hans"));
    }

    [Test]
    public async Task test_set_locale_cannot_switch_in_error_state()
    {
        // Arrange
        var (manager, backend) = CreateManager();
        backend.InitThrows = true;
        await manager.InitializeAsync();
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Error));

        // Act
        manager.SetLocale("en");

        // Assert — locale unchanged, still in Error
        Assert.That(backend.SelectedLocale, Is.EqualTo("zh-Hans"));
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Error));
    }

    // =========================================================================
    // AC-3: Narrative fragment text updates after switch
    // =========================================================================

    [Test]
    public async Task test_get_localized_string_returns_correct_locale()
    {
        // Arrange
        var (manager, backend) = CreateManager();
        backend.RegisterTable("Narrative_Ch01", new Dictionary<string, string>
        {
            { "narrative.ch01.frag_01.line_01", "记忆像旧照片一样褪色..." }
        });
        await manager.InitializeAsync();

        // Act
        var text = manager.GetLocalizedString("Narrative_Ch01", "narrative.ch01.frag_01.line_01");

        // Assert
        Assert.That(text, Is.EqualTo("记忆像旧照片一样褪色..."));
    }

    [Test]
    public async Task test_narrative_table_loading_state_transition()
    {
        // Arrange
        var (manager, _) = CreateManager();
        await manager.InitializeAsync();

        // Act
        manager.NotifyNarrativeTableLoading();

        // Assert
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.LoadingNarrativeTable));

        // Act 2
        manager.NotifyNarrativeTableReady();

        // Assert 2
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Ready));
    }

    // =========================================================================
    // AC-4: State machine correct transitions
    // =========================================================================

    [Test]
    public async Task test_state_machine_full_trajectory()
    {
        // Arrange
        var (manager, _) = CreateManager();

        // Uninitialized → Initializing → Ready
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Uninitialized));
        await manager.InitializeAsync();
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Ready));

        // Ready → SwitchingLocale → Ready
        manager.SetLocale("en");
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Ready));

        // Ready → LoadingNarrativeTable → Ready
        manager.NotifyNarrativeTableLoading();
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.LoadingNarrativeTable));
        manager.NotifyNarrativeTableReady();
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.Ready));
    }

    [Test]
    public async Task test_switching_locale_during_narrative_load_preserves_narrative_state()
    {
        // Arrange
        var (manager, _) = CreateManager();
        await manager.InitializeAsync();
        manager.NotifyNarrativeTableLoading();
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.LoadingNarrativeTable));

        // Act — switch locale while narrative is loading
        manager.SetLocale("en");

        // Assert — returns to LoadingNarrativeTable, not Ready
        Assert.That(manager.CurrentState, Is.EqualTo(LocaleState.LoadingNarrativeTable));
    }

    [Test]
    public async Task test_consecutive_set_locale_last_one_wins()
    {
        // Arrange
        var (manager, backend) = CreateManager();
        await manager.InitializeAsync();

        // Act — rapid consecutive switches
        manager.SetLocale("en");
        manager.SetLocale("zh-Hans");

        // Assert — last one wins
        Assert.That(backend.SelectedLocale, Is.EqualTo("zh-Hans"));
    }

    // =========================================================================
    // AC-5: OnLocaleChanged notifies all subscribers
    // =========================================================================

    [Test]
    public async Task test_on_locale_changed_notifies_multiple_subscribers()
    {
        // Arrange
        var (manager, _) = CreateManager();
        await manager.InitializeAsync();

        int callCount = 0;
        LocalizationManager.OnLocaleChanged += _ => callCount++;
        LocalizationManager.OnLocaleChanged += _ => callCount++;

        // Act
        manager.SetLocale("en");

        // Assert
        Assert.That(callCount, Is.EqualTo(2));
    }

    [Test]
    public async Task test_on_locale_changed_safe_with_no_subscribers()
    {
        // Arrange
        var (manager, _) = CreateManager();
        await manager.InitializeAsync();

        // Act & Assert — no subscribers, no exception
        Assert.DoesNotThrow(() => manager.SetLocale("en"));
    }

    [Test]
    public async Task test_get_current_locale_code_returns_backend_code()
    {
        // Arrange
        var (manager, backend) = CreateManager();
        await manager.InitializeAsync();
        manager.SetLocale("en");

        // Act
        var code = manager.GetCurrentLocaleCode();

        // Assert
        Assert.That(code, Is.EqualTo("en"));
    }
}
