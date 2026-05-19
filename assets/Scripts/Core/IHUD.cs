using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Interface for the in-game HUD system (#17).
/// Injected into <see cref="InteractionManager"/> for testability.
///
/// The concrete implementation uses UI Toolkit (ADR-0006) to render
/// fragment text overlays and choice panels.
/// </summary>
public interface IHUD
{
    /// <summary>
    /// Shows a fragment text overlay at the specified screen position.
    /// The text auto-fades after its configured duration.
    /// </summary>
    /// <param name="content">The text content and duration.</param>
    /// <param name="screenPosition">Screen-space position (cursor + 20px offset).</param>
    void ShowFragmentText(TextContent content, Vector2 screenPosition);

    /// <summary>
    /// Presents a choice panel and returns after the player makes a selection
    /// or cancels (Escape). The returned choice ID is null if cancelled.
    /// </summary>
    /// <param name="choiceGroup">The choice group to display.</param>
    /// <returns>The selected ChoiceId, or null if cancelled.</returns>
    Task<string> ShowChoicePanel(ChoiceGroup choiceGroup);

    /// <summary>
    /// Presents a choice panel at the specified screen position.
    /// Overload for Story 004 — panel positioned relative to the anchor object.
    /// </summary>
    /// <param name="choiceGroup">The choice group to display.</param>
    /// <param name="screenPosition">Screen-space position for the panel corner.</param>
    /// <returns>The selected ChoiceId, or null if cancelled.</returns>
    Task<string> ShowChoicePanel(ChoiceGroup choiceGroup, Vector2 screenPosition);

    /// <summary>
    /// Hides the choice panel with a fade-out animation.
    /// </summary>
    /// <param name="fadeDuration">Fade duration in seconds.</param>
    Task HideChoicePanel(float fadeDuration);
}
