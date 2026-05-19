using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Production implementation of <see cref="ILocalizationBackend"/> that wraps
/// the Unity Localization Package.
///
/// In the real game this delegates to:
///   - <see cref="UnityEngine.Localization.Settings.LocalizationSettings"/> for locale switching
///   - <see cref="UnityEngine.Localization.Tables.StringTable"/> for string lookups
///   - Unity LP's built-in fallback chain (en → zh-Hans)
///
/// This stub provides the interface contract. Replace method bodies with
/// real Unity LP calls once the package is installed in the Unity project.
/// </summary>
public class UnityLocalizationBackend : ILocalizationBackend
{
    // In-memory tables for editor / test use until Unity LP is wired.
    private readonly Dictionary<string, Dictionary<string, string>> _tables = new();
    private string[] _availableLocales = { "zh-Hans", "en" };
    private string _selectedLocale = "zh-Hans";
    private bool _initialized;

    public bool IsInitialized => _initialized;

    public event Action<string, string, string> MissingTranslationEntry;

    /// <summary>
    /// Pre-loads a mock UI_Shared table. Replace with real Addressables
    /// StringTable load when Unity LP is installed.
    /// </summary>
    public Task InitializeAsync()
    {
        // Production: await LocalizationSettings.InitializationOperation;
        // Production: await Addressables.LoadAssetAsync<StringTable>("UI_Shared");
        _initialized = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Production delegates to StringTable.GetLocalizedString() which
    /// applies the Unity LP fallback chain internally.
    /// </summary>
    public string GetLocalizedString(string tableRef, string entryRef)
    {
        if (_tables.TryGetValue(tableRef, out var table)
            && table.TryGetValue(entryRef, out var value))
        {
            return value;
        }

        // Fire missing translation event
        MissingTranslationEntry?.Invoke(tableRef, entryRef, _selectedLocale);

        // Unity LP fallback would have returned the zh-Hans value;
        // here we signal "not found" so LocalizationManager can apply
        // its own fallback logic.
        return null;
    }

    public string[] GetAvailableLocales() => _availableLocales;

    public void SetSelectedLocale(string localeCode)
    {
        _selectedLocale = localeCode;
        // Production: LocalizationSettings.SelectedLocale = locale;
    }

    public string GetSelectedLocaleCode() => _selectedLocale;

    // =========================================================================
    // Editor / Test Helpers (not part of ILocalizationBackend contract)
    // =========================================================================

    /// <summary>
    /// Registers an in-memory string table for testing or Editor preview.
    /// Not available in release builds.
    /// </summary>
    public void RegisterTable(string tableRef, Dictionary<string, string> entries)
    {
        _tables[tableRef] = new Dictionary<string, string>(entries);
    }

    /// <summary>
    /// Sets the available locale list (for testing).
    /// </summary>
    public void SetAvailableLocales(string[] locales)
    {
        _availableLocales = locales ?? throw new ArgumentNullException(nameof(locales));
    }
}
