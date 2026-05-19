/// <summary>
/// Defines what happens when a player interacts with an InteractiveObject.
/// The <see cref="ResultType"/> determines which fields are meaningful.
///
/// Field usage by ResultType:
///   PlayAnimation       → AnimationId
///   ShowText            → TextContent
///   PresentChoice       → ChoiceGroupId
///   TransitionToFragment→ TargetFragmentId
///   RevealObject        → TargetObjectId
/// </summary>
public class InteractionResult
{
    /// <summary>The type of result — determines the dispatch path.</summary>
    public ResultType ResultType;

    /// <summary>Animation ID for PlayAnimation results (e.g., "ripple", "object_appear").</summary>
    public string AnimationId;

    /// <summary>Text content for ShowText results.</summary>
    public TextContent TextContent;

    /// <summary>Choice group ID for PresentChoice results.</summary>
    public string ChoiceGroupId;

    /// <summary>Target fragment ID for TransitionToFragment results.</summary>
    public string TargetFragmentId;

    /// <summary>Target object ID for RevealObject results.</summary>
    public string TargetObjectId;
}
