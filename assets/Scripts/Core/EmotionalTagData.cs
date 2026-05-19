using System;
using UnityEngine;

/// <summary>
/// A single entry in the EmotionalTagCatalog (ADR-0007).
///
/// Defines the metadata for one emotional tag:
///   - TagId — stable string identifier used across code and data
///   - DisplayName — localized display name (string for MVP; TableReference in production)
///   - Category — one of 8 EmotionCategory values
///   - ParentTagId — optional parent for 2-level hierarchy (null = root tag)
///   - IncompatibleWith — tags that cannot share IsPrimary on the same fragment
///   - AssociatedColors — visual color pair for emotion-driven theming
///   - Description — designer-facing explanation of the tag's emotional meaning
///
/// Runtime read-only — all values set at design time in EmotionalTagCatalog SO.
/// </summary>
[Serializable]
public struct EmotionalTagData
{
    /// <summary>Stable string identifier (e.g., "nostalgia", "hope").</summary>
    public string TagId;

    /// <summary>Localized display name. String for MVP; TableReference in production.</summary>
    public string DisplayName;

    /// <summary>Emotional category this tag belongs to.</summary>
    public EmotionCategory Category;

    /// <summary>Parent tag ID for hierarchy (max 2 levels). Null for root tags.</summary>
    public string ParentTagId;

    /// <summary>Tag IDs that cannot both be IsPrimary on the same fragment.</summary>
    public string[] IncompatibleWith;

    /// <summary>Visual color pair for emotion-driven theming.</summary>
    public ColorAssociation AssociatedColors;

    /// <summary>Designer-facing explanation of this tag's emotional meaning.</summary>
    public string Description;

    public EmotionalTagData(
        string tagId,
        string displayName,
        EmotionCategory category,
        string parentTagId = null,
        string[] incompatibleWith = null,
        ColorAssociation associatedColors = default,
        string description = null)
    {
        TagId = tagId;
        DisplayName = displayName;
        Category = category;
        ParentTagId = parentTagId;
        IncompatibleWith = incompatibleWith ?? new string[0];
        AssociatedColors = associatedColors;
        Description = description;
    }
}
