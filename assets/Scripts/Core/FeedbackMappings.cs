using System;
using UnityEngine;

/// <summary>
/// ScriptableObject that maps gameplay event names to visual and audio feedback configurations.
/// Used by <see cref="InteractionFeedback"/> to determine which animations and SFX to trigger
/// for each interaction event.
///
/// Implements: InteractionFeedback Epic (#18), Story 001 -- Event subscription + feedback mapping.
/// </summary>
[CreateAssetMenu(menuName = "Echoes/FeedbackMappings")]
public class FeedbackMappings : ScriptableObject
{
    /// <summary>
    /// Ordered array of feedback mappings. Each entry maps one event name to its
    /// visual animation ID, audio SFX key, priority level, and debounce flag.
    /// </summary>
    public FeedbackMapping[] Mappings;
}

/// <summary>
/// A single entry in the feedback mapping table.
/// Maps one gameplay event name to its visual animation ID, audio key, priority, and debounce flag.
/// </summary>
[Serializable]
public class FeedbackMapping
{
    /// <summary>
    /// The event name (e.g., "OnHoverEnter", "OnInteract", "OnChoiceSelected").
    /// </summary>
    public string EventName;

    /// <summary>
    /// MicroAnimationManager animation ID. Empty string = no visual feedback for this event.
    /// </summary>
    public string VisualAnimationId;

    /// <summary>
    /// AudioManager SFX key. Empty string = no audio feedback for this event.
    /// </summary>
    public string AudioKey;

    /// <summary>
    /// Priority level 0-10. Higher values preempt lower-priority active feedback.
    /// When two events have equal priority, the newer event wins.
    /// </summary>
    public int Priority;

    /// <summary>
    /// If true, applies a 300ms debounce window to rapid repeated fires of this event
    /// for the same (objectId, eventName) pair.
    /// </summary>
    public bool IsDebounced;
}
