using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Time abstraction so <see cref="AutoSaveManager"/> is testable without
/// depending on <see cref="Time.time"/> directly.
/// </summary>
public interface ITimeProvider
{
    float Time { get; }
}

/// <summary>
/// Production <see cref="ITimeProvider"/> — delegates to Unity's Time.time.
/// </summary>
public class UnityTimeProvider : ITimeProvider
{
    public float Time => UnityEngine.Time.time;
}

/// <summary>
/// Drives automatic saving at predefined trigger points. Subscribes to
/// <see cref="AutoSaveTriggers"/> static events in production; triggers
/// can also be called directly for testing.
///
/// Auto-save is always silent — no UI notification, no sound effect.
/// Failures are logged but never surfaced to the player.
/// </summary>
public class AutoSaveManager
{
    private readonly SaveOrchestrator _orchestrator;
    private readonly SaveManager _saveManager;
    private readonly ITimeProvider _time;

    private float _lastAutoSaveTime = float.MinValue;
    private const float AutoSaveDebounceSeconds = 30f;
    private const string AutoSaveSlot = "auto_save";

    public float LastAutoSaveTime => _lastAutoSaveTime;

    /// <param name="orchestrator">CollectSaveData source.</param>
    /// <param name="saveManager">SaveAsync target.</param>
    /// <param name="timeProvider">Time source for debounce (use <see cref="UnityTimeProvider"/> in production).</param>
    public AutoSaveManager(
        SaveOrchestrator orchestrator,
        SaveManager saveManager,
        ITimeProvider timeProvider)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _saveManager = saveManager ?? throw new ArgumentNullException(nameof(saveManager));
        _time = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    // =========================================================================
    // Public Trigger API
    // =========================================================================

    /// <summary>
    /// Called when a chapter starts. Always triggers — not subject to debounce.
    /// </summary>
    public async Task TriggerChapterStart(string chapterKey)
    {
        await ExecuteAutoSave("chapter_start");
    }

    /// <summary>
    /// Called when a chapter completes. Always triggers — not subject to debounce.
    /// </summary>
    public async Task TriggerChapterComplete(string chapterKey)
    {
        await ExecuteAutoSave("chapter_complete");
    }

    /// <summary>
    /// Called after a critical choice that changes the ChangeOverlay.
    /// Subject to 30-second debounce — skipped if an auto-save occurred
    /// within the debounce window.
    /// </summary>
    public async Task TriggerCriticalChoice(string fragmentId)
    {
        if (_time.Time - _lastAutoSaveTime < AutoSaveDebounceSeconds)
        {
            Debug.Log($"[AutoSave] Skipped — within debounce window ({_time.Time - _lastAutoSaveTime:F1}s < {AutoSaveDebounceSeconds}s)");
            return;
        }
        await ExecuteAutoSave("critical_choice");
    }

    /// <summary>
    /// Synchronous save for application quit. Blocks up to 500ms —
    /// gives up silently if the save does not complete in time.
    /// </summary>
    public void TriggerApplicationQuit()
    {
        try
        {
            var data = _orchestrator.CollectSaveData();
            var task = _saveManager.SaveAsync(AutoSaveSlot, data);
            if (!task.Wait(TimeSpan.FromMilliseconds(500)))
            {
                Debug.LogWarning("[AutoSave] OnApplicationQuit save timed out — discarding");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AutoSave] OnApplicationQuit failed: {ex.Message}");
        }
    }

    // =========================================================================
    // Event Wiring
    // =========================================================================

    /// <summary>
    /// Subscribes to trigger events. Call once during initialisation.
    /// Unsubscribe with <see cref="UnsubscribeFromEvents"/> on teardown.
    /// </summary>
    public void SubscribeToEvents()
    {
        AutoSaveTriggers.OnChapterStarted += HandleChapterStarted;
        AutoSaveTriggers.OnChapterCompleted += HandleChapterCompleted;
        AutoSaveTriggers.OnCriticalChoice += HandleCriticalChoice;
        Application.quitting += HandleApplicationQuit;
    }

    /// <summary>
    /// Unsubscribes all event handlers. Call during teardown / OnDestroy.
    /// </summary>
    public void UnsubscribeFromEvents()
    {
        AutoSaveTriggers.OnChapterStarted -= HandleChapterStarted;
        AutoSaveTriggers.OnChapterCompleted -= HandleChapterCompleted;
        AutoSaveTriggers.OnCriticalChoice -= HandleCriticalChoice;
        Application.quitting -= HandleApplicationQuit;
    }

    // =========================================================================
    // Event Handlers
    // =========================================================================

    private async void HandleChapterStarted(string chapterKey) =>
        await TriggerChapterStart(chapterKey);

    private async void HandleChapterCompleted(string chapterKey) =>
        await TriggerChapterComplete(chapterKey);

    private async void HandleCriticalChoice(string fragmentId) =>
        await TriggerCriticalChoice(fragmentId);

    private void HandleApplicationQuit() => TriggerApplicationQuit();

    // =========================================================================
    // Core Save Logic
    // =========================================================================

    private async Task ExecuteAutoSave(string trigger)
    {
        try
        {
            var data = _orchestrator.CollectSaveData();
            await _saveManager.SaveAsync(AutoSaveSlot, data);
            _lastAutoSaveTime = _time.Time;
        }
        catch (SaveFileException ex)
        {
            Debug.LogWarning($"[AutoSave] Failed ({trigger}): {ex.Message}");
        }
    }
}

/// <summary>
/// Static event declarations for auto-save triggers. These events
/// are fired by the producer systems (ChapterManager, ChangeTracker)
/// and consumed by <see cref="AutoSaveManager"/>.
///
/// When the producer systems are implemented, each event's declaration
/// moves into its owning class per ADR-0001.
/// </summary>
public static class AutoSaveTriggers
{
    /// <summary>Fired by ChapterManager when a chapter starts.</summary>
    public static event Action<string> OnChapterStarted;

    /// <summary>Fired by ChapterManager when a chapter completes.</summary>
    public static event Action<string> OnChapterCompleted;

    /// <summary>Fired by ChangeTracker when the overlay changes due to a choice.</summary>
    public static event Action<string> OnCriticalChoice;

    /// <summary>
    /// Resets all events to null. Call in [TearDown] to prevent cross-test leakage.
    /// </summary>
    public static void ResetAll()
    {
        OnChapterStarted = null;
        OnChapterCompleted = null;
        OnCriticalChoice = null;
    }
}
