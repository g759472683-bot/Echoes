using System;
using UnityEngine;

namespace Echoes
{
    /// <summary>
    /// Identifies the low-level technique used to realize a micro-animation.
    /// Maps to the implementation strategy, not the artistic intent.
    /// </summary>
    public enum AnimationImpl
    {
        Shader_VertexDisplace,
        Shader_UVScroll,
        Shader_MaterialPulse,
        Shader_Progress,
        Tween_Loop,
        Tween_OneShot,
        TweenSequence,
        SpriteSwap,
        UIToolkit_Transition
    }

    /// <summary>
    /// Broad classification of an animation's role in the experience.
    /// </summary>
    public enum AnimationCategory
    {
        /// <summary>Continuously running, never triggered by player action.</summary>
        Ambient,
        /// <summary>Launched in response to a specific game event.</summary>
        Triggered,
        /// <summary>Immediate player-action acknowledgment (e.g., hover, click).</summary>
        Feedback
    }

    /// <summary>
    /// Definition for a continuously-running ambient animation.
    /// Drives looping shader effects or perpetual tweens on fragment elements.
    /// </summary>
    [Serializable]
    public class AmbientAnimDef
    {
        public string DefId;
        public AnimationImpl Implementation;
        public float DefaultSpeed = 1f;
        public float DefaultAmplitude = 1f;
        public EaseType DefaultEasing = EaseType.SineInOut;
        public string ShaderPropertyName;
        public Vector2 ShaderPropertyRange = new Vector2(0f, 1f);
    }

    /// <summary>
    /// Definition for a one-shot animation triggered by a game event
    /// (e.g., fragment transition, examine, choice reveal).
    /// </summary>
    [Serializable]
    public class TriggeredAnimDef
    {
        public string DefId;
        public AnimationImpl Implementation;
        public float Duration = 1f;
        public EaseType Easing = EaseType.EaseOutCubic;
    }

    /// <summary>
    /// Definition for immediate player feedback animations
    /// (e.g., hover glow, click ripple, drag highlight).
    /// </summary>
    [Serializable]
    public class FeedbackAnimDef
    {
        public string DefId;
        public AnimationImpl Implementation;
        public float Duration;
        public string ShaderPropertyName;
    }

    /// <summary>
    /// Emotion-driven visual modulation preset.
    /// Modulates ambient animation parameters to reflect the current emotional tone.
    /// </summary>
    [Serializable]
    public class EmotionPreset
    {
        public string EmotionCategory;
        public float SpeedMultiplier = 1f;
        public float AmplitudeMultiplier = 1f;
        public float HueShift;
        public float SaturationModulation;
        public float BrightnessModulation;
        public float GlowIntensity;
    }

    /// <summary>
    /// ScriptableObject catalog holding all micro-animation definitions.
    /// Designers populate this asset with AmbientDefs, TriggeredDefs, FeedbackDefs,
    /// and EmotionPresets. At runtime, MicroAnimationManager queries this catalog
    /// by DefId.
    ///
    /// Implements: MicroAnimationSystem Epic, Story 001 (data types + catalog).
    /// </summary>
    [CreateAssetMenu(menuName = "Echoes/MicroAnimationCatalog")]
    public class MicroAnimationCatalog : ScriptableObject
    {
        public AmbientAnimDef[] AmbientDefs = Array.Empty<AmbientAnimDef>();
        public TriggeredAnimDef[] TriggeredDefs = Array.Empty<TriggeredAnimDef>();
        public FeedbackAnimDef[] FeedbackDefs = Array.Empty<FeedbackAnimDef>();
        public EmotionPreset[] EmotionPresets = Array.Empty<EmotionPreset>();

        /// <summary>
        /// Looks up an AmbientAnimDef by its string identifier.
        /// Returns null if not found or if the array is null.
        /// </summary>
        public AmbientAnimDef GetAmbientDef(string defId)
        {
            if (AmbientDefs == null) return null;
            for (int i = 0; i < AmbientDefs.Length; i++)
            {
                if (AmbientDefs[i] != null && AmbientDefs[i].DefId == defId)
                    return AmbientDefs[i];
            }
            return null;
        }

