using System.Threading.Tasks;

/// <summary>
/// Interface for the full-screen fade effect that covers asset loading during transitions.
/// Implemented as a UI Toolkit VisualElement with USS transitions (ADR-0004).
/// The ink-spread/recede animation visually masks the fragment swap.
/// </summary>
public interface ISceneFader
{
    /// <summary>
    /// Animates the fade-out (ink spreading across the screen).
    /// Called at the start of a fragment transition, before unloading the current fragment.
    /// </summary>
    /// <param name="duration">Animation duration in seconds (ADR-0004 specifies 0.5f).</param>
    /// <returns>Task that completes when the fade-out animation finishes.</returns>
    Task FadeOut(float duration);

    /// <summary>
    /// Animates the fade-in (ink receding from the screen).
    /// Called after the new fragment's assets are loaded and applied.
    /// </summary>
    /// <param name="duration">Animation duration in seconds (ADR-0004 specifies 0.5f).</param>
    /// <returns>Task that completes when the fade-in animation finishes.</returns>
    Task FadeIn(float duration);
}
