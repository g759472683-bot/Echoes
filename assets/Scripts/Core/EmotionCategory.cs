/// <summary>
/// Eight emotional categories for the tag vocabulary (ADR-0007).
///
/// Every EmotionalTagData belongs to exactly one category.
/// The web association engine (#13) uses categories for emotional rhythm
/// pacing — preventing consecutive fragments from the same category.
/// </summary>
public enum EmotionCategory
{
    Joy,
    Sadness,
    Love,
    Fear,
    Anger,
    Wonder,
    Melancholy,
    Peace
}
