using System.Collections.Generic;

/// <summary>
/// Runtime guard for immutable visual layers (ADR-0007).
///
/// Before applying a ToggleVisualLayer ContentChange, checks whether the
/// target layer has IsMutable=false. If so, logs a warning and rejects the
/// change — the game continues without error.
///
/// Called by ChangeTracker.ApplyChanges() before writing to the overlay.
/// </summary>
public static class ImmutableLayerGuard
{
    /// <summary>
    /// Checks whether a ToggleVisualLayer change is allowed for the given
    /// fragment's visual layers. Returns true if the change can proceed.
    ///
    /// Reasons for rejection (returns false):
    ///   - The layer's IsMutable is false (immutable layer protection)
    ///   - The layer ID does not exist in the fragment's VisualLayers
    /// </summary>
    /// <param name="change">The toggle change being applied.</param>
    /// <param name="visualLayers">The target fragment's visual layer definitions.</param>
    /// <param name="reason">Output: human-readable rejection reason (null if allowed).</param>
    public static bool CanApplyChange(
        ToggleVisualLayer change,
        IReadOnlyList<VisualLayer> visualLayers,
        out string reason)
    {
        reason = null;

        if (change == null || visualLayers == null)
        {
            reason = "Change or visual layers list is null";
            return false;
        }

        // Find the target layer
        VisualLayer? found = null;
        for (int i = 0; i < visualLayers.Count; i++)
        {
            if (visualLayers[i].LayerId == change.LayerId)
            {
                found = visualLayers[i];
                break;
            }
        }

        if (found == null)
        {
            reason = $"图层 '{change.LayerId}' 不存在于碎片 '{change.TargetFragmentId}' 中";
            return false;
        }

        if (!found.Value.IsMutable)
        {
            reason = $"尝试修改不可变图层 '{change.LayerId}'——已跳过";
            return false;
        }

        return true;
    }
}
