using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Lightweight progress state machine and coordinator for chapter progression
/// (GDD chapter-management, TR-chapter-management-001–006).
///
/// Owns: current chapter/fragment references, 3-state machine (IDLE/IN_CHAPTER/TRANSITIONING),
/// chapter completion detection, completion transition flow, replay logic, and linear unlock.
///
/// Fragment navigation is driven by the web association engine (#13), not SequenceIndex
/// linear order. Completion detection uses dual conditions: (A) all fragments visited OR
/// (B) visitedRatio >= CompletionRatio AND best candidate score < COMPLETION_ASSOCIATION_THRESHOLD.
///
/// Implements <see cref="IChapterSaveRestore"/> for SaveOrchestrator integration.
///
/// Constructor DI: IDataManager, IEndingResolver, IAssociationProvider, IChapterSceneProvider, autoSaveFunc
/// </summary>
public class ChapterManager : IChapterSaveRestore
{
    private readonly IDataManager _dataManager;
    private readonly IEndingResolver _endingResolver;
    private readonly IAssociationProvider _associationProvider;
    private readonly IChapterSceneProvider _sceneProvider;
    private readonly Func<Task> _autoSave;

    /// <summary>Threshold below which the best association candidate triggers chapter completion.</summary>
    public const float COMPLETION_ASSOCIATION_THRESHOLD = 0.30f;

    /// <summary>Default ratio of fragments that must be visited to trigger completion check.</summary>
    public const float DEFAULT_COMPLETION_RATIO = 0.6f;

    // =========================================================================
    // Public State
    // =========================================================================

    public ChapterState CurrentState { get; private set; } = ChapterState.Idle;
    public string CurrentChapterKey { get; private set; }
    public string CurrentFragmentId { get; private set; }
    public int CurrentFragmentIndex { get; private set; }

    // =========================================================================
    // Internal State
    // =========================================================================

    private HashSet<string> _chapterVisitedFragments = new HashSet<string>();
    private HashSet<string> _sessionVisitedFragments = new HashSet<string>();
    private List<string> _recentHistory = new List<string>();
    private HashSet<string> _completedChapters = new HashSet<string>();
    private HashSet<string> _unlockedChapters = new HashSet<string>();
    private bool _preloadNotYetTriggered;
    private int _totalFragmentsInChapter;
    private float _cachedCompletionRatio = DEFAULT_COMPLETION_RATIO;
    private bool _allowReplay = true;

    // =========================================================================
    // Static Events (ADR-0001)
    // =========================================================================

    /// <summary>Fired when a chapter begins (new game, load, replay, or transition).</summary>
    public static event Action<string> OnChapterStarted;

    /// <summary>Fired when a chapter is completed.</summary>
    public static event Action<string> OnChapterCompleted;

    /// <summary>Fired when the player transitions between fragments.</summary>
    public static event Action<string, string> OnFragmentChanged;

    /// <summary>Fired when all chapters are completed (end of game).</summary>
    public static event Action OnAllChaptersCompleted;

    /// <summary>Fired before chapter replay state reset — consumed by CrossChapterTracker.</summary>
    public static event Action<string> OnChapterReplayStarted;

    // =========================================================================
    // Constructor
    // =========================================================================

    public ChapterManager(
        IDataManager dataManager,
        IEndingResolver endingResolver,
        IAssociationProvider associationProvider,
        IChapterSceneProvider sceneProvider,
        Func<Task> autoSave)
    {
        _dataManager = dataManager;
        _endingResolver = endingResolver;
        _associationProvider = associationProvider;
        _sceneProvider = sceneProvider;
        _autoSave = autoSave;
    }

    // =========================================================================
    // New Game / Load & Restore
    // =========================================================================

