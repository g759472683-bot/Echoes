using System;

/// <summary>
/// Severity level for validation errors found by FragmentValidator.
/// </summary>
public enum ValidationErrorLevel
{
    /// <summary>Must be fixed — blocking game logic error.</summary>
    Error,

    /// <summary>Advisory — exceeds recommended limits but game will run.</summary>
    Warning
}

/// <summary>
/// A single validation finding from FragmentValidator.ValidateAll().
/// Carries severity, human-readable message, and the source fragment ID
/// so Editor tooling can navigate to the offending SO.
/// </summary>
[Serializable]
public struct ValidationError
{
    /// <summary>Error severity level.</summary>
    public ValidationErrorLevel Level;

    /// <summary>FragmentId of the SO that triggered this error (may be null).</summary>
    public string SourceFragmentId;

    /// <summary>Human-readable error message.</summary>
    public string Message;

    public ValidationError(ValidationErrorLevel level, string message, string sourceFragmentId = null)
    {
        Level = level;
        Message = message;
        SourceFragmentId = sourceFragmentId;
    }

    public override string ToString() => $"[{Level}] {SourceFragmentId ?? "(global)"}: {Message}";
}
