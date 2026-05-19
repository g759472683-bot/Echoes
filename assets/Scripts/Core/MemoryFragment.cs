using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScriptableObject representing a single memory fragment — the atomic unit of the
/// game world (ADR-0007, GDD memory-fragment-data-model §Detailed Design).
///
/// <b>8 Data Categories (ADR-0007):</b>
///   1. Core Identity — FragmentId, ChapterKey, SequenceIndex, FragmentName (dry ink)
///   2. Visual — BaseIllustration key, VisualLayers (wet: DefaultVisible)
///   3. Interactive Objects — Hotspots with hitbox, state, sprites, conditions
///   4. Emotional Tags — TagId, BaseWeight (wet), IsPrimary
///   5. Choice Branches — ChoiceGroups → Choice → ContentChanges
///   6. Content Changes — 6 polymorphic types via [SerializeReference]
///   7. Explicit Associations — Edges in the fragment graph (wet: BaseWeight)
///   8. Ending Triggers — Conditions that contribute to ending resolution
///
/// <b>Immutability Rule (ADR-0007):</b>
/// This SO is NEVER modified at runtime. Player choices produce ContentChanges
/// that are applied via ChangeTracker overlay — the SO remains pristine.
/// Fields use public setters for Unity serialization and test construction.
/// The "immutable at runtime" rule is enforced by convention and code review,
/// not by the compiler — production code routes all changes through ChangeTracker.
///
/// <b>Loading:</b>
/// Loaded via Addressables by IDataManager.GetFragmentAsync(chapterKey, fragmentId).
/// The CreateAssetMenu attribute enables designer creation in the Unity Editor.
/// </summary>
[CreateAssetMenu(fileName = "MemoryFragment", menuName = "回响/Memory Fragment")]
[UnityEngine.Scripting.Preserve]
public class MemoryFragment : ScriptableObject, IFragmentValidationTarget
{
    // =========================================================================
    // Category 1: Core Identity (All Dry Ink — never change at runtime)
    // =========================================================================

    /// <summary>
    /// Unique fragment identifier within its chapter.
    /// Format: "frag_01", "frag_02", etc.
    /// Combined with ChapterKey to form the full Addressables key: "ch01_frag_03".
    /// </summary>
    [field: SerializeField]
    public string FragmentId { get; set; }

    /// <summary>
    /// Chapter key this fragment belongs to (e.g., "ch01").
    /// Must match a ChapterDefinition.ChapterKey.
    /// </summary>
    [field: SerializeField]
    public string ChapterKey { get; set; }

    /// <summary>
    /// Alias for ChapterKey — GDD field name.
    /// Returns the same value as ChapterKey.
    /// </summary>
    public string ChapterId => ChapterKey;

    /// <summary>
    /// Canonical sort order within the chapter (0-based).
    /// Designer-adjustable — determines default navigation sequence.
    /// </summary>
    [field: SerializeField]
    public int SequenceIndex { get; set; }

    /// <summary>
    /// Localized display name for this fragment (editor/debug use only).
    /// Uses string until com.unity.localization package is installed,
    /// then migrate to TableReference. LocalizationManager (#4) is ready.
    /// </summary>
    [field: SerializeField]
    public string FragmentName { get; set; }

    /// <summary>
    /// Optional condition that must be met for this fragment to appear in the
    /// web association candidate pool (ADR-0009 §Candidate Pool).
    /// Null or empty means the fragment is always accessible.
    /// Evaluated via ConditionGroup.Evaluate(IChangeTracker).
    /// </summary>
    [field: SerializeField]
    public ConditionGroup UnlockCondition { get; set; }

    // =========================================================================
    // Category 2: Visual Fields
    // =========================================================================

    /// <summary>
    /// Key used by IDataManager to load the base background illustration sprite.
    /// This is dry ink — the core illustration never changes.
    /// </summary>
    [field: SerializeField]
    public string IllustrationKey { get; set; }

    /// <summary>
    /// GDD field alias — returns the same value as IllustrationKey.
    /// </summary>
    public string BaseIllustration => IllustrationKey;

    /// <summary>
    /// Visual layers overlaid on the base illustration.
    /// Each layer's DefaultVisible is wet ink — player choices can toggle via
    /// ToggleVisualLayer ContentChange. Layers with IsMutable=false are permanent.
    /// </summary>
    [field: SerializeField]
    public List<VisualLayer> VisualLayers { get; set; }

    // =========================================================================
    // Category 3: Interactive Objects
    // =========================================================================

    /// <summary>
    /// Audio clip keys to preload before the fragment is displayed (legacy field).
    /// Preloaded by GameSceneManager during fragment transitions.
    /// </summary>
    [field: SerializeField]
    public string[] AudioKeys { get; set; }

    /// <summary>
    /// Interactive objects placed on this fragment (hotspots).
    /// Empty array is valid — represents a "viewing only" fragment with no interaction.
    /// </summary>
    [field: SerializeField]
    public InteractiveObject[] InteractiveObjects { get; set; }

    // =========================================================================
    // Category 4: Emotional Tags
    // =========================================================================

    /// <summary>
    /// Emotional tags carried by this fragment.
    /// TagId references the vocabulary defined by the Emotional Tag System (#10).
    /// BaseWeight is wet ink — player choices can modify via ModifyTagWeight.
    /// IsPrimary marks the dominant emotional tone for rhythm pacing (ADR-0009).
    /// </summary>
    [field: SerializeField]
    public List<EmotionalTag> EmotionalTags { get; set; }

    // =========================================================================
    // Category 5: Choice Branches
    // =========================================================================

    /// <summary>
    /// Choice groups available on this fragment.
    /// Triggered by PresentChoice interaction results. Each group contains
    /// 2-3 Choice options, each with 0+ ContentChanges applied on selection.
    /// </summary>
    [field: SerializeField]
    public ChoiceGroup[] ChoiceGroups { get; set; }

    /// <summary>
    /// Looks up a ChoiceGroup by its GroupId.
    /// Returns null if the group is not found or ChoiceGroups is null.
    /// </summary>
    public ChoiceGroup GetChoiceGroup(string groupId)
    {
        if (ChoiceGroups == null) return null;
        return ChoiceGroups.FirstOrDefault(g => g.GroupId == groupId);
    }

    // =========================================================================
    // Category 7: Explicit Associations
    // =========================================================================

    /// <summary>
    /// Explicitly defined associations from this fragment to others.
    /// Feeds into the web association engine (ADR-0009) as explicit edges.
    /// BaseWeight is wet ink — modifiable via UnlockAssociation ContentChange.
    /// </summary>
    [field: SerializeField]
    public List<FragmentAssociation> ExplicitAssociations { get; set; }

    // =========================================================================
    // Category 8: Ending Triggers
    // =========================================================================

    /// <summary>
    /// Ending trigger conditions contributed by this fragment.
    /// Collected and evaluated by the multi-ending system (ADR-0010).
    /// Fragments only define conditions — resolution logic is owned by system #14.
    /// </summary>
    [field: SerializeField]
    public List<EndingTrigger> EndingTriggers { get; set; }

    // =========================================================================
    // Construction
    // =========================================================================

    public MemoryFragment()
    {
        VisualLayers = new List<VisualLayer>();
        EmotionalTags = new List<EmotionalTag>();
        ExplicitAssociations = new List<FragmentAssociation>();
        EndingTriggers = new List<EndingTrigger>();
        InteractiveObjects = Array.Empty<InteractiveObject>();
        ChoiceGroups = Array.Empty<ChoiceGroup>();
        AudioKeys = Array.Empty<string>();
    }
}