    /// <summary>
    /// Starts a new game — unlocks only the first chapter (OrderIndex=0) and transitions to it.
    /// </summary>
    public async Task StartNewGame()
    {
        var chapters = _dataManager.GetFragmentsByChapter(null);
        // GetAllChapters not directly available — use the ChapterDefinition EndingsProvider
        // Actually we need all chapter definitions. Use the data manager's knowledge.
        // The DataManager stores chapter keys. Since we don't have GetAllChapters(),
        // we receive chapter list from the caller or use a known set.
        // For MVP: hardcode Ch01 as first, Ch02 as second — the caller provides the list.

        // We need the list of all chapters. Let's get it from chapter definitions.
        // Since we only have GetFragmentsByChapter and GetChapterAsync, and we need
        // the ordered list — we'll receive it from a higher-level orchestrator.

        // For now, use a known-first-chapter approach:
        CurrentState = ChapterState.Transitioning;

        // Unlock first chapter (lowest OrderIndex)
        // In MVP: hardcoded to "ch01" — the game concept has 2 linear chapters
        _unlockedChapters = new HashSet<string> { "ch01" };

        await EnterChapter("ch01");
    }

    /// <summary>
    /// Enters a chapter at its EntryFragmentId.
    /// Fires OnChapterStarted after the transition completes.
    /// </summary>
    public async Task EnterChapter(string chapterKey)
    {
        CurrentState = ChapterState.Transitioning;
        CurrentChapterKey = chapterKey;

        var chapterDef = await _dataManager.GetChapterAsync(chapterKey);
        if (chapterDef == null)
        {
            Debug.LogError($"ChapterManager: ChapterDefinition not found for '{chapterKey}'.");
            CurrentState = ChapterState.Idle;
            return;
        }

        var entryFragmentId = chapterDef.EntryFragmentId;
        var fragments = _dataManager.GetFragmentsByChapter(chapterKey);
        _totalFragmentsInChapter = fragments?.Count ?? 0;
        _cachedCompletionRatio = chapterDef.CompletionRatio;
        _allowReplay = chapterDef.AllowReplay;

        if (_totalFragmentsInChapter == 0)
        {
            Debug.LogError($"ChapterManager: Chapter '{chapterKey}' has 0 fragments — auto-completing.");
            await ExecuteChapterCompletion(chapterKey);
            return;
        }

        _chapterVisitedFragments.Clear();
        _recentHistory.Clear();
        _preloadNotYetTriggered = false;

        _endingResolver.OnChapterStart(chapterKey);

        await _sceneProvider.TransitionToFragmentAsync(chapterKey, entryFragmentId);

        CurrentFragmentId = entryFragmentId;
        CurrentFragmentIndex = 0;
        CurrentState = ChapterState.InChapter;

        _chapterVisitedFragments.Add(entryFragmentId);
        _sessionVisitedFragments.Add(entryFragmentId);

        // Preload next chapter assets at first opportunity
        if (!_preloadNotYetTriggered)
        {
            var nextChapter = GetNextChapterKey(chapterKey);
            if (nextChapter != null)
            {
                _ = _sceneProvider.PreloadChapterAsync(nextChapter);
                _preloadNotYetTriggered = true;
            }
        }

        OnChapterStarted?.Invoke(chapterKey);
    }

    // =========================================================================
    // Fragment Navigation (Story 001)
    // =========================================================================

