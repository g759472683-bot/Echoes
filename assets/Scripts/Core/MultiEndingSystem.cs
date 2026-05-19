using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Multi-ending resolution engine (ADR-0010, GDD Rule 11).
///
/// Evaluates all ending definitions for a chapter using a three-stage algorithm:
///   Stage 1: Collect EndingTriggers from all chapter fragments, grouped by EndingId
///   Stage 2: IsEssential gate — any essential trigger condition not met → disqualify
///   Stage 3: Accumulate ContributionWeight + EmotionalAffinity path bonus →
///            threshold check → tie-breaking → return winner
///
/// Pure function — same state always produces the same ResolvedEnding.
/// No caching of resolution results. UnlockedEndingIds is the only persistent state.
///
/// Constructor DI: IEndingDefinitionProvider, IDataManager, IChangeTracker, IEmotionalTagSystem
/// </summary>
public class MultiEndingSystem
{
    private readonly IEndingDefinitionProvider _endingProvider;
    private readonly IDataManager _dataManager;
    private readonly IChangeTracker _changeTracker;
    private readonly IEmotionalTagSystem _tagSystem;
    private HashSet<string> _unlockedEndingIds;
    private float _pathBonusWeight = 0.0f;

    /// <summary>
    /// Fired when a new ending ID is added to UnlockedEndingIds (union semantics).
    /// Not fired on duplicate unlocks. Subscribed by gallery (#20) and achievements.
    /// </summary>
    public static event Action<string> OnEndingUnlocked;

