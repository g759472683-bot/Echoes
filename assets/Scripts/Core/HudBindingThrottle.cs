using System;
using UnityEngine.UIElements;

/// <summary>
/// Dirty-flag throttle for HUD MVVM data binding refresh.
///
/// Multiple rapid property changes within a 100ms window are batched into a single
/// <see cref="OnRefresh"/> invocation, enforcing a maximum refresh rate of 10 Hz.
/// This prevents excessive UI Toolkit layout recalculations when data sources
/// (AssociationPathsDataSource, ChapterProgressDataSource) change rapidly.
///
/// Usage: Call <see cref="MarkDirty"/> from data source propertyChanged handlers.
/// The scheduled callback invokes <see cref="OnRefresh"/> once per 100ms window
/// if the dirty flag was set.
/// </summary>
public class HudBindingThrottle
{
    private bool _isDirty;
    private readonly IVisualElementScheduledItem _scheduledUpdate;

    /// <summary>
    /// Fires at most once per 100ms when the dirty flag was raised.
    /// Subscribers should call their UI refresh logic in this handler.
    /// </summary>
    public event Action OnRefresh;

    /// <summary>
    /// Creates a throttle bound to the given VisualElement's scheduler.
    /// The scheduled callback polls <see cref="_isDirty"/> every 100ms (10 Hz max).
    /// </summary>
    /// <param name="root">The HUD root VisualElement whose scheduler to use.</param>
    public HudBindingThrottle(VisualElement root)
    {
        _scheduledUpdate = root.schedule.Execute(() =>
        {
            if (_isDirty)
            {
                _isDirty = false;
                RefreshAllBindings();
            }
        });
        _scheduledUpdate.Every(100); // 100ms = 10Hz max refresh rate
    }

    /// <summary>
    /// Marks the binding state as dirty. Multiple calls within the same 100ms
    /// window result in only one <see cref="OnRefresh"/> invocation.
    /// </summary>
    public void MarkDirty()
    {
        _isDirty = true;
    }

    /// <summary>
    /// Stops the scheduled update. Call when the HUD is being destroyed
    /// to prevent callbacks on disposed VisualElements.
    /// </summary>
    public void Stop()
    {
        _scheduledUpdate?.Pause();
    }

    private void RefreshAllBindings()
    {
        OnRefresh?.Invoke();
    }
}
