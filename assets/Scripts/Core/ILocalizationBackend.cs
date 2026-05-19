using System;
using System.Threading.Tasks;

/// <summary>
/// Abstraction over the Unity Localization Package so
/// <see cref="LocalizationManager"/> is testable without Unity runtime.
///
/// Production implementation: <see cref="UnityLocalizationBackend"/>.
/// </summary>
public interface ILocalizationBackend
{
    /// <summary>Whether the backend is fully initialised (UI_Shared loaded).</summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Loads the UI_Shared StringTable. Must succeed before any localised
    /// string queries are valid.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Returns the localised string for a given table + entry, applying
    /// the backend's fallback chain. Must not return null.
    /// </summary>
    string GetLocalizedString(string tableRef, string entryRef);

    /// <summary>
    /// All available locale codes in priority order (index 0 = default).
    /// </summary>
    string[] GetAvailableLocales();

    /// <summary>
    /// Switches the active locale. Implementations must handle invalid
    /// codes by falling back to the default locale.
    /// </summary>
    void SetSelectedLocale(string localeCode);

    /// <summary>
    /// Returns the BCP-47 code of the currently active locale (e.g. "zh-Hans").
    /// </summary>
    string GetSelectedLocaleCode();

    /// <summary>
    /// Raised when a translation key is missing at all levels of the
    /// fallback chain. Parameters: tableRef, entryRef, localeCode.
    /// </summary>
    event Action<string, string, string> MissingTranslationEntry;
}
