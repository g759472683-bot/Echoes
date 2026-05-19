/// <summary>
/// Provides panel assets (UXML VisualTreeAsset equivalents) by panel ID.
///
/// In production, wraps a Dictionary&lt;string, VisualTreeAsset&gt; registry.
/// In tests, returns mock objects that can signal whether loading succeeded.
/// Extracted for DI — enables pure C# testing of UIPanelStackCore without Unity.
/// </summary>
public interface IPanelAssetProvider
{
    /// <summary>
    /// Returns true if a panel asset is registered under the given ID.
    /// The asset may still fail to instantiate — this only checks registration.
    /// </summary>
    bool HasAsset(string panelId);

    /// <summary>
    /// Attempts to load (clone) the panel asset for the given ID.
    /// Returns null if the asset is not registered or instantiation fails.
    /// </summary>
    IPanelInstance LoadPanel(string panelId);
}

/// <summary>
/// A panel instance created from a VisualTreeAsset.CloneTree() equivalent.
/// In production, wraps a VisualElement. In tests, a lightweight stub.
/// </summary>
public interface IPanelInstance
{
    string PanelId { get; }
}
