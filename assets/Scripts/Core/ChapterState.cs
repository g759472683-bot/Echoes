/// <summary>
/// Three-state machine for the ChapterManager lifecycle (GDD chapter-management §3).
///
/// IDLE: No chapter active (main menu, pause menu)
/// IN_CHAPTER: Player is actively navigating within a chapter
/// TRANSITIONING: Scene loading/fade in progress — all navigation requests blocked
/// </summary>
public enum ChapterState
{
    /// <summary>No chapter active. Initial state and return-to-menu state.</summary>
    Idle,

    /// <summary>Player is actively navigating a chapter. Fragment transitions allowed.</summary>
    InChapter,

    /// <summary>Scene transition or loading in progress. All navigation blocked.</summary>
    Transitioning
}
