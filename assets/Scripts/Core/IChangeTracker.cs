/// <summary>
/// Data source interface consumed by Condition.Evaluate() (ADR-0008).
///
/// Provides the runtime state that leaf conditions query: flags, visit records,
/// chapter completion, choice history, object state, and tag weights.
///
/// The concrete implementation lives in ChangeTracker (#12) — this story
/// only defines the interface so the evaluation engine can be tested.
///
/// All methods are pure queries — calling Evaluate() must not modify state.
/// </summary>
public interface IChangeTracker
{
    /// <summary>Returns the current boolean value of a global narrative flag.</summary>
    bool GetFlag(string flagId);

    /// <summary>Returns true if the player has made the specified choice in the specified fragment.</summary>
    bool HasChoiceMade(string fragmentId, string choiceId);

    /// <summary>Returns the current state of an interactive object.</summary>
    ObjectState GetObjectState(string fragmentId, string objectId);

    /// <summary>Returns true if the player has visited the specified fragment at least once.</summary>
    bool HasVisited(string fragmentId);

    /// <summary>Returns true if the specified chapter has been completed.</summary>
    bool IsChapterCompleted(string chapterId);

    /// <summary>Returns the resolved emotional tag weight (after overlay), clamped [0.0, 1.0].</summary>
    float GetTagWeight(string tagId);
}
