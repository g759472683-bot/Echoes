using System;
using UnityEngine;

namespace Echoes
{
    /// <summary>
    /// Easing function identifiers for MicroTween interpolation.
    /// Each maps to a standard easing curve via ApplyEase().
    /// </summary>
    public enum EaseType
    {
        Linear,
        SineIn,
        SineOut,
        SineInOut,
        EaseOutCubic,
        EaseOutElastic,
        EaseOutBounce
    }

    /// <summary>
    /// Lightweight, zero-GC value-type tween.
    /// Designed for CPU-side animation of float parameters (opacity, scale, shader uniforms).
    /// Looping tweens reset Elapsed when it exceeds Duration.
    /// One-shot tweens report IsComplete when Elapsed >= Duration.
    ///
    /// Implements: MicroAnimationSystem Epic, Story 001 (tween evaluation engine).
    /// </summary>
    public struct MicroTween
    {
        public float Duration;
        public float Elapsed;
        public EaseType Ease;
        public float FromValue;
        public float ToValue;
        public bool IsLooping;

        /// <summary>
        /// True when this one-shot tween has completed its full duration.
        /// Always false for looping tweens.
        /// </summary>
        public bool IsComplete => !IsLooping && Elapsed >= Duration;

        /// <summary>
        /// Creates a new MicroTween with the given parameters.
        /// </summary>
        /// <param name="from">Start value for interpolation.</param>
        /// <param name="to">Target value for interpolation.</param>
        /// <param name="duration">Total duration in seconds.</param>
        /// <param name="ease">Easing function to apply.</param>
        /// <param name="isLooping">If true, Elapsed resets to 0 when it exceeds Duration.</param>
        public MicroTween(float from, float to, float duration, EaseType ease, bool isLooping = false)
        {
            FromValue = from;
            ToValue = to;
            Duration = duration;
            Elapsed = 0f;
            Ease = ease;
            IsLooping = isLooping;
        }

        /// <summary>
        /// Evaluates the current interpolation value based on Elapsed / Duration.
        /// Clamped to [FromValue, ToValue] range.
        /// </summary>
        public float Evaluate()
        {
            if (Duration <= 0f) return ToValue;
            float t = Mathf.Clamp01(Elapsed / Duration);
            return Mathf.Lerp(FromValue, ToValue, ApplyEase(t, Ease));
        }

        /// <summary>
        /// Applies the given easing function to a normalized time t in [0, 1].
        /// All easing functions map [0,1] to approximately [0,1].
        /// </summary>
        public static float ApplyEase(float t, EaseType ease)
        {
            switch (ease)
            {
                case EaseType.Linear: return t;
                case EaseType.SineIn: return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
                case EaseType.SineOut: return Mathf.Sin(t * Mathf.PI * 0.5f);
                case EaseType.SineInOut: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case EaseType.EaseOutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case EaseType.EaseOutElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;
                case EaseType.EaseOutBounce:
                    const float n1 = 7.5625f;
                    const float d1 = 2.75f;
                    if (t < 1f / d1)
                        return n1 * t * t;
                    else if (t < 2f / d1)
                        return n1 * (t -= 1.5f / d1) * t + 0.75f;
                    else if (t < 2.5f / d1)
                        return n1 * (t -= 2.25f / d1) * t + 0.9375f;
                    else
                        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
                default: return t;
            }
        }
    }
}