    /// <summary>
    /// Transitions to a target fragment within the current chapter.
    /// Blocks if CurrentState is not IN_CHAPTER. Drives discovery tracking,
    /// preload threshold check, and completion detection after transition.
    /// </summary>
    public async Task TransitionToFragment(string targetFragmentId)
    {
        if (CurrentState != ChapterState.InChapter)
        {
            Debug.LogWarning($"ChapterManager: Cannot transition — current state is {CurrentState}.");
            return;
        }

        if (targetFragmentId == CurrentFragmentId)
            return; // No-op — already on this fragment

        // Validate target belongs to current chapter
        var fragments = _dataManager.GetFragmentsByChapter(CurrentChapterKey);
        var targetFrag = fragments?.FirstOrDefault(f => f != null && f.FragmentId == targetFragmentId);
        if (targetFrag == null)
        {
            Debug.LogWarning(
                $"ChapterManager: Fragment '{targetFragmentId}' not found in chapter '{CurrentChapterKey}'.");
            return;
        }

        var previousFragmentId = CurrentFragmentId;
        CurrentState = ChapterState.Transitioning;

        await _sceneProvider.TransitionToFragmentAsync(CurrentChapterKey, targetFragmentId);

        CurrentFragmentId = targetFragmentId;
        CurrentFragmentIndex = fragments.IndexOf(targetFrag);
        CurrentState = ChapterState.InChapter;

        // Update tracking
        _chapterVisitedFragments.Add(targetFragmentId);
        _sessionVisitedFragments.Add(targetFragmentId);

        // Update recent history (sliding window of K=4 for rhythm penalty)
        _recentHistory.Add(targetFragmentId);
        if (_recentHistory.Count > 4)
            _recentHistory.RemoveAt(0);

        OnFragmentChanged?.Invoke(previousFragmentId, targetFragmentId);

        // Check preload threshold (Story 001: when ≤3 unvisited fragments remain)
        CheckAndTriggerPreload();

        // Check chapter completion (Story 002)
        if (CheckChapterCompletion(CurrentChapterKey))
        {
            await ExecuteChapterCompletion(CurrentChapterKey);
        }
    }

    // =========================================================================
    // Preload Trigger (Story 001)
    // =========================================================================

    private void CheckAndTriggerPreload()
    {
        if (_preloadNotYetTriggered)
            return;

        int visitedCount = _chapterVisitedFragments.Count;
        int remainingUnvisited = _totalFragmentsInChapter - visitedCount;

        if (remainingUnvisited <= 3)
        {
            var nextChapter = GetNextChapterKey(CurrentChapterKey);
            if (nextChapter != null)
            {
                _ = _sceneProvider.PreloadChapterAsync(nextChapter);
                _preloadNotYetTriggered = true;
            }
        }
    }

    // =========================================================================
    // Chapter Completion Detection (Story 002)
    // =========================================================================

    /// <summary>
    /// Dual-condition chapter completion check:
    /// (A) All fragments visited — always complete.
    /// (B) visitedRatio >= CompletionRatio AND best association candidate score < 0.30.
    /// </summary>
    public bool CheckChapterCompletion(string chapterKey)
    {
        var fragments = _dataManager.GetFragmentsByChapter(chapterKey);
        int totalFragments = fragments?.Count ?? 0;

        if (totalFragments == 0)
        {
            Debug.LogError($"ChapterManager: Chapter '{chapterKey}' has 0 fragments — auto-completing.");
            return true;
        }

        int visitedCount = _chapterVisitedFragments.Count;

        // Condition A: All fragments visited
        if (visitedCount >= totalFragments)
            return true;

        // Condition B: Ratio met + association threshold
        float visitedRatio = (float)visitedCount / totalFragments;
        float completionRatio = _cachedCompletionRatio;

        if (visitedRatio >= completionRatio)
        {
            var candidates = _associationProvider.ComputeAssociations(
                CurrentFragmentId, chapterKey, _recentHistory, _sessionVisitedFragments);

            if (candidates == null || candidates.Count == 0)
                return true; // No candidates — chapter naturally exhausted

            float bestScore = candidates[0].CompositeScore;
            if (bestScore < COMPLETION_ASSOCIATION_THRESHOLD)
                return true;
        }

        return false;
    }

    // =========================================================================
    // Chapter Completion Transition Flow (Story 003)
    // =========================================================================

    /// <summary>
    /// 5-step completion flow:
    /// 1. Resolve ending  2. Update progression state  3. Auto-save
    /// 4. Transition to next chapter  5. Fire completion events
    /// </summary>
    public async Task ExecuteChapterCompletion(string chapterKey)
    {
        // Step 1: Resolve ending
        ResolvedEnding ending;
        try
        {
            ending = _endingResolver.ResolveEnding(chapterKey);
        }
        catch (Exception e)
        {
            Debug.LogError($"ChapterManager: ResolveEnding failed for '{chapterKey}': {e.Message}");
            throw;
        }

        // Step 2: Update progression state
        _completedChapters.Add(chapterKey); // HashSet — idempotent

        string nextChapterKey = GetNextChapterKey(chapterKey);
        if (nextChapterKey != null)
            _unlockedChapters.Add(nextChapterKey); // Union semantics

        // Step 3: Auto-save (MUST complete before transition)
        if (_autoSave != null)
        {
            try
            {
                await _autoSave();
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"ChapterManager: Auto-save failed during chapter completion: {e.Message}");
                // Continue — don't block the player
            }
        }

