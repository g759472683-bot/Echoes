/// <summary>
/// Error severity classification for scene management error recovery (ADR-0004).
/// Determines which recovery options are presented to the player.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>Non-fatal error — player can return to previous state, retry, or go to main menu.</summary>
    Recoverable,
    /// <summary>Fatal error — game cannot continue. Only exit option available.</summary>
    Fatal
}
