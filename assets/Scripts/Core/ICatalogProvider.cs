/// <summary>
/// Provides access to the EmotionalTagCatalog at runtime (DI abstraction).
///
/// In production, loads the catalog ScriptableObject via Addressables
/// and converts it to EmotionalTagCatalogData. In tests, returns
/// directly constructed catalog data.
///
/// The catalog is loaded once at boot and cached — this interface
/// represents the cached result, not the loading process itself.
/// The actual Addressables.LoadAssetAsync call happens in the
/// production adapter (out of scope for this story).
/// </summary>
public interface ICatalogProvider
{
    /// <summary>
    /// The loaded catalog data, or an error-state catalog if loading failed.
    /// Never null — check IsLoaded to determine success.
    /// </summary>
    EmotionalTagCatalogData Catalog { get; }
}
