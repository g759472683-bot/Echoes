using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# multi-factor association scoring engine (ADR-0009).
///
/// Answers: "Which memories are most relevant to the current moment?"
/// Stateless — ComputeAssociations is a pure function. Same inputs produce
/// same outputs. No MonoBehaviour, no Unity dependencies — fully unit-testable.
///
/// Four-factor formula:
///   Score = (A × 0.6 + B × 0.4) × C × D
///
///   A = Cosine tag similarity via TagSimilarityMatrix [0.0, 1.0]
///   B = Explicit association weight [0.0, 1.0], -1.0 = designer exclusion
///   C = Rhythm penalty (sliding window K=4) [0.1, 1.3]
///   D = Discovery boost (unvisited bonus + revisit decay) [0.3, 1.3]
///
/// Candidate pool: same chapter, unlocked (condition met), exclude self.
/// Returns top 5 candidates sorted by compositeScore descending.
/// </summary>
public class WebAssociationEngine
{
    // =========================================================================
    // Formula Constants (ADR-0009 §Decision + GDD §Tuning Knobs)
    // =========================================================================

    public const float WEIGHT_A = 0.6f;
    public const float WEIGHT_B = 0.4f;
    public const float B_EXCLUSION = -1.0f;
    public const float BIDIRECTIONAL_BONUS = 0.15f;

    // Factor C — Rhythm Penalty
    public const int RHYTHM_WINDOW_K = 4;
    public static readonly float[] PENALTY_BY_POSITION = { 0.70f, 0.55f, 0.40f, 0.25f };
    public const float C_MIN = 0.10f;
    public const float C_MAX = 1.30f;
    public const float PEACE_BONUS = 1.30f;
    public const float ADAPTIVE_HALVING = 0.5f;

    // Factor D — Discovery Boost
    public const float D_UNVISITED = 1.30f;
    public const float D_VISIT_DECAY = 0.30f;
    public const float D_MIN = 0.30f;
    public const float D_PENDING_CHANGES_FLOOR = 0.70f;

    // Ranking
    public const int TOP_N = 5;
    public const int MIN_CANDIDATE_POOL = 3;
    public const float SCORE_EXCLUSION_THRESHOLD = 0.05f;
    public const int ADAPTIVE_POOL_THRESHOLD = 5;

    // =========================================================================
    // Dependencies
    // =========================================================================

    private readonly IDataManager _dataManager;
    private readonly IEmotionalTagSystem _tagSystem;
    private readonly IChangeTracker _changeTracker;

    // =========================================================================
    // Construction
    // =========================================================================

    public WebAssociationEngine(
        IDataManager dataManager,
        IEmotionalTagSystem tagSystem,
        IChangeTracker changeTracker)
    {
        _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
        _tagSystem = tagSystem ?? throw new ArgumentNullException(nameof(tagSystem));
        _changeTracker = changeTracker ?? throw new ArgumentNullException(nameof(changeTracker));
    }

    // =========================================================================
    // ComputeAssociations (public API)
    // =========================================================================

    /// <summary>
    /// Computes the top-N association candidates for the given fragment context.
    /// Pure function — same inputs always produce the same outputs.
    /// </summary>
    public List<AssociationCandidate> ComputeAssociations(
        string currentFragmentId,
        string chapterKey,
        List<string> recentHistory,
        HashSet<string> visitedFragmentIds)
    {
        // Step 1: Build candidate pool
        var pool = BuildCandidatePool(currentFragmentId, chapterKey);
        if (pool.Count == 0) return new List<AssociationCandidate>();

        // Get current fragment for Factor A/B computation
        var currentFragment = FindFragmentInPool(currentFragmentId, pool) ??
                              LookupFragment(currentFragmentId, chapterKey);

        recentHistory ??= new List<string>();
        visitedFragmentIds ??= new HashSet<string>();

        // Step 2: Score each candidate
        var scored = new List<AssociationCandidate>(pool.Count);
        foreach (var candidate in pool)
        {
            var result = ScoreCandidate(
                currentFragment, candidate,
                recentHistory, visitedFragmentIds, pool.Count);
            scored.Add(result);
        }

        // Step 3: Sort descending by compositeScore
        scored.Sort((a, b) => b.CompositeScore.CompareTo(a.CompositeScore));

        // Step 4: Filter and take top N
        var result = new List<AssociationCandidate>(TOP_N);
        foreach (var candidate in scored)
        {
            // B=-1.0 sentinel — designer exclusion
            if (candidate.FactorB <= B_EXCLUSION + 0.001f) continue;

            // Score threshold — relaxed for small pools
            if (candidate.CompositeScore < SCORE_EXCLUSION_THRESHOLD && pool.Count > MIN_CANDIDATE_POOL)
                continue;

            result.Add(candidate);
            if (result.Count >= TOP_N) break;
        }

        return result;
    }

