/// <summary>
/// Represents the current phase of a fragment transition.
/// Used by SceneManager to guard against concurrent transitions (ADR-0004).
/// </summary>
public enum TransitionState
{
    /// <summary>No transition in progress. Ready to accept new transition requests.</summary>
    Idle,
    /// <summary>SceneFader is animating the fade-out (ink spreading). Interaction is gated.</summary>
    FadingOut,
    /// <summary>Previous fragment unloaded, new fragment assets are loading.</summary>
    Loading,
    /// <summary>SceneFader is animating the fade-in (ink receding). Interaction still gated.</summary>
    FadingIn
}
