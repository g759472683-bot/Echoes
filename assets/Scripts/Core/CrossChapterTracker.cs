using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Cross-chapter narrative flag orchestrator (ADR-0011).
///
/// Sits between ChangeTracker (#12) and SaveSystem (#7), providing:
///   - Flag registry directory (CrossChapterFlagRegistry SO)
///   - New-game flag initialization (InitializeAllFlags → SetFlagRaw)
///   - IsImmutable flag protection during chapter replay
///   - Non-immutable flag reset on OnChapterReplayStarted
///   - Save/load bridge (GetPersistableFlags / RestoreFlags)
///
/// Does NOT own flag storage — ChangeTracker._flags is the sole source of truth.
/// All writes go through IChangeTrackerInternal.SetFlagRaw.
///
/// Constructor subscribes to ChapterManager.OnChapterReplayStarted.
/// Call Dispose() to unsubscribe (test teardown or game shutdown).
/// </summary>
public class CrossChapterTracker
{
    private readonly CrossChapterFlagRegistry _registry;
    private readonly IChangeTrackerInternal _changeTracker;

    public CrossChapterTracker(
        CrossChapterFlagRegistry registry,
        IChangeTrackerInternal changeTracker)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _changeTracker = changeTracker ?? throw new ArgumentNullException(nameof(changeTracker));

        // Wire IsImmutable guard into ChangeTracker
        _changeTracker.SetImmutableFlagCheck(IsFlagImmutable);

        // Subscribe to chapter replay events from ChapterManager (#15)
        ChapterManager.OnChapterReplayStarted += HandleChapterReplayStarted;
    }

    /// <summary>
    /// Unsubscribes from static events. Call in test teardown or when
    /// the tracker is no longer needed.
    /// </summary>
    public void Dispose()
    {
        ChapterManager.OnChapterReplayStarted -= HandleChapterReplayStarted;
        _changeTracker.SetImmutableFlagCheck(null);
    }

    // =========================================================================
    // Story 001: New Game Initialization
    // =========================================================================

    /// <summary>
    /// Initializes all registered flags to their DefaultValue via SetFlagRaw.
    /// Called once on new game start. Idempotent — subsequent calls overwrite
    /// existing values.
    /// </summary>
    public void InitializeAllFlags()
    {
        if (_registry.Flags == null) return;

        foreach (var def in _registry.Flags)
        {
            if (!string.IsNullOrEmpty(def.FlagId))
                _changeTracker.SetFlagRaw(def.FlagId, def.DefaultValue);
        }
    }

    // =========================================================================
    // Story 002: IsImmutable Protection + Replay Lifecycle
    // =========================================================================

    /// <summary>
    /// Handles ChapterManager.OnChapterReplayStarted.
    /// Immutable flags are preserved. Non-immutable flags are reset to DefaultValue
    /// so the player can make different choices during replay.
    /// </summary>
    private void HandleChapterReplayStarted(string chapterKey)
    {
        if (_registry.Flags == null) return;

        var flagsInChapter = _registry.Flags
            .Where(f => f.SetInChapter == chapterKey);

        foreach (var def in flagsInChapter)
        {
            if (string.IsNullOrEmpty(def.FlagId)) continue;

            if (def.IsImmutable)
            {
                // Protection: immutable flags are NOT reset
                // Their current value persists into the replay
                continue;
            }

            // Non-immutable: reset to default for fresh replay
            _changeTracker.SetFlagRaw(def.FlagId, def.DefaultValue);
        }
    }

    /// <summary>
    /// Returns true if the given flag is registered as IsImmutable and
    /// thus SetFlag(false) should be rejected when current value is true.
    /// This is the callback wired into ChangeTrackerCore via SetImmutableFlagCheck.
    /// </summary>
    private bool IsFlagImmutable(string flagId)
    {
        if (_registry.Flags == null) return false;

        foreach (var def in _registry.Flags)
        {
            if (def.FlagId == flagId && def.IsImmutable)
                return true;
        }
        return false;
    }

    // =========================================================================
    // Story 003: Persistence Bridge
    // =========================================================================

    /// <summary>
    /// Returns a snapshot of all registry-tracked flags with their current values.
    /// Flags in the registry that haven't been set yet return their DefaultValue.
    /// Called by SaveManager during CollectSaveData.
    /// </summary>
    public Dictionary<string, bool> GetPersistableFlags()
    {
        var allFlags = _changeTracker.GetAllFlags();
        var result = new Dictionary<string, bool>();

        if (_registry.Flags != null)
        {
            foreach (var def in _registry.Flags)
            {
                if (string.IsNullOrEmpty(def.FlagId)) continue;

                if (allFlags.TryGetValue(def.FlagId, out bool value))
                    result[def.FlagId] = value;
                else
                    result[def.FlagId] = def.DefaultValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Restores flags from a saved dictionary. Orphan flags (in save but not in
    /// registry) are still restored with a warning — they may be consumed by
    /// conditions even without registry entries. Registry flags missing from the
    /// save get their DefaultValue.
    /// Called by SaveManager during RestoreFromSave.
    /// </summary>
    public void RestoreFlags(Dictionary<string, bool> savedFlags)
    {
        if (savedFlags == null) return;

        // Restore flags present in save
        foreach (var kv in savedFlags)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;

            bool inRegistry = _registry.Flags != null &&
                _registry.Flags.Any(f => f.FlagId == kv.Key);

            if (!inRegistry)
            {
                Debug.LogWarning(
                    $"CrossChapterTracker: Saved flag '{kv.Key}' not found in " +
                    $"CrossChapterFlagRegistry — value preserved but not tracked by registry.");
            }

            _changeTracker.SetFlagRaw(kv.Key, kv.Value);
        }

        // Flags in registry but NOT in save → set to DefaultValue
        if (_registry.Flags != null)
        {
            foreach (var def in _registry.Flags)
            {
                if (string.IsNullOrEmpty(def.FlagId)) continue;
                if (!savedFlags.ContainsKey(def.FlagId))
                {
                    _changeTracker.SetFlagRaw(def.FlagId, def.DefaultValue);
                }
            }
        }
    }
}
