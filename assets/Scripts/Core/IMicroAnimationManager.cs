using System.Threading.Tasks;

/// <summary>
/// Interface for the micro-animation system (#9).
/// Injected into <see cref="InteractionManager"/> for testability.
///
/// The concrete implementation uses Shader Graph + MaterialPropertyBlock
/// for GPU-side animations (ADR-0012).
/// </summary>
public interface IMicroAnimationManager
{
    /// <summary>
    /// Plays a one-shot triggered animation by ID.
    /// Returns when the animation's optional CPU-side MicroTween completes.
    /// Non-blocking for GPU-only animations.
    /// </summary>
    /// <param name="animationId">The animation identifier (e.g., "ripple", "examine_zoom_in").</param>
    Task PlayTriggered(string animationId);
}