        /// <summary>
        /// Looks up a TriggeredAnimDef by its string identifier.
        /// Returns null if not found or if the array is null.
        /// </summary>
        public TriggeredAnimDef GetTriggeredDef(string defId)
        {
            if (TriggeredDefs == null) return null;
            for (int i = 0; i < TriggeredDefs.Length; i++)
            {
                if (TriggeredDefs[i] != null && TriggeredDefs[i].DefId == defId)
                    return TriggeredDefs[i];
            }
            return null;
        }

        /// <summary>
        /// Looks up a FeedbackAnimDef by its string identifier.
        /// Returns null if not found or if the array is null.
        /// </summary>
        public FeedbackAnimDef GetFeedbackDef(string defId)
        {
            if (FeedbackDefs == null) return null;
            for (int i = 0; i < FeedbackDefs.Length; i++)
            {
                if (FeedbackDefs[i] != null && FeedbackDefs[i].DefId == defId)
                    return FeedbackDefs[i];
            }
            return null;
        }

        /// <summary>
        /// Looks up an EmotionPreset by category string (case-insensitive).
        /// Returns null if not found or if the array is null.
        /// </summary>
        public EmotionPreset GetEmotionPreset(string category)
        {
            if (EmotionPresets == null) return null;
            for (int i = 0; i < EmotionPresets.Length; i++)
            {
                if (EmotionPresets[i] != null &&
                    string.Equals(EmotionPresets[i].EmotionCategory, category, StringComparison.OrdinalIgnoreCase))
                    return EmotionPresets[i];
            }
            return null;
        }
    }

    /// <summary>
    /// Preset configuration for chapter-level ambient animation parameters.
    /// Each chapter (childhood/spring, youth/summer, twilight/winter) has distinct
    /// ambient animation characteristics that reflect its emotional tone.
    ///
    /// Implements: MicroAnimationSystem Epic, Story 004 (chapter animation presets).
    /// </summary>
    [Serializable]
    public class ChapterAnimationPreset
    {
        public string ChapterKey;
        public int TargetFPS;
        public float MinCyclePeriod;
        public float MaxCyclePeriod;
        public float MinAmplitude;
        public float MaxAmplitude;
        public EaseType DefaultEasing;
        public float StillMotionRatio;

        /// <summary>
        /// Returns the built-in animation preset for the given chapter key.
        /// ch01 = Childhood (Spring): fast, elastic, wide amplitude (target 11fps, 0.5-0.8s cycles, 4-12px)
        /// ch02 = Youth (Summer): moderate, sine easing, medium amplitude (target 9fps, 0.8-1.0s cycles, 3-8px)
        /// ch03 = Twilight (Winter): slow, subdued, narrow amplitude (target 7fps, 1.0-1.5s cycles, 2-4px)
        /// Unknown keys default to ch01 (childhood).
        /// </summary>
        public static ChapterAnimationPreset GetChapterPreset(string chapterKey)
        {
            switch (chapterKey)
            {
                case "ch01": // Childhood (Spring) — lively, bouncy, wide motion
                    return new ChapterAnimationPreset
                    {
                        ChapterKey = "ch01",
                        TargetFPS = 11,
                        MinCyclePeriod = 0.5f,
                        MaxCyclePeriod = 0.8f,
                        MinAmplitude = 4f,
                        MaxAmplitude = 12f,
                        DefaultEasing = EaseType.EaseOutElastic,
                        StillMotionRatio = 0.85f
                    };
                case "ch02": // Youth (Summer) — moderate, flowing, medium motion
                    return new ChapterAnimationPreset
                    {
                        ChapterKey = "ch02",
                        TargetFPS = 9,
                        MinCyclePeriod = 0.8f,
                        MaxCyclePeriod = 1.0f,
                        MinAmplitude = 3f,
                        MaxAmplitude = 8f,
                        DefaultEasing = EaseType.SineInOut,
                        StillMotionRatio = 0.90f
                    };
                case "ch03": // Twilight (Winter) — slow, subdued, narrow motion
                    return new ChapterAnimationPreset
                    {
                        ChapterKey = "ch03",
                        TargetFPS = 7,
                        MinCyclePeriod = 1.0f,
                        MaxCyclePeriod = 1.5f,
                        MinAmplitude = 2f,
                        MaxAmplitude = 4f,
                        DefaultEasing = EaseType.SineIn,
                        StillMotionRatio = 0.95f
                    };
                default:
                    return GetChapterPreset("ch01"); // Default to childhood
            }
        }
    }
}
