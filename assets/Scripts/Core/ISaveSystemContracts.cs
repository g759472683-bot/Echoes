using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// =========================================================================
// System interfaces consumed by SaveManager orchestration.
// Each system (Localization, Audio, ChangeTracker, etc.) implements its
// own interface. SaveManager depends on these abstractions, not concretions.
// =========================================================================

/// <summary>Locale code provider (implemented by Localization #4).</summary>
public interface ILocaleProvider
{
    string GetCurrentLocaleCode();
    void RestoreLocale(string localeCode);
}

/// <summary>Volume settings accessor (implemented by Audio #3).</summary>
public interface IAudioSettingsAccessor
{
    float GetVolume(string channel);  // "master", "sfx", "music", "ambience"
    void SetVolume(string channel, float value);
}

/// <summary>Change overlay persistence (implemented by ChangeTracker #12).</summary>
public interface IChangeOverlayPersistence
{
    Dictionary<string, string> GetPersistableOverlay();
    void RestoreFromSave(Dictionary<string, string> overlay);
}

/// <summary>Cross-chapter flag persistence (implemented by CrossChapterTracker #16).</summary>
public interface ICrossChapterFlagPersistence
{
    Dictionary<string, bool> GetPersistableFlags();
    void RestoreFromSave(Dictionary<string, bool> flags);
}

/// <summary>Ending trigger persistence (implemented by EndingTracker #14).</summary>
public interface IEndingTriggerPersistence
{
    string[] GetTriggeredIds();
    void RestoreFromSave(string[] triggeredIds);
}

/// <summary>Play time tracker (implemented by game session manager).</summary>
public interface IPlayTimeTracker
{
    int ElapsedSeconds { get; }
}

/// <summary>
/// Chapter save/restore contract (implemented by ChapterManager #15).
/// LoadAndRestore triggers the full scene transition to the saved fragment.
/// </summary>
public interface IChapterSaveRestore
{
    string CurrentChapterKey { get; }
    string CurrentFragmentId { get; }
    int CurrentFragmentIndex { get; }
    string[] GetCompletedChapters();
    string[] GetUnlockedChapters();
    Task LoadAndRestore(SaveData data);
}