    // =========================================================================
    // Step 1: Candidate Pool
    // =========================================================================

    /// <summary>
    /// Builds the candidate pool: all fragments in the chapter that are
    /// unlocked and not the current fragment.
    /// </summary>
    public List<MemoryFragment> BuildCandidatePool(string currentFragmentId, string chapterKey)
    {
        var allFragments = _dataManager.GetFragmentsByChapter(chapterKey);
        if (allFragments == null) return new List<MemoryFragment>();

        var candidates = new List<MemoryFragment>(allFragments.Count);
        foreach (var f in allFragments)
        {
            if (f == null) continue;
            if (f.FragmentId == currentFragmentId) continue;

            // Check UnlockCondition — null means always unlocked
            if (f.UnlockCondition != null && !f.UnlockCondition.Evaluate(_changeTracker))
                continue;

            candidates.Add(f);
        }
        return candidates;
    }

    // =========================================================================
    // Step 2: Score a Single Candidate
    // =========================================================================

    private AssociationCandidate ScoreCandidate(
        MemoryFragment currentFragment,
        MemoryFragment candidate,
        List<string> recentHistory,
        HashSet<string> visitedFragmentIds,
        int poolSize)
    {
        // Factor A: Cosine tag similarity
        float factorA = ComputeFactorA(currentFragment?.EmotionalTags, candidate.EmotionalTags);

        // Factor B: Explicit association weight
        float factorB = ComputeFactorB(currentFragment, candidate);

        // B = -1.0 → designer exclusion sentinel
        if (factorB <= B_EXCLUSION + 0.001f)
        {
            return new AssociationCandidate(
                candidate.FragmentId, 0f, Strength.Trace,
                DominantFactor.ExplicitAssociation,
                factorA, factorB, 1f, 1f);
        }

        // Factor C: Rhythm penalty
        float factorC = ComputeFactorC(recentHistory, candidate, poolSize);

        // Factor D: Discovery boost
        float factorD = ComputeFactorD(candidate.FragmentId, visitedFragmentIds);

        // Composite: (A×0.6 + B×0.4) × C × D
        float tagScore = factorA * WEIGHT_A;
        float explicitScore = factorB * WEIGHT_B;
        float compositeScore = (tagScore + explicitScore) * factorC * factorD;

        Strength grade = DetermineGrade(compositeScore);
        DominantFactor dominant = DetermineDominantFactor(
            factorA, factorB, factorC, factorD, tagScore, explicitScore);

        return new AssociationCandidate(
            candidate.FragmentId, compositeScore, grade, dominant,
            factorA, factorB, factorC, factorD);
    }

    // =========================================================================
    // Factor A: Cosine Tag Similarity
    // =========================================================================

    /// <summary>
    /// Computes cosine similarity between two tag weight vectors.
    ///
    /// A = Σᵢ Σⱼ (w_i × w_j × M[tag_i][tag_j]) / (||w_current|| × ||w_candidate||)
    ///
    /// Returns 0 if either tag list is null or empty, or if both norms are zero.
    /// </summary>
    public float ComputeFactorA(List<EmotionalTag> currentTags, List<EmotionalTag> candidateTags)
    {
        if (currentTags == null || currentTags.Count == 0) return 0f;
        if (candidateTags == null || candidateTags.Count == 0) return 0f;

        float numerator = 0f;
        float currentNormSq = 0f;
        float candidateNormSq = 0f;

        // Compute norms first (single pass per list)
        foreach (var t in currentTags)
        {
            if (t.TagId == null) continue;
            float w = Clamp01(t.BaseWeight);
            currentNormSq += w * w;
        }
        foreach (var t in candidateTags)
        {
            if (t.TagId == null) continue;
            float w = Clamp01(t.BaseWeight);
            candidateNormSq += w * w;
        }

        if (currentNormSq < 0.0001f || candidateNormSq < 0.0001f) return 0f;

        // Compute weighted cross-product
        foreach (var t1 in currentTags)
        {
            if (t1.TagId == null) continue;
            float w1 = Clamp01(t1.BaseWeight);

            foreach (var t2 in candidateTags)
            {
                if (t2.TagId == null) continue;
                float w2 = Clamp01(t2.BaseWeight);
                float sim = GetTagSimilarity(t1.TagId, t2.TagId);
                numerator += w1 * w2 * sim;
            }
        }

        float currentNorm = Sqrt(currentNormSq);
        float candidateNorm = Sqrt(candidateNormSq);
        float a = numerator / (currentNorm * candidateNorm);
        return Clamp01(a);
    }

