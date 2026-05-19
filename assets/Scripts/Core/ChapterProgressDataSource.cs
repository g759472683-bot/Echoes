using System;
using UnityEngine.UIElements;

/// <summary>
/// MVVM data source for the chapter progress HUD element (#chapter-progress).
/// Implements <see cref="INotifyBindablePropertyChanged"/> so UI Toolkit bindings
/// automatically update when progress values change.
///
/// Consumed by InGameHUD to push chapter progression into the UI layer
/// with throttled refresh (see <see cref="HudBindingThrottle"/>).
/// </summary>
public class ChapterProgressDataSource : INotifyBindablePropertyChanged
{
    /// <inheritdoc/>
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    private int _visitedCount;
    private int _totalCount;
    private string _chapterName;

    /// <summary>
    /// Number of fragments visited in the current chapter.
    /// Setting this property fires <see cref="propertyChanged"/>.
    /// </summary>
    [CreateProperty]
    public int VisitedCount
    {
        get => _visitedCount;
        set
        {
            if (_visitedCount == value) return;
            _visitedCount = value;
            NotifyPropertyChanged(nameof(VisitedCount));
        }
    }

    /// <summary>
    /// Total number of fragments in the current chapter.
    /// Setting this property fires <see cref="propertyChanged"/>.
    /// </summary>
    [CreateProperty]
    public int TotalCount
    {
        get => _totalCount;
        set
        {
            if (_totalCount == value) return;
            _totalCount = value;
            NotifyPropertyChanged(nameof(TotalCount));
        }
    }

    /// <summary>
    /// Localized display name of the current chapter.
    /// Setting this property fires <see cref="propertyChanged"/>.
    /// </summary>
    [CreateProperty]
    public string ChapterName
    {
        get => _chapterName;
        set
        {
            if (_chapterName == value) return;
            _chapterName = value;
            NotifyPropertyChanged(nameof(ChapterName));
        }
    }

    public ChapterProgressDataSource()
    {
        _visitedCount = 0;
        _totalCount = 0;
        _chapterName = string.Empty;
    }

    private void NotifyPropertyChanged(string propertyName)
    {
        propertyChanged?.Invoke(this,
            new BindablePropertyChangedEventArgs(propertyName));
    }
}
