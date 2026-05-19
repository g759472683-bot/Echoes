/// <summary>
/// Navigation direction for keyboard/gamepad focus movement.
/// Mirrors Unity's FocusNavigationDirection for pure C# testability.
/// </summary>
public enum NavigationDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// Abstracts Unity's FocusController for pure C# testing of FocusNavigationCore.
///
/// In production, delegates to VisualElement.focusController methods
/// (FocusNextInDirection, FocusNext, FocusPrevious, Focus) and UQuery
/// for finding focusable elements. In tests, a lightweight mock.
/// </summary>
public interface IFocusProvider
{
    /// <summary>
    /// Auto-focus the first focusable element in the panel.
    /// Returns the element ID that received focus, or null if the panel has no focusable elements.
    /// </summary>
    string FocusFirst(string panelId);

    /// <summary>
    /// Capture the currently focused element ID in the panel.
    /// Returns the element ID, or null if nothing is focused.
    /// </summary>
    string CaptureCurrentFocus(string panelId);

    /// <summary>
    /// Focus a specific element by ID. No-op if the element ID is not found.
    /// </summary>
    void FocusElement(string panelId, string elementId);

    /// <summary>
    /// Navigate focus in a direction using Unity's FocusNextInDirection.
    /// Returns the newly focused element ID, or null if focus didn't change.
    /// </summary>
    string NavigateDirection(string panelId, NavigationDirection direction);

    /// <summary>
    /// Move focus to the next focusable group (Tab forward).
    /// Returns the newly focused element ID, or null if focus didn't change.
    /// </summary>
    string FocusNext(string panelId);

    /// <summary>
    /// Move focus to the previous focusable group (Shift+Tab).
    /// Returns the newly focused element ID, or null if focus didn't change.
    /// </summary>
    string FocusPrevious(string panelId);

    /// <summary>
    /// Activate the currently focused element. For buttons, triggers click.
    /// For other elements (Toggle, Slider), triggers submit.
    /// Returns true if an action was taken.
    /// </summary>
    bool ActivateFocused(string panelId);
}
