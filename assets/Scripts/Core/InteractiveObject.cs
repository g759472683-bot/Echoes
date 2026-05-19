using System;
using UnityEngine;

/// <summary>
/// Data class representing an interactive hotspot on a memory fragment scroll
/// (ADR-0007 Category 3).
///
/// Read from MemoryFragment SO (immutable base) at fragment transition time.
/// Each InteractiveObject defines a collider region on the 2D scroll that responds
/// to mouse hover/click/drag via InteractionManager.Physics2D.OverlapPoint (ADR-0005).
///
/// Fields marked "wet ink" (DefaultState) can be overridden by player choices
/// via SetObjectState ContentChange applied through ChangeTracker overlay.
/// </summary>
[Serializable]
public class InteractiveObject
{
    /// <summary>Unique identifier for this interactive object within its fragment.</summary>
    public string ObjectId;

    /// <summary>
    /// Interaction behaviour type (Touch / Drag / Hover / Examine).
    /// Also accessible via InteractionType property (GDD naming convention).
    /// </summary>
    public InteractionType Type;

    /// <summary>
    /// GDD field name — returns the same value as <see cref="Type"/>.
    /// </summary>
    public InteractionType InteractionType => Type;

    /// <summary>World-space center of the interactable hitbox (BoxCollider2D).</summary>
    public Vector2 HitboxCenter;

    /// <summary>Size of the BoxCollider2D trigger area.</summary>
    public Vector2 HitboxSize;

    /// <summary>
    /// Initial state — wet ink.
    /// Hidden objects get no collider. Player choices can change this via SetObjectState.
    /// </summary>
    public ObjectState DefaultState;

    /// <summary>Default sprite key for this object when in Active state (Addressables key).</summary>
    public string DefaultSprite;

    /// <summary>Sprite key shown on hover — visual feedback (glow, shimmer). Optional.</summary>
    public string HoverSprite;

    /// <summary>
    /// Condition that must be met for this object to be interactable.
    /// Serialized via [SerializeReference] for polymorphic ConditionGroup support.
    /// Defaults to ConditionAlways if not set (always interactable).
    /// </summary>
    [SerializeReference]
    public ConditionGroup InteractCondition;

    /// <summary>Sort order for overlapping colliders. Higher values render/layer on top.</summary>
    public int SortOrder;

    /// <summary>What happens when the player interacts with this object (click/hover result).</summary>
    public InteractionResult OnInteract;

    public InteractiveObject()
    {
        DefaultState = ObjectState.Active;
        Type = InteractionType.Touch;
    }
}
