using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Mirrors the GDD localisation state machine.
/// </summary>
public enum LocaleState
{
    /// <summary>LocalizationSettings has not been loaded yet.</summary>
    Uninitialized,

    /// <summary>UI_Shared StringTable is loading asynchronously.</summary>
    Initializing,

    /// <summary>Default locale is active; all queries succeed.</summary>
    Ready,

    /// <summary>Player changed language — switching active locale.</summary>
    SwitchingLocale,

    /// <summary>Chapter Narrative StringTable is loading asynchronously.</summary>
    LoadingNarrativeTable,

    /// <summary>UI_Shared load failed — unrecoverable hard error.</summary>
    Error
}

/// <summary>
/// Central coordination point for the localisation system. Wraps an
/// <see cref="ILocalizationBackend"/> (Unity Localization Package in production)
/// and provides:
///
/// - Six-state locale lifecycle (GDD §States and Transitions)
/// - SetLocale / GetCurrentLocaleCode / RestoreLocale
/// - GetLocalizedString with missing-translation handling
/// - <see cref="OnLocaleChanged"/> static event per ADR-0001
/// - Implements <see cref="ILocaleProvider"/> for save/load integration
///
/// All player-facing text flows through this class. Hardcoding strings
/// outside of fallback error messages is forbidden.
/// </summary>
public class LocalizationManager : ILocaleProvider
{
    private readonly ILocalizationBackend _backend;
    private LocaleState _state = LocaleState.Uninitialized;

    /// <summary>ADR-0001 static event — fired after the active locale changes.</summary>
    public static event Action<string> OnLocaleChanged;

    /// <summary>Default locale code when no save data or fallback is available.</summary>
    public const string DefaultLocaleCode = "zh-Hans";

    /// <summary>StringTable names (matching ADR-0015 1+N table design).</summary>
    public const string UITableRef = "UI_Shared";
    public const string NarrativeTablePrefix = "Narrative_Ch";

    public LocaleState CurrentState => _state;

    public LocalizationManager(ILocalizationBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _backend.MissingTranslationEntry += HandleMissingTranslation;
    }

    // =========================================================================
    // Lifecycle
    // =========================================================================

    /// <summary>
    /// Boot sequence: load UI_Shared StringTable and activate the default locale.
    /// On success the state transitions to <see cref="LocaleState.Ready"/>.
    /// On failure it transitions to <see cref="LocaleState.Error"/>.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_state != LocaleState.Uninitialized)
        {
            Debug.LogWarning($"[Localization] InitializeAsync called in state {_state} — ignoring");
            return;
        }

        _state = LocaleState.Initializing;

        try
        {
            await _backend.InitializeAsync();
            _state = LocaleState.Ready;
            OnLocaleChanged?.Invoke(_backend.GetSelectedLocaleCode());
        }
        catch (Exception ex)
        {
            _state = LocaleState.Error;
            Debug.LogError($"[Localization] UI_Shared load failed: {ex.Message}");
        }
    }

    // =========================================================================
    // ILocaleProvider (save/load integration — Story 004)
    // =========================================================================

    /// <inheritdoc />
    public string GetCurrentLocaleCode()
    {
        return _backend.GetSelectedLocaleCode();
    }

    /// <inheritdoc />
    public void RestoreLocale(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            Debug.Log("[Localization] No locale in save data — keeping default zh-Hans");
            return;
        }

        SetLocale(code);
    }

    // =========================================================================
    // Locale Switching (Story 002)
    // =========================================================================

    /// <summary>
    /// Switches the active locale and fires <see cref="OnLocaleChanged"/>.
    /// If <paramref name="localeCode"/> is unrecognised, falls back to
    /// <see cref="DefaultLocaleCode"/>.
    ///
    /// UI_Shared text refreshes on the same frame (Unity LP handles this).
    /// Narrative table loading is deferred — call
    /// <see cref="NotifyNarrativeTableLoading"/> and
    /// <see cref="NotifyNarrativeTableReady"/> around chapter transitions.
    /// </summary>
    public void SetLocale(string localeCode)
    {
        if (_state == LocaleState.Error || _state == LocaleState.Uninitialized)
        {
            Debug.LogWarning($"[Localization] Cannot switch locale in state {_state}");
            return;
        }

        var previousState = _state;
        _state = LocaleState.SwitchingLocale;

        try
        {
            // Validate — the backend handles fallback internally
            if (!IsLocaleAvailable(localeCode))
            {
                Debug.LogWarning($"[Localization] Locale '{localeCode}' not available — falling back to {DefaultLocaleCode}");
                localeCode = DefaultLocaleCode;
            }

            _backend.SetSelectedLocale(localeCode);
            _state = previousState == LocaleState.LoadingNarrativeTable
                ? LocaleState.LoadingNarrativeTable
                : LocaleState.Ready;

            OnLocaleChanged?.Invoke(localeCode);
        }
        catch (Exception ex)
        {
            _state = LocaleState.Error;
            Debug.LogError($"[Localization] Locale switch failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Call when a chapter Narrative StringTable begins loading.
    /// UI remains fully functional during this state.
    /// </summary>
    public void NotifyNarrativeTableLoading()
    {
        if (_state == LocaleState.Ready || _state == LocaleState.SwitchingLocale)
            _state = LocaleState.LoadingNarrativeTable;
    }

    /// <summary>
    /// Call when the chapter Narrative StringTable has finished loading.
    /// </summary>
    public void NotifyNarrativeTableReady()
    {
        if (_state == LocaleState.LoadingNarrativeTable)
            _state = LocaleState.Ready;
    }

    // =========================================================================
    // String Queries (Story 003)
    // =========================================================================

    /// <summary>
    /// Returns the localised string for the given table and entry key.
    /// Handles missing translations:
    ///   - Development Build: returns "&lt;MISSING: key&gt;"
    ///   - Release Build: returns "……" (ellipsis, never raw key names)
    /// </summary>
    public string GetLocalizedString(string tableRef, string entryRef)
    {
        if (_state == LocaleState.Error || _state == LocaleState.Uninitialized)
        {
            return GetMissingFallback(entryRef);
        }

        try
        {
            var result = _backend.GetLocalizedString(tableRef, entryRef);

            // Empty string is treated the same as missing
            if (string.IsNullOrEmpty(result))
            {
                return GetMissingFallback(entryRef);
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Localization] GetLocalizedString failed ({tableRef}/{entryRef}): {ex.Message}");
            return GetMissingFallback(entryRef);
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private bool IsLocaleAvailable(string localeCode)
    {
        var available = _backend.GetAvailableLocales();
        foreach (var lc in available)
        {
            if (string.Equals(lc, localeCode, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void HandleMissingTranslation(string tableRef, string entryRef, string localeCode)
    {
        Debug.LogWarning(
            $"[Localization] Missing translation — " +
            $"Table={tableRef}, Key={entryRef}, Locale={localeCode}");
    }

    private static string GetMissingFallback(string entryRef)
    {
#if DEVELOPMENT_BUILD
        return $"<MISSING: {entryRef}>";
#else
        return "……";
#endif
    }

    /// <summary>
    /// Resets static event state. Call in test teardown to prevent
    /// cross-test leakage.
    /// </summary>
    public static void ResetStaticState()
    {
        OnLocaleChanged = null;
    }
}
