using System;
using System.Text.RegularExpressions;

/// <summary>
/// Key naming constants and validation for the localisation system.
/// Enforces the three-segment dot-notation convention from GDD §Rule 4.
///
/// Pattern: [domain].[subsystem].[element]_[property]
///
/// Domains: ui, narrative, system
/// All keys must be lowercase ASCII with underscores — no uppercase, no CJK.
/// </summary>
public static class LocalizationConfig
{
    /// <summary>Default locale (development baseline).</summary>
    public const string DefaultLocale = "zh-Hans";

    /// <summary>MVP supported locales.</summary>
    public static readonly string[] SupportedLocales = { "zh-Hans", "en" };

    /// <summary>Fallback chain: en → zh-Hans.</summary>
    public static readonly string[] FallbackChain = { "en", "zh-Hans" };

    /// <summary>Persistent UI string table name.</summary>
    public const string UITableName = "UI_Shared";

    /// <summary>Per-chapter narrative table prefix. Suffix is chapter key (e.g. "Ch01").</summary>
    public const string NarrativeTablePrefix = "Narrative_Ch";

    /// <summary>Regex that all localisation keys must match.</summary>
    private static readonly Regex KeyPattern = new(
        @"^(ui|narrative|system)\.[a-z0-9_]+\.[a-z0-9_]+$",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns true if <paramref name="key"/> follows the three-segment
    /// dot-notation convention.
    /// </summary>
    public static bool IsValidKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        if (!KeyPattern.IsMatch(key))
            return false;

        // Additional: no trailing dot, no double dots
        if (key.EndsWith('.') || key.Contains(".."))
            return false;

        return true;
    }

    /// <summary>
    /// Returns the domain segment of a valid key (ui / narrative / system).
    /// Returns empty string for invalid keys.
    /// </summary>
    public static string GetKeyDomain(string key)
    {
        if (!IsValidKey(key))
            return "";

        int firstDot = key.IndexOf('.');
        return firstDot > 0 ? key.Substring(0, firstDot) : "";
    }

    /// <summary>
    /// Builds a narrative table reference from a chapter number.
    /// Example: GetNarrativeTableRef(1) → "Narrative_Ch01"
    /// </summary>
    public static string GetNarrativeTableRef(int chapterNumber)
    {
        return $"{NarrativeTablePrefix}{chapterNumber:D2}";
    }
}
