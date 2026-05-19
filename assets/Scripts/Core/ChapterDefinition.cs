using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// ScriptableObject that defines a single chapter and the fragments it contains.
///
/// Each ChapterDefinition is an addressable asset. It holds the chapter metadata
/// plus an array of AssetReferenceT&lt;MemoryFragment&gt; — one per fragment in the
/// chapter. The DataManager resolves these references at runtime via Addressables
/// (ADR-0002).
///
/// Designer-authored — created in the Unity Editor via
/// Assets &gt; Create &gt; 回响 &gt; Chapter Definition.
/// </summary>
[CreateAssetMenu(fileName = "ChapterDefinition", menuName = "回响/Chapter Definition")]
[UnityEngine.Scripting.Preserve]
public class ChapterDefinition : ScriptableObject
{
    /// <summary>
    /// Stable chapter key used across all systems (e.g. "ch01", "ch02").
    /// Matches the folder structure under Data_Ch01–04 Addressables groups.
    /// </summary>
    public string ChapterKey;

    /// <summary>
    /// Deterministic ordering index — chapters are played in OrderIndex ascending.
    /// Chapter 1 = 0, Chapter 2 = 1, etc.
    /// </summary>
    public int OrderIndex;

    /// <summary>
    /// The fragment ID that should be displayed when the chapter begins.
    /// Must match a FragmentId within one of the MemoryFragment assets
    /// referenced in <see cref="Fragments"/>.
    /// </summary>
    public string EntryFragmentId;

    /// <summary>
    /// All MemoryFragment assets belonging to this chapter, in display order.
    /// AssetReferenceT&lt;MemoryFragment&gt; ensures only MemoryFragment assets
    /// can be assigned in the Inspector. The order of this array defines the
    /// default fragment sequence within the chapter.
    /// </summary>
    public AssetReferenceT<MemoryFragment>[] Fragments;

    /// <summary>
    /// Ending configurations for this chapter (ADR-0010).
    /// Each chapter has 2-5 ending definitions. The multi-ending system evaluates
    /// all ending definitions when ResolveEnding is called.
    /// Exactly one ending per chapter must have IsDefault=true, MinimumScore=0.0.
    /// </summary>
    public EndingDefinition[] Endings;

    /// <summary>
    /// Ratio [0.0, 1.0] of fragments that must be visited before the
    /// association-based completion condition (B) can trigger.
    /// Default 0.6 — designer-adjustable per chapter.
    /// </summary>
    public float CompletionRatio = 0.6f;

    /// <summary>
    /// If true, this chapter can be replayed after completion.
    /// Default true. Set to false for tutorial-only or one-shot chapters.
    /// </summary>
    public bool AllowReplay = true;
}