    private float GetTagSimilarity(string tagIdA, string tagIdB)
    {
        if (tagIdA == tagIdB) return 1.0f;

        float matrixValue = _tagSystem.GetTagSimilarity(tagIdA, tagIdB);
        if (matrixValue > 0f) return Clamp01(matrixValue);

        // Fallback to parent/category rules when matrix has no entry
        string parentA = _tagSystem.GetParentTag(tagIdA);
        string parentB = _tagSystem.GetParentTag(tagIdB);
        if (parentA != null && parentA == parentB) return 0.6f;

        string catA = _tagSystem.GetTagCategory(tagIdA);
        string catB = _tagSystem.GetTagCategory(tagIdB);
        if (catA != null && catA == catB) return 0.4f;

        return 0f;
    }

    // =========================================================================
    // Factor B: Explicit Association Weight
    // =========================================================================

    /// <summary>
    /// Computes the explicit association weight from current to candidate.
    /// B = base weight + bidirectional bonus, clamped [0.0, 1.0].
    /// B = -1.0 = designer exclusion.
    /// Returns 0 if current fragment is null or has no explicit associations.
    /// </summary>
    public float ComputeFactorB(MemoryFragment currentFragment, MemoryFragment candidate)
    {
        if (currentFragment == null || candidate == null) return 0f;
        if (currentFragment.ExplicitAssociations == null) return 0f;

        float baseWeight = 0f;
        bool hasExplicit = false;

        foreach (var assoc in currentFragment.ExplicitAssociations)
        {
            if (assoc.TargetFragmentId != candidate.FragmentId) continue;

            // Check visibility condition
            if (assoc.VisibilityCondition != null &&
                !assoc.VisibilityCondition.Evaluate(_changeTracker))
                continue;

            if (assoc.BaseWeight <= B_EXCLUSION + 0.001f)
                return B_EXCLUSION;

            baseWeight = Clamp01(assoc.BaseWeight);
            hasExplicit = true;
            break;
        }

        if (!hasExplicit) return 0f;

        // Bidirectional bonus
        if (candidate.ExplicitAssociations != null)
        {
            foreach (var assoc in candidate.ExplicitAssociations)
            {
                if (assoc.TargetFragmentId == currentFragment.FragmentId && assoc.IsBidirectional)
                {
                    baseWeight = Math.Min(baseWeight + BIDIRECTIONAL_BONUS, 1.0f);
                    break;
                }
            }
        }

        return baseWeight;
    }

    // =========================================================================
    // Factor C: Rhythm Penalty
    // =========================================================================

    /// <summary>
    /// Computes the rhythm penalty based on dominant emotional category
    /// repetition in the recent history window (K=4).
    ///
    /// For each history fragment whose dominant category matches the candidate's,
    /// multiplies C by the position-based penalty. Peace category gets a bonus.
    /// Adaptive halving when pool size ≤ 5.
    /// Clamped to [C_MIN, C_MAX].
    /// </summary>
    public float ComputeFactorC(List<string> recentHistory, MemoryFragment candidate, int poolSize)
    {
        if (candidate == null) return 1.0f;

        string candidateCategory = _tagSystem.GetDominantCategory(candidate.EmotionalTags);
        if (string.IsNullOrEmpty(candidateCategory)) return 1.0f;

        float c = 1.0f;
        bool useAdaptive = poolSize <= ADAPTIVE_POOL_THRESHOLD;

        for (int i = 0; i < recentHistory.Count && i < RHYTHM_WINDOW_K; i++)
        {
            string historyId = recentHistory[i];
            if (string.IsNullOrEmpty(historyId)) continue;

            string historyCategory = _tagSystem.GetFragmentDominantCategory(historyId);
            if (historyCategory == candidateCategory)
            {
                float penalty = PENALTY_BY_POSITION[i];
                if (useAdaptive)
                    penalty = 1.0f - (1.0f - penalty) * ADAPTIVE_HALVING;

                c *= penalty;
            }
        }

        // Peace category bonus
        if (candidateCategory == "Peace")
            c *= PEACE_BONUS;

        return Clamp(c, C_MIN, C_MAX);
    }