    public MultiEndingSystem(
        IEndingDefinitionProvider endingProvider,
        IDataManager dataManager,
        IChangeTracker changeTracker,
        IEmotionalTagSystem tagSystem)
    {
        _endingProvider = endingProvider;
        _dataManager = dataManager;
        _changeTracker = changeTracker;
        _tagSystem = tagSystem;
        _unlockedEndingIds = new HashSet<string>();
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Resolves the ending for a completed chapter using the three-stage algorithm.
    /// Always re-evaluates — no caching. Returns the winning ResolvedEnding.
    /// </summary>
    /// <param name="chapterId">The chapter to resolve (e.g., "ch01").</param>
    /// <returns>The resolved ending with score, type, and metadata.</returns>
    public ResolvedEnding ResolveEnding(string chapterId)
    {
        var endingDefs = _endingProvider.GetEndingDefinitions(chapterId);
        if (endingDefs == null || endingDefs.Count == 0)
        {
            Debug.LogError($"MultiEndingSystem: No ending definitions found for chapter '{chapterId}'.");
            return new ResolvedEnding("unknown", EndingType.ChapterEnding, 0f, true, false);
        }

        // Stage 1: Collect triggers from all fragments in the chapter
        var triggerGroups = CollectTriggers(chapterId, endingDefs);

        // Stage 2-3: Evaluate each ending definition
        var qualified = new List<(EndingDefinition Def, float Score, int EssentialCount)>();

        foreach (var def in endingDefs)
        {
            if (!triggerGroups.TryGetValue(def.EndingId, out var triggers))
                triggers = new List<EndingTrigger>();

            // Stage 2: IsEssential gate
            int essentialSatisfied = 0;
            bool allEssentialPassed = true;
            foreach (var trigger in triggers.Where(t => t.IsEssential))
            {
                if (trigger.TriggerCondition != null &&
                    trigger.TriggerCondition.Evaluate(_changeTracker))
                {
                    essentialSatisfied++;
                }
                else
                {
                    allEssentialPassed = false;
                    break;
                }
            }

            if (!allEssentialPassed)
                continue; // DISQUALIFIED — essential gate failed

            // Stage 3a: Accumulate ContributionWeight
            float score = 0f;
            foreach (var trigger in triggers)
            {
                if (trigger.TriggerCondition == null ||
                    trigger.TriggerCondition.Evaluate(_changeTracker))
                {
                    score += trigger.ContributionWeight;
                }
            }
            score = Mathf.Clamp01(score);

            // Stage 3b: EmotionalAffinity path bonus
            if (!string.IsNullOrEmpty(def.EmotionalAffinity))
            {
                var dominantEmotion = ComputeDominantPathEmotion(def.ChapterId);
                if (def.EmotionalAffinity == dominantEmotion)
                    score = Mathf.Clamp01(score * (1.0f + _pathBonusWeight));
            }

            // Stage 3c: Threshold check
            if (score >= def.MinimumScore)
                qualified.Add((def, score, essentialSatisfied));
        }

        // Stage 3d: Tie-breaking
        if (qualified.Count > 1)
        {
            var endingDefsList = endingDefs;
            qualified = qualified
                .OrderByDescending(q => q.Score)
                .ThenByDescending(q => q.EssentialCount)
                .ThenByDescending(q => !_unlockedEndingIds.Contains(q.Def.EndingId))
                .ThenBy(q => endingDefsList.IndexOf(q.Def))
                .ToList();
        }

        // Select winner or fallback
        EndingDefinition winner;
        float winnerScore;
        int winnerEssential;

        if (qualified.Count > 0)
        {
            var best = qualified[0];
            winner = best.Def;
            winnerScore = best.Score;
            winnerEssential = best.EssentialCount;
        }
        else
        {
            // Fallback: return default ending
            winner = endingDefs.FirstOrDefault(d => d.IsDefault);
            if (winner == null)
            {
                Debug.LogError($"MultiEndingSystem: Chapter '{chapterId}' has no IsDefault=true ending. " +
                               "Returning first definition as emergency fallback.");
                winner = endingDefs[0];
            }
            winnerScore = 0f;
            winnerEssential = 0;
        }

        // Compute dominant path emotion for the winning result
        string dominantPathEmotion = ComputeDominantPathEmotion(chapterId);

        // Build qualified endings list for the result
        var qualifiedList = new List<(string EndingId, float Score)>();
        foreach (var q in qualified)
            qualifiedList.Add((q.Def.EndingId, q.Score));

        // Record unlock and check novelty
        bool isNewUnlock = RecordUnlock(winner.EndingId);

        return new ResolvedEnding(
            endingId: winner.EndingId,
            endingType: winner.EndingType,
            score: winnerScore,
            isDefault: winner.IsDefault || qualified.Count == 0,
            isNewUnlock: isNewUnlock,
            qualifiedEndings: qualifiedList,
            dominantPathEmotion: dominantPathEmotion
        );
    }

    /// <summary>
    /// Lifecycle marker — called when a chapter starts.
    /// Idempotent. Resolves nothing; just a hook for future preloading.
    /// </summary>
    public void OnChapterStart(string chapterId) { }

    /// <summary>
    /// Returns a copy of the current unlocked ending IDs set.
    /// </summary>
    public HashSet<string> GetUnlockedEndingIds() => new HashSet<string>(_unlockedEndingIds);

    /// <summary>
    /// Serializes persistent state for save system bridge.
    /// </summary>
    public MultiEndingSaveData GetSaveData()
    {
        return new MultiEndingSaveData { UnlockedEndingIds = _unlockedEndingIds.ToArray() };
    }

    /// <summary>
    /// Restores persistent state from save system bridge.
    /// </summary>
    public void Restore(MultiEndingSaveData data)
    {
        _unlockedEndingIds = new HashSet<string>(data.UnlockedEndingIds ?? Array.Empty<string>());
    }

    // =========================================================================
    // Stage 1: Trigger Collection
    // =========================================================================

    /// <summary>
    /// Collects all EndingTriggers from all fragments in the chapter, grouped by EndingId.
    /// Orphan triggers (EndingId not in any EndingDefinition) are logged as warnings
    /// and excluded from the result.
    /// </summary>
    private Dictionary<string, List<EndingTrigger>> CollectTriggers(
        string chapterId, List<EndingDefinition> endingDefs)
    {
        var fragments = _dataManager.GetFragmentsByChapter(chapterId);
        var grouped = new Dictionary<string, List<EndingTrigger>>();
        var validEndingIds = new HashSet<string>(endingDefs.Select(d => d.EndingId));

        foreach (var frag in fragments)
        {
            if (frag == null || frag.EndingTriggers == null)
                continue;

            foreach (var trigger in frag.EndingTriggers)
            {
                if (!validEndingIds.Contains(trigger.EndingId))
                {
                    Debug.LogWarning(
                        $"MultiEndingSystem: Trigger references unknown EndingId " +
                        $"'{trigger.EndingId}' — ignored.");
                    continue;
                }

                if (!grouped.ContainsKey(trigger.EndingId))
                    grouped[trigger.EndingId] = new List<EndingTrigger>();
                grouped[trigger.EndingId].Add(trigger);
            }
        }

        return grouped;
    }

    // =========================================================================
    // Path Emotion Computation
    // =========================================================================

    /// <summary>
    /// Computes the dominant emotional path across all visited fragments in the chapter.
    /// Counts the dominant category for each visited fragment and returns the most
    /// frequent category. Returns null if no visited fragments have tags.
    /// </summary>
    private string ComputeDominantPathEmotion(string chapterId)
    {
        var fragments = _dataManager.GetFragmentsByChapter(chapterId);
        var categoryCounts = new Dictionary<string, int>();

        foreach (var frag in fragments)
        {
            if (frag == null || string.IsNullOrEmpty(frag.FragmentId))
                continue;

            if (!_changeTracker.HasVisited(frag.FragmentId))
                continue;

            var dominant = _tagSystem.GetFragmentDominantCategory(frag.FragmentId);
            if (!string.IsNullOrEmpty(dominant))
            {
                categoryCounts.TryGetValue(dominant, out int count);
                categoryCounts[dominant] = count + 1;
            }
        }

        if (categoryCounts.Count == 0)
            return null;

        return categoryCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
    }

    // =========================================================================
    // Persistence
    // =========================================================================

    /// <summary>
    /// Records an ending ID as unlocked. Union semantics — existing IDs are preserved.
    /// Fires OnEndingUnlocked on first-time unlocks. Returns true if newly unlocked.
    /// </summary>
    private bool RecordUnlock(string endingId)
    {
        if (_unlockedEndingIds.Add(endingId))
        {
            OnEndingUnlocked?.Invoke(endingId);
            return true;
        }
        return false;
    }
}
