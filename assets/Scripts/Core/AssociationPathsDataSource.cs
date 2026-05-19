using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// MVVM data source for the association paths HUD element (#association-paths).
/// Implements <see cref="INotifyBindablePropertyChanged"/> so UI Toolkit bindings
/// automatically update when <see cref="Candidates"/> changes.
///
/// Consumed by InGameHUD to push association engine results into the UI layer
/// with throttled refresh (see <see cref="HudBindingThrottle"/>).
/// </summary>
public class AssociationPathsDataSource : INotifyBindablePropertyChanged
{
    /// <inheritdoc/>
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    private List<PathCandidateData> _candidates;

    /// <summary>
    /// The top-5 association candidates to render as ink-trail paths.
    /// Setting this property fires <see cref="propertyChanged"/> so bound
    /// VisualElements refresh automatically.
    /// </summary>
    [CreateProperty]
    public List<PathCandidateData> Candidates
    {
        get => _candidates;
        set
        {
            _candidates = value;
            propertyChanged?.Invoke(this,
                new BindablePropertyChangedEventArgs(nameof(Candidates)));
        }
    }

    public AssociationPathsDataSource()
    {
        _candidates = new List<PathCandidateData>();
    }
}
