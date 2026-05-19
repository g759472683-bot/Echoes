/// <summary>
/// Serializable data class for a single association path candidate rendered in the HUD.
/// Wraps an <see cref="AssociationCandidate"/> result into a bindable data model
/// consumed by <see cref="AssociationPathsDataSource"/> for MVVM binding.
/// </summary>
public class PathCandidateData
{
    /// <summary>The target fragment ID to transition to on click.</summary>
    public string TargetFragmentId;

    /// <summary>Composite association score from the engine.</summary>
    public float Score;

    /// <summary>Visual grading tier (Strong/Medium/Faint/Trace).</summary>
    public Strength Grade;

    /// <summary>The dominant factor that contributed most to this score.</summary>
    public DominantFactor DominantFactor;

    public PathCandidateData()
    {
    }

    /// <summary>Creates a PathCandidateData from an AssociationCandidate result.</summary>
    public static PathCandidateData FromCandidate(AssociationCandidate candidate)
    {
        return new PathCandidateData
        {
            TargetFragmentId = candidate.FragmentId,
            Score = candidate.CompositeScore,
            Grade = candidate.Grade,
            DominantFactor = candidate.DominantFactor
        };
    }
}
