using UnityEngine;

/// <summary>
/// Abstraction over Unity Input System mouse position polling.
/// Enables pure C# testing of HoverDetectorCore without Unity runtime.
/// </summary>
public interface IMousePositionProvider
{
    /// <summary>Returns the current mouse position in screen coordinates.</summary>
    Vector2 GetMousePosition();
}

/// <summary>
/// Abstraction over Camera.main.ScreenToWorldPoint for coordinate conversion.
/// Enables pure C# testing of HoverDetectorCore without Unity runtime.
/// </summary>
public interface ICameraProvider
{
    /// <summary>Converts screen position to world position.</summary>
    Vector2 ScreenToWorldPoint(Vector2 screenPoint);
}

/// <summary>
/// Abstraction over Physics2D.OverlapPoint for interactable detection.
/// Enables pure C# testing of HoverDetectorCore without Unity runtime.
/// </summary>
public interface IPhysics2DProvider
{
    /// <summary>
    /// Performs a non-alloc overlap point check at the given world position.
    /// Returns the GameObject names of all colliders hit (sorted by depth).
    /// Only Interactable layer colliders are detected.
    /// </summary>
    string[] OverlapPoint(Vector2 worldPoint);
}
