using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Full-screen ink-fade overlay implementing ISceneFader (ADR-0004).
///
/// A UI Toolkit VisualElement that covers the entire screen with a pure black
/// mask, animated via GPU-accelerated USS opacity transitions. Used by
/// GameSceneManager for all fragment/chapter/scene transitions.
///
/// Usage:
///   var fader = new SceneFader();
///   fader.AddToDocument(uiDocument);
///   await fader.FadeOut(0.5f); // ink spreads across screen
///   // ... load assets ...
///   await fader.FadeIn(0.5f);  // ink recedes
///
/// Picking-mode is set to Ignore so mouse events pass through the overlay.
/// Input gating during transitions is handled separately by InputManager
/// (see GameSceneManager state machine).
/// </summary>
public class SceneFader : VisualElement, ISceneFader
{
    /// <summary>
    /// Creates a full-screen ink-fade overlay. Initial state: transparent.
    /// Call AddToDocument(UIDocument) to mount onto a UIDocument's root.
    /// </summary>
    public SceneFader()
    {
        // Apply USS class for static styles (background-color, transition-property, etc.)
        AddToClassList("scene-fader");

        // Position: cover entire screen
        style.position = Position.Absolute;
        style.top = 0;
        style.left = 0;
        style.width = Length.Percent(100);
        style.height = Length.Percent(100);

        // Don't block mouse events — input gating is managed by InputManager
        pickingMode = PickingMode.Ignore;
    }

    /// <summary>
    /// Mounts this SceneFader as a child of the given UIDocument's root VisualElement,
    /// bringing it to the front of the rendering order.
    /// Safe to call after the UIDocument has finished loading (rootVisualElement is non-null).
    /// </summary>
    /// <param name="uiDocument">The UIDocument whose root will host this fader.</param>
    public void AddToDocument(UIDocument uiDocument)
    {
        if (uiDocument == null)
        {
            Debug.LogWarning("[SceneFader] AddToDocument called with null UIDocument.");
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[SceneFader] UIDocument.rootVisualElement is null — " +
                "ensure the UXML has finished loading before mounting.");
            return;
        }

        root.Add(this);
        BringToFront();
    }

    /// <summary>
    /// Animates the ink spreading across the screen (opacity 0 → 1).
    /// Uses USS GPU-accelerated transition — no per-frame C# coroutine.
    /// </summary>
    /// <param name="duration">Animation duration in seconds (ADR-0004: 0.5f for fragments, 1.0f for chapters/scenes).</param>
    /// <returns>Task that completes when the fade-out animation finishes.</returns>
    public async Task FadeOut(float duration)
    {
        SetTransitionDuration(duration);
        style.opacity = 1;
        await Task.Delay((int)(duration * 1000));
    }

    /// <summary>
    /// Animates the ink receding from the screen (opacity 1 → 0).
    /// Uses USS GPU-accelerated transition — no per-frame C# coroutine.
    /// </summary>
    /// <param name="duration">Animation duration in seconds (ADR-0004: 0.5f for fragments, 1.0f for chapters/scenes).</param>
    /// <returns>Task that completes when the fade-in animation finishes.</returns>
    public async Task FadeIn(float duration)
    {
        SetTransitionDuration(duration);
        style.opacity = 0;
        await Task.Delay((int)(duration * 1000));
    }

    /// <summary>
    /// Sets the inline transition-duration style value, overriding the USS default.
    /// This allows different transition types (Fragment vs Chapter vs Scene) to use
    /// different durations while sharing the same USS class.
    /// </summary>
    /// <param name="duration">Transition duration in seconds.</param>
    private void SetTransitionDuration(float duration)
    {
        style.transitionDuration = new List<TimeValue>
        {
            new TimeValue(duration, TimeUnit.Second)
        };
    }
}
