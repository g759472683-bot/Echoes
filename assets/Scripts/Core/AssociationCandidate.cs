using System;

/// <summary>
/// Result struct returned by WebAssociationEngine.ComputeAssociations (ADR-0009).
///
/// Each candidate represents a fragment that the engine recommends as a "next
/// memory to explore" from the current fragment. The CompositeScore drives
/// ranking; FactorA-D expose the breakdown for debugging and DominantFactor.
///
/// All fields are readonly — consumers cannot modify engine output.
/// </summary>
[Serializable]
public readonly struct AssociationCandidate
{
    /// <summary>The recommended fragment's ID.</summary>
    public readonly string FragmentId;

    /// <summary>Composite association score: (A×0.6 + B×0.4) × C × D.</summary>
    public readonly float CompositeScore;

    /// <summary>Visual grading tier based on CompositeScore thresholds.</summary>
    public readonly Strength Grade;

    /// <summary>The single factor that contributed most to this score.</summary>
    public readonly DominantFactor DominantFactor;

    // Factor breakdown (public for debugging / UI transparency)

    /// <summary>Factor A — cosine tag similarity [0.0, 1.0].</summary>
    public readonly float FactorA;

    /// <summary>Factor B — explicit association weight [0.0, 1.0].</summary>
    public readonly float FactorB;

    /// <summary>Factor C — rhythm penalty [0.1, 1.3].</summary>
    public readonly float FactorC;

    /// <summary>Factor D — discovery boost [0.3, 1.3].</summary>
    public readonly float FactorD;

    public AssociationCandidate(
        string fragmentId,
        float compositeScore,
        Strength grade,
        DominantFactor dominantFactor,
        float factorA,
        float factorB,
        float factorC,
        float factorD)
    {
        FragmentId = fragmentId;
        CompositeScore = compositeScore;
        Grade = grade;
        DominantFactor = dominantFactor;
        FactorA = factorA;
        FactorB = factorB;
        FactorC = factorC;
        FactorD = factorD;
    }
}

/// <summary>
/// Visual grading tier for association strength (ADR-0009 §Visual Grading).
/// Maps to UI ink opacity / color warmth in HUD (#17).
/// </summary>
public enum Strength
{
    /// <summary>compositeScore ≥ 0.60 — warm color, thick ink stroke.</summary>
    Strong,

    /// <summary>compositeScore ≥ 0.30 — warm color, thin ink stroke.</summary>
    Medium,

    /// <summary>compositeScore ≥ 0.10 — cool color, faint ink.</summary>
    Faint,

    /// <summary>compositeScore &lt; 0.10 — cool color, barely visible.</summary>
    Trace
}

/// <summary>
/// Identifies which factor contributed most to a candidate's composite score (ADR-0009 §DominantFactor).
/// Used by HUD (#17) to optionally display association type.
/// </summary>
public enum DominantFactor
{
    /// <summary>Factor A (cosine tag similarity) was the largest contributor.</summary>
    TagSimilarity,

    /// <summary>Factor B (explicit association weight) was the largest contributor.</summary>
    ExplicitAssociation,

    /// <summary>Factor C (rhythm penalty deviation from 1.0) was the largest contributor.</summary>
    RhythmBoost,

    /// <summary>Factor D (discovery boost deviation from 1.0) was the largest contributor.</summary>
    DiscoveryBoost
}