        // Fire OnChapterCompleted
        OnChapterCompleted?.Invoke(chapterKey);

        // Step 4: Chapter transition
        if (nextChapterKey != null)
        {
            CurrentState = ChapterState.Transitioning;
            await _sceneProvider.TransitionToChapterAsync(nextChapterKey);
            await EnterChapter(nextChapterKey);
        }
        else
        {
            // Step 5: All chapters completed
            CurrentState = ChapterState.Transitioning;
            OnAllChaptersCompleted?.Invoke();
        }
    }

    // =========================================================================
    // Chapter Replay (Story 004)
    // =========================================================================

    /// <summary>
    /// Replays a previously completed chapter. Preserves overlay, flags, and
    /// persistent chapter state. Resets visit records, recent history, and
    /// preload trigger. Fires OnChapterReplayStarted before state reset.
    /// </summary>
    public async Task ReplayChapter(string chapterKey)
    {
        if (!_completedChapters.Contains(chapterKey))
        {
            Debug.LogWarning($"ChapterManager: Cannot replay incomplete chapter '{chapterKey}'.");
            return;
        }

        if (!_allowReplay)
        {
            Debug.LogWarning($"ChapterManager: Chapter '{chapterKey}' does not allow replay.");
            return;
        }

        // Fire replay event BEFORE clearing state — CrossChapterTracker needs
        // to see current flags for IsImmutable protection
        OnChapterReplayStarted?.Invoke(chapterKey);

        // Reset session state (NOT overlay/flags — those persist across replays)
        _chapterVisitedFragments.Clear();
        _sessionVisitedFragments = new HashSet<string>();
        _recentHistory.Clear();
        _preloadNotYetTriggered = false;

        // Reload chapter definition for fresh entry fragment, CompletionRatio, etc.
        var chapterDef = await _dataManager.GetChapterAsync(chapterKey);
        string entryFragId = chapterDef?.EntryFragmentId ?? "frag_01";
        _cachedCompletionRatio = chapterDef?.CompletionRatio ?? DEFAULT_COMPLETION_RATIO;
        _allowReplay = chapterDef?.AllowReplay ?? true;
        _totalFragmentsInChapter = _dataManager.GetFragmentsByChapter(chapterKey).Count;

        // Load entry fragment
        CurrentState = ChapterState.Transitioning;
        CurrentChapterKey = chapterKey;
        _endingResolver.OnChapterStart(chapterKey);

        await _sceneProvider.TransitionToFragmentAsync(chapterKey, entryFragId);

        CurrentFragmentId = entryFragId;
        CurrentFragmentIndex = 0;
        CurrentState = ChapterState.InChapter;

        _chapterVisitedFragments.Add(entryFragId);
        _sessionVisitedFragments.Add(entryFragId);

        OnChapterStarted?.Invoke(chapterKey);
    }

    // =========================================================================
    // Linear Chapter Unlock (Story 004)
    // =========================================================================

    /// <summary>
    /// Returns the next chapter key after the given chapter, based on OrderIndex.
    /// Returns null if this is the last chapter.
    /// </summary>
    private string GetNextChapterKey(string currentChapterKey)
    {
        // Since IDataManager doesn't have GetAllChapters(), we use a simple approach:
        // The chapter keys follow a naming convention. For MVP with 2 chapters:
        if (currentChapterKey == "ch01") return "ch02";
        if (currentChapterKey == "ch02") return null;

        // For extensibility: try numeric suffix pattern
        if (currentChapterKey.StartsWith("ch") &&
            int.TryParse(currentChapterKey.Substring(2), out int orderIndex))
        {
            return $"ch{orderIndex + 1:D2}";
        }

        return null;
    }

    /// <summary>
    /// Returns a copy of the unlocked chapter keys set.
    /// </summary>
    public HashSet<string> GetUnlockedChaptersSet() => new HashSet<string>(_unlockedChapters);

    // =========================================================================
    // IChapterSaveRestore Implementation
    // =========================================================================

    string IChapterSaveRestore.CurrentChapterKey => CurrentChapterKey;
    string IChapterSaveRestore.CurrentFragmentId => CurrentFragmentId;
    int IChapterSaveRestore.CurrentFragmentIndex => CurrentFragmentIndex;

    string[] IChapterSaveRestore.GetCompletedChapters() => _completedChapters.ToArray();
    string[] IChapterSaveRestore.GetUnlockedChapters() => _unlockedChapters.ToArray();

    /// <summary>
    /// Full restore path — called by SaveOrchestrator. Transitions to the saved fragment.
    /// If the saved chapter/fragment don't exist in the current build, falls back to
    /// the first chapter's EntryFragmentId.
    /// </summary>
    async Task IChapterSaveRestore.LoadAndRestore(SaveData data)
    {
        _completedChapters = new HashSet<string>(data.CompletedChapters ?? Array.Empty<string>());
        _unlockedChapters = new HashSet<string>(data.UnlockedChapters ?? Array.Empty<string>());

        // Validate saved chapter/fragment still exist
        var chapterKey = data.CurrentChapterKey;
        var fragmentId = data.CurrentFragmentId;

        if (!string.IsNullOrEmpty(chapterKey))
        {
            try
            {
                var fragments = _dataManager.GetFragmentsByChapter(chapterKey);
                bool fragmentExists = fragments?.Any(f => f != null && f.FragmentId == fragmentId) == true;

                if (!fragmentExists)
                {
                    Debug.LogWarning(
                        $"ChapterManager: Saved fragment '{fragmentId}' not found in chapter " +
                        $"'{chapterKey}'. Falling back to chapter entry fragment.");
                    var chapterDef = await _dataManager.GetChapterAsync(chapterKey);
                    fragmentId = chapterDef?.EntryFragmentId ?? fragmentId;
                }
            }
            catch
            {
                Debug.LogWarning(
                    $"ChapterManager: Saved chapter '{chapterKey}' not found. " +
                    "Falling back to first chapter.");
                chapterKey = "ch01";
                var chapterDef = await _dataManager.GetChapterAsync(chapterKey);
                fragmentId = chapterDef?.EntryFragmentId ?? "frag_01";
            }
        }
        else
        {
            chapterKey = "ch01";
            var chapterDef = await _dataManager.GetChapterAsync(chapterKey);
            fragmentId = chapterDef?.EntryFragmentId ?? "frag_01";
        }

        CurrentState = ChapterState.Transitioning;
        CurrentChapterKey = chapterKey;

        _chapterVisitedFragments.Clear();
        _recentHistory.Clear();
        _preloadNotYetTriggered = false;
        _sessionVisitedFragments = new HashSet<string>();

        // Cache chapter definition values
        try
        {
            var chapterDef = await _dataManager.GetChapterAsync(chapterKey);
            _cachedCompletionRatio = chapterDef?.CompletionRatio ?? DEFAULT_COMPLETION_RATIO;
            _allowReplay = chapterDef?.AllowReplay ?? true;
            _totalFragmentsInChapter = _dataManager.GetFragmentsByChapter(chapterKey)?.Count ?? 0;
        }
        catch
        {
            _cachedCompletionRatio = DEFAULT_COMPLETION_RATIO;
            _allowReplay = true;
        }

        _endingResolver.OnChapterStart(chapterKey);

        await _sceneProvider.TransitionToFragmentAsync(chapterKey, fragmentId);

        CurrentFragmentId = fragmentId;
        CurrentFragmentIndex = 0;
        CurrentState = ChapterState.InChapter;

        _chapterVisitedFragments.Add(fragmentId);
        _sessionVisitedFragments.Add(fragmentId);

        OnChapterStarted?.Invoke(chapterKey);
    }
}
