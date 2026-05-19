using System;
using UnityEngine;

/// <summary>
/// A visual layer overlaid on top of the base illustration (ADR-0007 Category 2).
///
/// Layers can be toggled on/off by ToggleVisualLayer ContentChanges triggered
/// by player choices. Each layer has a SortOrder for rendering order and an
/// optional PositionOffset relative to the base illustration.
///
/// If IsMutable is false, the overlay system (ChangeTracker) refuses to toggle
/// this layer — it is permanently part of the fragment's visual composition.
/// </summary>
[Serializable]
public struct VisualLayer
{
    /// <summary>Unique layer identifier within the fragment (e.g., "rain_layer", "ghost_figure").</summary>
    [field: SerializeField]
    public string LayerId { get; private set; }

    /// <summary>Sprite reference for this layer (loaded via Addressables).</summary>
    [field: SerializeField]
    public string SpriteReference { get; private set; }

    /// <summary>
    /// Default visibility — wet ink.
    /// Player choices can flip this via ToggleVisualLayer ContentChange.
    /// </summary>
    [field: SerializeField]
    public bool DefaultVisible { get; private set; }

    /// <summary>Rendering sort order (higher = on top).</summary>
    [field: SerializeField]
    public int SortOrder { get; private set; }

    /// <summary>Position offset relative to the base illustration center, in world units.</summary>
    [field: SerializeField]
    public Vector2 PositionOffset { get; private set; }

    /// <summary>
    /// If false, the overlay system refuses to toggle this layer.
    /// Immutable layers are permanently part of the fragment's visual composition.
    /// </summary>
    [field: SerializeField]
    public bool IsMutable { get; private set; }

    public VisualLayer(string layerId, string spriteReference, bool defaultVisible,
        int sortOrder, Vector2 positionOffset, bool isMutable = true)
    {
        LayerId = layerId;
        SpriteReference = spriteReference;
        DefaultVisible = defaultVisible;
        SortOrder = sortOrder;
        PositionOffset = positionOffset;
        IsMutable = isMutable;
    }
}
