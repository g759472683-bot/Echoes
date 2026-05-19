using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global emotional tag vocabulary (ADR-0007 S001).
///
/// ScriptableObject asset created at design time — defines all available
/// emotional tags (MVP: 15-20 tags across 8 EmotionCategory values).
///
/// Runtime read-only. Loaded once at boot via Addressables.LoadAssetAsync
/// and converted to EmotionalTagCatalogData for querying.
///
/// Asset creation: Assets > Create > 回响 > Emotional Tag Catalog
/// </summary>
[CreateAssetMenu(menuName = "回响/Emotional Tag Catalog")]
public class EmotionalTagCatalog : ScriptableObject
{
    /// <summary>All emotional tags defined in the vocabulary.</summary>
    public List<EmotionalTagData> Tags = new();
}
