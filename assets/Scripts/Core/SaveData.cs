using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Canonical save data structure aggregating all persistent player state
/// across 6 systems (ADR-0003 + GDD save-load-system Rule 2).
///
/// Value type (struct) — copy-safe for checksum computation where Checksum
/// is temporarily cleared before SHA-256 hashing.
///
/// Checksum is [JsonIgnore] — never serialized into JSON; computed and
/// validated externally by <see cref="SaveChecksum"/>.
/// </summary>
[Serializable]
public struct SaveData
{
    /// <summary>Save format version. Current = 1.</summary>
    public int Version;

    /// <summary>ISO 8601 UTC timestamp of the save operation.</summary>
    public string Timestamp;

    /// <summary>Current locale code (e.g., "zh-Hans").</summary>
    public string LocaleCode;

    /// <summary>Cumulative play time in seconds.</summary>
    public int PlayTimeSeconds;

    // =========================================================================
    // Chapter Progress
    // =========================================================================

    /// <summary>The chapter the player is currently in.</summary>
    public string CurrentChapterKey;

    /// <summary>The fragment being viewed when the save was made.</summary>
    public string CurrentFragmentId;

    /// <summary>Index of the current fragment within its chapter.</summary>
    public int CurrentFragmentIndex;

    /// <summary>Chapter keys that have been completed.</summary>
    public string[] CompletedChapters;

    /// <summary>Chapter keys that have been unlocked.</summary>
    public string[] UnlockedChapters;

    // =========================================================================
    // Change Overlay (from Memory Change Tracking #12)
    // =========================================================================

    /// <summary>
    /// Serialized ContentOverrides keyed by "fragmentId:choiceId".
    /// Values are JSON strings produced by each ContentChange's own serialization.
    /// </summary>
    public Dictionary<string, string> ChangeOverlay;

    // =========================================================================
    // Cross-Chapter State (from Cross-Chapter State Tracking #16)
    // =========================================================================

    /// <summary>Cross-chapter flags keyed by flag ID.</summary>
    public Dictionary<string, bool> CrossChapterFlags;

    // =========================================================================
    // Player Settings (volume only — other settings via PlayerPrefs)
    // =========================================================================

    public float MasterVolume;
    public float SFXVolume;
    public float MusicVolume;
    public float AmbienceVolume;

    // =========================================================================
    // Ending Triggers
    // =========================================================================

    /// <summary>IDs of ending conditions that have been triggered.</summary>
    public string[] TriggeredEndingConditionIds;

    // =========================================================================
    // Integrity (NOT serialized — computed and validated externally)
    // =========================================================================

    /// <summary>
    /// SHA-256 hex digest of all fields above.
    /// [JsonIgnore] — excluded from serialization. Set by SaveChecksum after serialization.
    /// </summary>
    [JsonIgnore]
    public string Checksum;
}

/// <summary>
/// Thrown when a save file's SHA-256 checksum does not match the computed value,
/// indicating data corruption or tampering. Callers must NOT attempt partial
/// recovery — display the error and return to the main menu.
/// </summary>
public class SaveCorruptedException : Exception
{
    public SaveCorruptedException(string message) : base(message) { }
}
