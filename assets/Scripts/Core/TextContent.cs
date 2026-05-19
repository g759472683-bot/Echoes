/// <summary>
/// Text content payload for ShowText interaction results.
/// Carried by <see cref="InteractionResult.TextContent"/> and dispatched
/// via <see cref="InteractionManager.OnShowText"/>.
/// </summary>
public class TextContent
{
    /// <summary>The display text (localized key or raw string).</summary>
    public string Text;

    /// <summary>How long the text stays visible in seconds (default 4.0s per HUD spec).</summary>
    public float Duration;
}
