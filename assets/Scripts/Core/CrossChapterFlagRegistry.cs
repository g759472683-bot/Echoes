using System;
using UnityEngine;

/// <summary>
/// ScriptableObject registry defining all cross-chapter narrative flags (ADR-0011).
///
/// Each entry maps a globally unique FlagId to its origin chapter/fragment/choice,
/// immutability rules, default value, and consumer list. The registry is the
/// authoritative directory — designers use it to see which choices ripple across
/// chapter boundaries.
///
/// Runtime read-only per ADR-0007. Never mutated at runtime.
///
/// Create via: Assets → Create → Echoes → CrossChapterFlagRegistry
/// </summary>
[CreateAssetMenu(menuName = "Echoes/CrossChapterFlagRegistry")]
public class CrossChapterFlagRegistry : ScriptableObject
{
    public CrossChapterFlagDef[] Flags = Array.Empty<CrossChapterFlagDef>();
}

/// <summary>
/// Definition of a single cross-chapter narrative flag (ADR-0011).
/// </summary>
[Serializable]
public struct CrossChapterFlagDef
{
    /// <summary>Globally unique identifier. e.g. "ch1_letter_kept". snake_case.</summary>
    public string FlagId;

    /// <summary>Which chapter sets this flag. e.g. "ch01".</summary>
    public string SetInChapter;

    /// <summary>Which fragment within the chapter sets this flag.</summary>
    public string SetInFragmentId;

    /// <summary>Which choice within the fragment triggers this flag.</summary>
    public string SetByChoiceId;

    /// <summary>If true, once set to true the flag can never be set back to false (not even during replay).</summary>
    public bool IsImmutable;

    /// <summary>Value when a new game starts. Almost always false.</summary>
    public bool DefaultValue;

    /// <summary>EndingIds or ConditionGroup identifiers that consume this flag (documentation).</summary>
    public string[] ConsumedBy;
}