    // =========================================================================
    // Factor D: Discovery Boost
    // =========================================================================

    /// <summary>
    /// Computes the discovery boost.
    /// Unvisited → 1.30, visited → 0.70, pending changes → floor at 0.70.
    /// </summary>
    public float ComputeFactorD(string candidateId, HashSet<string> visitedFragmentIds)
    {
        if (visitedFragmentIds == null || !visitedFragmentIds.Contains(candidateId))
            return D_UNVISITED;

        // Visited: apply decay (1 visit = 0.70)
        float d = Math.Max(D_MIN, 1.0f - D_VISIT_DECAY);

        // Pending changes floor: check if fragment has been modified by player choices
        // If the player has already made choices on this fragment, it has replay value
        if (_changeTracker.HasVisited(candidateId))
            d = Math.Max(d, D_PENDING_CHANGES_FLOOR);

        return d;
    }

    // =========================================================================
    // Grading & Dominant Factor
    // =========================================================================

    /// <summary>
    /// Determines the visual grading tier for a composite score.
    /// Strong ≥0.60, Medium ≥0.30, Faint ≥0.10, Trace otherwise.
    /// </summary>
    public static Strength DetermineGrade(float compositeScore)
    {
        if (compositeScore >= 0.60f) return Strength.Strong;
        if (compositeScore >= 0.30f) return Strength.Medium;
        if (compositeScore >= 0.10f) return Strength.Faint;
        return Strength.Trace;
    }

    /// <summary>
    /// Determines which factor contributed most to the composite score.
    /// </summary>
    public static DominantFactor DetermineDominantFactor(
        float factorA, float factorB, float factorC, float factorD,
        float tagScore, float explicitScore)
    {
        float tagContrib = tagScore;
        float explicitContrib = explicitScore;
        float baseScore = tagScore + explicitScore;
        float rhythmContrib = Math.Abs(baseScore * (factorC - 1.0f));
        float discoveryContrib = Math.Abs(baseScore * factorC * (factorD - 1.0f));

        float maxContrib = tagContrib;
        DominantFactor dominant = DominantFactor.TagSimilarity;

        if (explicitContrib > maxContrib)
        {
            maxContrib = explicitContrib;
            dominant = DominantFactor.ExplicitAssociation;
        }
        if (rhythmContrib > maxContrib)
        {
            maxContrib = rhythmContrib;
            dominant = DominantFactor.RhythmBoost;
        }
        if (discoveryContrib > maxContrib)
        {
            dominant = DominantFactor.DiscoveryBoost;
        }

        return dominant;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>Finds a fragment in a pool by ID. Returns null if not found.</summary>
    private static MemoryFragment FindFragmentInPool(string fragmentId, List<MemoryFragment> pool)
    {
        foreach (var f in pool)
        {
            if (f.FragmentId == fragmentId) return f;
        }
        return null;
    }

    /// <summary>
    /// Looks up a fragment using IDataManager when it's not in the candidate pool
    /// (e.g., the current fragment which is excluded from its own pool).
    /// </summary>
    private MemoryFragment LookupFragment(string fragmentId, string chapterKey)
    {
        if (string.IsNullOrEmpty(fragmentId) || string.IsNullOrEmpty(chapterKey))
            return null;

        var allFragments = _dataManager.GetFragmentsByChapter(chapterKey);
        if (allFragments == null) return null;

        foreach (var f in allFragments)
        {
            if (f != null && f.FragmentId == fragmentId) return f;
        }
        return null;
    }

    /// <summary>Clamps a float to [0.0, 1.0], handling NaN and Infinity.</summary>
    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsNegativeInfinity(value)) return 0f;
        if (float.IsPositiveInfinity(value)) return 1.0f;
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }

    /// <summary>Clamps a float to [min, max].</summary>
    private static float Clamp(float value, float min, float max)
    {
        if (float.IsNaN(value)) return min;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>Safe square root — returns 0 for negative inputs.</summary>
    private static float Sqrt(float value)
    {
        if (value <= 0f) return 0f;
        return (float)Math.Sqrt(value);
    }
}
