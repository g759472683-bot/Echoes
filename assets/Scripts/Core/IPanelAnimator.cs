using System;

/// <summary>
/// Abstracts panel transition animations (fade-in/fade-out) for DI.
///
/// In production, implements CSS class-based opacity transitions using
/// Unity UI Toolkit USS classes (fade-in/fade-in--active/fade-out/fade-out--active).
/// In tests, a lightweight mock that completes immediately.
///
/// Transition durations are sourced from Theme.uss CSS variables:
///   --transition-normal (fade-in, default 300ms)
///   --transition-fast (fade-out, default 200ms)
/// </summary>
public interface IPanelAnimator
{
    /// <summary>Duration of fade-in transition in milliseconds.</summary>
    int FadeInDurationMs { get; }

    /// <summary>Duration of fade-out transition in milliseconds.</summary>
    int FadeOutDurationMs { get; }

    /// <summary>
    /// Play the fade-in animation on a panel instance.
    /// The panel should start with opacity:0 (fade-in class applied before this call).
    /// Calls onComplete when the transition finishes.
    /// </summary>
    void PlayFadeIn(IPanelInstance panel, Action onComplete);

    /// <summary>
    /// Play the fade-out animation on a panel instance.
    /// The panel starts fully visible. On complete, the panel should have opacity:0.
    /// Calls onComplete when the transition finishes (before the panel is removed).
    /// </summary>
    void PlayFadeOut(IPanelInstance panel, Action onComplete);
}
