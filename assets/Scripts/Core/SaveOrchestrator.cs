using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Orchestrates cross-system save data collection and restoration.
/// Bridges the SaveManager (file I/O) with the seven game systems that
/// own runtime state. All dependencies injected via constructor.
///
/// Version migration is handled during load — the migration chain
/// transforms old-format SaveData before restoration.
/// </summary>
public class SaveOrchestrator
{
    private readonly ILocaleProvider _locale;
    private readonly IAudioSettingsAccessor _audio;
    private readonly IChangeOverlayPersistence _changeTracker;
    private readonly ICrossChapterFlagPersistence _crossChapterTracker;
    private readonly IEndingTriggerPersistence _endingTracker;
    private readonly IPlayTimeTracker _playTime;
    private readonly IChapterSaveRestore _chapterManager;

    public const int CurrentSaveVersion = 1;

    public SaveOrchestrator(
        ILocaleProvider locale,
        IAudioSettingsAccessor audio,
        IChangeOverlayPersistence changeTracker,
        ICrossChapterFlagPersistence crossChapterTracker,
        IEndingTriggerPersistence endingTracker,
        IPlayTimeTracker playTime,
        IChapterSaveRestore chapterManager)
    {
        _locale = locale ?? throw new ArgumentNullException(nameof(locale));
        _audio = audio ?? throw new ArgumentNullException(nameof(audio));
        _changeTracker = changeTracker ?? throw new ArgumentNullException(nameof(changeTracker));
        _crossChapterTracker = crossChapterTracker ?? throw new ArgumentNullException(nameof(crossChapterTracker));
        _endingTracker = endingTracker ?? throw new ArgumentNullException(nameof(endingTracker));
        _playTime = playTime ?? throw new ArgumentNullException(nameof(playTime));
        _chapterManager = chapterManager ?? throw new ArgumentNullException(nameof(chapterManager));
    }

    // =========================================================================
    // Collect
    // =========================================================================

    /// <summary>
    /// Aggregates runtime state from all 7 systems into a single
    /// <see cref="SaveData"/> struct ready for serialization.
    /// </summary>
    public SaveData CollectSaveData()
    {
        return new SaveData
        {
            Version = CurrentSaveVersion,
            Timestamp = DateTime.UtcNow.ToString("O"),
            LocaleCode = _locale.GetCurrentLocaleCode(),
            PlayTimeSeconds = _playTime.ElapsedSeconds,

            CurrentChapterKey = _chapterManager.CurrentChapterKey,
            CurrentFragmentId = _chapterManager.CurrentFragmentId,
            CurrentFragmentIndex = _chapterManager.CurrentFragmentIndex,
            CompletedChapters = _chapterManager.GetCompletedChapters(),
            UnlockedChapters = _chapterManager.GetUnlockedChapters(),

            ChangeOverlay = _changeTracker.GetPersistableOverlay(),
            CrossChapterFlags = _crossChapterTracker.GetPersistableFlags(),

            MasterVolume = _audio.GetVolume("master"),
            SFXVolume = _audio.GetVolume("sfx"),
            MusicVolume = _audio.GetVolume("music"),
            AmbienceVolume = _audio.GetVolume("ambience"),

            TriggeredEndingConditionIds = _endingTracker.GetTriggeredIds()
        };
    }

    // =========================================================================
    // Restore (with checksum validation)
    // =========================================================================

    /// <summary>
    /// Full restore pipeline: validate checksum → version migration →
    /// distribute state to all 7 systems → trigger chapter restoration.
    /// </summary>
    /// <exception cref="SaveCorruptedException">Checksum mismatch.</exception>
    /// <exception cref="SaveMigrationException">Version too new or migration failed.</exception>
    public async Task RestoreSaveData(SaveData data)
    {
        // 1. Integrity check (from S001)
        SaveChecksum.ValidateChecksum(data);

        // 2. Version migration
        data = MigrateIfNeeded(data);

        // 3. Restore locale and audio first (no async, immediate effect)
        _locale.RestoreLocale(data.LocaleCode);
        _audio.SetVolume("master", data.MasterVolume);
        _audio.SetVolume("sfx", data.SFXVolume);
        _audio.SetVolume("music", data.MusicVolume);
        _audio.SetVolume("ambience", data.AmbienceVolume);

        // 4. Restore state trackers (no scene dependency)
        _changeTracker.RestoreFromSave(data.ChangeOverlay ?? new Dictionary<string, string>());
        _crossChapterTracker.RestoreFromSave(data.CrossChapterFlags ?? new Dictionary<string, bool>());
        _endingTracker.RestoreFromSave(data.TriggeredEndingConditionIds ?? Array.Empty<string>());

        // 5. Restore chapter progress (triggers scene load — comes last)
        await _chapterManager.LoadAndRestore(data);
    }

    // =========================================================================
    // Version Migration
    // =========================================================================

    /// <summary>
    /// Runs the sequential migration chain from <paramref name="data"/>.Version
    /// up to <see cref="CurrentSaveVersion"/>. Each step mutates the struct
    /// (value-type copy — caller's original is not affected).
    /// </summary>
    /// <exception cref="SaveMigrationException">
    /// Version > CurrentSaveVersion, or a migration step threw.
    /// </exception>
    public static SaveData MigrateIfNeeded(SaveData data)
    {
        if (data.Version > CurrentSaveVersion)
        {
            throw new SaveMigrationException(
                $"Save file is from a newer game version (v{data.Version}). " +
                $"Current supported version is v{CurrentSaveVersion}. " +
                $"Please update the game.");
        }

        while (data.Version < CurrentSaveVersion)
        {
            try
            {
                data = data.Version switch
                {
                    0 => Migrate_V0_to_V1(data),
                    // Future versions: add cases here
                    _ => throw new SaveMigrationException(
                        $"No migration path from v{data.Version} to v{CurrentSaveVersion}.")
                };
            }
            catch (SaveMigrationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SaveMigrationException(
                    $"Migration v{data.Version} → v{data.Version + 1} failed: {ex.Message}", ex);
            }
        }

        return data;
    }

    // =========================================================================
    // Migration Functions (one per version step)
    // =========================================================================

    /// <summary>v0 → v1: Ensure all v1 fields have defaults.</summary>
    private static SaveData Migrate_V0_to_V1(SaveData data)
    {
        // v0 may be missing fields added in v1.
        // Ensure non-null reference-type fields.
        data.ChangeOverlay ??= new Dictionary<string, string>();
        data.CrossChapterFlags ??= new Dictionary<string, bool>();
        data.CompletedChapters ??= Array.Empty<string>();
        data.UnlockedChapters ??= Array.Empty<string>();
        data.TriggeredEndingConditionIds ??= Array.Empty<string>();
        data.LocaleCode ??= "zh-Hans";
        data.Version = 1;
        return data;
    }
}

/// <summary>
/// Thrown when version migration cannot complete — either the save is from
/// a newer game version, or a migration step encountered irrecoverable data.
/// </summary>
public class SaveMigrationException : Exception
{
    public SaveMigrationException(string message) : base(message) { }
    public SaveMigrationException(string message, Exception inner) : base(message, inner) { }
}
