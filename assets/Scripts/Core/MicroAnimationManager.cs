using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Echoes
{
    /// <summary>
    /// Performance degradation levels for the micro-animation system.
    /// Determines which animation categories are ticked each frame.
    ///
    /// Implements: MicroAnimationSystem Epic, Story 003 (performance degradation).
    /// </summary>
    public enum PerfLevel
    {
        /// <summary>All animations at 60fps. Full quality.</summary>
        High,
        /// <summary>Ambient loops tick every 2nd frame (30fps). Triggered + Feedback at 60fps.</summary>
        Medium,
        /// <summary>Ambient paused. Only Triggered + Feedback animate.</summary>
        Low,
        /// <summary>Only Feedback animations continue. Maximal degradation.</summary>
        Minimal
    }

    /// <summary>
    /// Glow intensity level for ink dot feedback on interactable objects.
    /// Drives the vermilion ink dot's visual prominence in response to player interaction.
    ///
    /// Implements: MicroAnimationSystem Epic, Story 004 (ink dot glow feedback).
    /// </summary>
    public enum GlowLevel
    {
        /// <summary>Static ink dot: 4-6px vermilion dot, no glow, no hue shift.</summary>
        L1_Static = 0,
        /// <summary>Breathing pulse: opacity 100%↔75% sine wave, 2.5s period.</summary>
        L2_Breathing = 1,
        /// <summary>Inner glow: warm hue shift (+300K equivalent), saturation +5-10%.</summary>
        L3_InnerGlow = 2
    }

    /// <summary>
    /// Runtime manager for the micro-animation system.
    /// Singleton MonoBehaviour that drives all ambient loops, triggered one-shots,
    /// feedback animations, and GPU shader animations for memory fragments.
    ///
    /// Implements IMicroAnimationManager for injection into InteractionManager.
    /// Uses a pool of MicroTween structs (List-based, zero-GC hot path) to track
    /// active CPU-side tweens, and a separate ShaderAnimEntry list for GPU-side
    /// shader animations driven via MaterialPropertyBlock.
    ///
    /// Implements: MicroAnimationSystem Epic — Stories 001, 002, 003.
    /// </summary>
    public class MicroAnimationManager : MonoBehaviour, IMicroAnimationManager
    {
        public static MicroAnimationManager Instance { get; private set; }

        private MicroAnimationCatalog _catalog;
        private readonly List<ActiveTween> _activeTweens = new List<ActiveTween>();
        private string _currentFragmentId;

        // Pre-allocated lists for zero-GC cleanup
        private readonly List<int> _removeIndices = new List<int>();
        private readonly List<int> _shaderRemoveIndices = new List<int>();

        // =========================================================================
        // GPU Shader Animation Fields (Story 002)
        // =========================================================================

        /// <summary>Whether MaterialPropertyBlock is effective on this device.</summary>
        internal bool _useMaterialPropertyBlock = true;

        /// <summary>Whether SpriteLit vertex displacement is supported.</summary>
        internal bool _useVertexDisplacement = false;

        /// <summary>Shared MPB reused across all shader animations (zero-GC).</summary>
        private MaterialPropertyBlock _sharedMPB;

        /// <summary>Active GPU shader animations tracked in a List (zero-GC index access).</summary>
        private readonly List<ShaderAnimEntry> _shaderAnims = new List<ShaderAnimEntry>();

        // =========================================================================
        // Performance Degradation Fields (Story 003)
        // =========================================================================

        /// <summary>Current performance level, evaluated each frame after 3-sample averaging.</summary>
        internal PerfLevel _currentPerfLevel = PerfLevel.High;

        /// <summary>Ring buffer for 3-frame averaging of unscaledDeltaTime.</summary>
        private readonly float[] _frameTimeRingBuffer = new float[3];

        private int _frameTimeBufferIndex;
        private int _frameTimeSamplesCollected;
        private int _ambientTickCounter;

        // =========================================================================
        // Emotion Preset Fields (Story 003)
        // =========================================================================

        /// <summary>The currently active emotion preset (null = default warmth preset).</summary>
        internal EmotionPreset _activeEmotionPreset;

        // =========================================================================
        // Glow Level Tracking (Story 004)
        // =========================================================================

        /// <summary>Per-object glow level state, keyed by GameObject name.</summary>
        private readonly Dictionary<string, GlowLevel> _glowLevels = new Dictionary<string, GlowLevel>();

        // =========================================================================
        // Per-Object Tween Tracking (Story 002 — InteractionFeedback)
        // =========================================================================

        /// <summary>
        /// Maps objectId to tween indices in _activeTweens.
        /// Used by StopAllForObject to find and remove all tweens for a specific object.
        /// Populated by PlayTriggered and PlayFeedback overloads that accept an objectId.
        /// </summary>
        private readonly Dictionary<string, List<int>> _objectTweenIndices = new Dictionary<string, List<int>>();

        // =========================================================================
        // Ink Dot Visual Spec Constants (Story 004)
        // =========================================================================
        //
        // These constants document the visual specification for the vermilion ink dot
        // asset (NOT created in code — the actual sprite is authored by the artist).
        //
        //   Color:      Vermilion Ink #A03828 (R=160, G=56, B=40)
        //   Diameter:   4-6px at native resolution
        //   Texture:    Brush stroke ink dot (NOT a geometric circle)
        //   Glow:       L1 = no glow (this is ink, not neon)
        //
        // ABSOLUTELY FORBIDDEN per design spec:
        //   - No outer glow / bloom
        //   - No neon colors
        //   - No geometric circular halos
        //   - No pure white highlights
        //   - No blinking >1Hz
        // =========================================================================

        /// <summary>L1 Vermilion Ink dot color (hex #A03828). R=160, G=56, B=40.</summary>
        public static readonly Color InkDotColor = new Color(0.627f, 0.220f, 0.157f, 1f);

        /// <summary>L1 static glow intensity on the shader _GlowLevel parameter.</summary>
        public const float L1_STATIC_GLOW = 0.2f;

        /// <summary>L3 inner glow intensity on the shader _GlowLevel parameter.</summary>
        public const float L3_INNER_GLOW = 1.0f;

        /// <summary>L3 hue shift representing +300K color temperature warmth.</summary>
        public const float L3_HUE_SHIFT = 0.08f;

        /// <summary>L3-to-L1 fade duration in seconds.</summary>
        public const float L3_FADE_DURATION = 0.5f;

        /// <summary>L2 breathing minimum opacity (75% of full).</summary>
        public const float L2_BREATHING_MIN = 0.75f;

        /// <summary>L2 breathing maximum opacity (100% of full).</summary>
        public const float L2_BREATHING_MAX = 1.0f;

        /// <summary>L2 breathing sine wave period in seconds.</summary>
        public const float L2_BREATHING_PERIOD = 2.5f;

        // =========================================================================
        // Public Properties
        // =========================================================================

        /// <summary>Number of active CPU-side tweens (for test inspection).</summary>
        public int ActiveTweenCount => _activeTweens.Count;

        /// <summary>Number of active GPU shader animations (for test inspection).</summary>
        internal int ActiveShaderAnimCount => _shaderAnims.Count;

        /// <summary>The fragment ID currently receiving animation updates.</summary>
        public string CurrentFragmentId => _currentFragmentId;

        /// <summary>Current performance level (for test inspection).</summary>
        public PerfLevel CurrentPerfLevel => _currentPerfLevel;

        // =========================================================================
        // Unity Lifecycle
        // =========================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            DetectCapabilities();
            _sharedMPB = new MaterialPropertyBlock();
            _activeEmotionPreset = GetDefaultPreset();
        }

        private void OnEnable()
        {
            GameSceneManager.OnFragmentTransitioned += HandleFragmentTransitioned;
        }

        private void OnDisable()
        {
            GameSceneManager.OnFragmentTransitioned -= HandleFragmentTransitioned;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Sets the animation catalog. Called during bootstrapping after catalog is loaded.
        /// </summary>
        public void SetCatalog(MicroAnimationCatalog catalog)
        {
            _catalog = catalog;
        }

        // =========================================================================
        // Catalog Lookup (AC-1, Story 001)
        // =========================================================================

        /// <summary>
        /// Looks up an AmbientAnimDef by ID. Logs a warning if the catalog is null
        /// or the definition is missing.
        /// </summary>
        public AmbientAnimDef GetAmbientDef(string defId)
        {
            if (_catalog == null)
            {
                Debug.LogWarning("MicroAnimationManager: Catalog not loaded.");
                return null;
            }
            var def = _catalog.GetAmbientDef(defId);
            if (def == null)
                Debug.LogWarning($"MicroAnimationManager: AmbientAnimDef '{defId}' not found in catalog.");
            return def;
        }

        /// <summary>
        /// Looks up a TriggeredAnimDef by ID. Logs a warning if the catalog is null
        /// or the definition is missing.
        /// </summary>
        public TriggeredAnimDef GetTriggeredDef(string defId)
        {
            if (_catalog == null)
            {
                Debug.LogWarning("MicroAnimationManager: Catalog not loaded.");
                return null;
            }
            var def = _catalog.GetTriggeredDef(defId);
            if (def == null)
                Debug.LogWarning($"MicroAnimationManager: TriggeredAnimDef '{defId}' not found in catalog.");
            return def;
        }

        /// <summary>
        /// Looks up a FeedbackAnimDef by ID. Logs a warning if the catalog is null
        /// or the definition is missing.
        /// </summary>
        public FeedbackAnimDef GetFeedbackDef(string defId)
        {
            if (_catalog == null)
            {
                Debug.LogWarning("MicroAnimationManager: Catalog not loaded.");
                return null;
            }
            var def = _catalog.GetFeedbackDef(defId);
            if (def == null)
                Debug.LogWarning($"MicroAnimationManager: FeedbackAnimDef '{defId}' not found in catalog.");
            return def;
        }

        // =========================================================================
        // Animation Control — CPU Tweens (Story 001)
        // =========================================================================

        /// <summary>
        /// Starts a looping ambient tween for a fragment.
        /// The tween oscillates from 0 to DefaultAmplitude over DefaultSpeed seconds.
        /// </summary>
        public void StartAmbientLoop(AmbientAnimDef def, string fragmentId)
        {
            if (def == null) return;
            var tween = new MicroTween(0f, def.DefaultAmplitude, def.DefaultSpeed, def.DefaultEasing, isLooping: true);
            _activeTweens.Add(new ActiveTween
            {
                FragmentId = fragmentId,
                Tween = tween,
                Category = AnimationCategory.Ambient
            });
        }

        /// <summary>
        /// Plays a one-shot triggered animation by ID.
        /// The optional onComplete callback fires when the tween finishes.
        /// </summary>
        public void PlayTriggered(string animId, Action onComplete = null)
        {
            var def = GetTriggeredDef(animId);
            if (def == null) return;
            var tween = new MicroTween(0f, 1f, def.Duration, def.Easing);
            _activeTweens.Add(new ActiveTween
            {
                FragmentId = _currentFragmentId,
                Tween = tween,
                OnComplete = onComplete,
                Category = AnimationCategory.Triggered
            });
        }

        /// <summary>
        /// Plays a one-shot triggered animation by ID, scoped to a specific object.
        /// Tracks the tween index for <see cref="StopAllForObject"/> lifecycle management.
        /// If overrideDuration > 0, it replaces the catalog definition's duration.
        /// </summary>
        /// <param name="animId">The animation identifier (e.g., "L3_flash").</param>
        /// <param name="objectId">The interactable object this animation belongs to.</param>
        /// <param name="overrideDuration">If > 0, overrides the catalog def's duration in seconds.</param>
        /// <param name="onComplete">Optional callback fired when the tween finishes.</param>
        public void PlayTriggered(string animId, string objectId, float overrideDuration = -1f, Action onComplete = null)
        {
            var def = GetTriggeredDef(animId);
            if (def == null) return;
            float duration = overrideDuration > 0f ? overrideDuration : def.Duration;
            var tween = new MicroTween(0f, 1f, duration, def.Easing);
            int index = _activeTweens.Count;
            _activeTweens.Add(new ActiveTween
            {
                FragmentId = _currentFragmentId,
                ObjectId = objectId,
                Tween = tween,
                OnComplete = onComplete,
                Category = AnimationCategory.Triggered
            });
            if (!_objectTweenIndices.ContainsKey(objectId))
                _objectTweenIndices[objectId] = new List<int>();
            _objectTweenIndices[objectId].Add(index);
        }

        /// <summary>
        /// IMicroAnimationManager implementation.
        /// Returns a task that completes when the triggered animation finishes
        /// or immediately if the definition is not found.
        /// </summary>
        async Task IMicroAnimationManager.PlayTriggered(string animationId)
        {
            var tcs = new TaskCompletionSource<bool>();
            var def = GetTriggeredDef(animationId);
            if (def == null)
            {
                tcs.SetResult(false);
            }
            else
            {
                var tween = new MicroTween(0f, 1f, def.Duration, def.Easing);
                _activeTweens.Add(new ActiveTween
                {
                    FragmentId = _currentFragmentId,
                    Tween = tween,
                    OnComplete = () => tcs.TrySetResult(true),
                    Category = AnimationCategory.Triggered
                });
            }
            await tcs.Task;
        }

        /// <summary>
        /// Plays a one-shot feedback animation by ID.
        /// The optional onComplete callback fires when the tween finishes.
        /// </summary>
        public void PlayFeedback(string animId, Action onComplete = null)
        {
            var def = GetFeedbackDef(animId);
            if (def == null) return;
            var tween = new MicroTween(0f, 1f, def.Duration, EaseType.EaseOutCubic);
            _activeTweens.Add(new ActiveTween
            {
                FragmentId = _currentFragmentId,
                Tween = tween,
                OnComplete = onComplete,
                Category = AnimationCategory.Feedback
            });
        }

        /// <summary>
        /// Plays a one-shot feedback animation by ID, scoped to a specific object.
        /// Tracks the tween index for <see cref="StopAllForObject"/> lifecycle management.
        /// If overrideDuration > 0, it replaces the catalog definition's duration.
        /// </summary>
        /// <param name="animId">The animation identifier.</param>
        /// <param name="objectId">The interactable object this animation belongs to.</param>
        /// <param name="overrideDuration">If > 0, overrides the catalog def's duration in seconds.</param>
        /// <param name="onComplete">Optional callback fired when the tween finishes.</param>
        public void PlayFeedback(string animId, string objectId, float overrideDuration = -1f, Action onComplete = null)
        {
            var def = GetFeedbackDef(animId);
            if (def == null) return;
            float duration = overrideDuration > 0f ? overrideDuration : def.Duration;
            var tween = new MicroTween(0f, 1f, duration, EaseType.EaseOutCubic);
            int index = _activeTweens.Count;
            _activeTweens.Add(new ActiveTween
            {
                FragmentId = _currentFragmentId,
                ObjectId = objectId,
                Tween = tween,
                OnComplete = onComplete,
                Category = AnimationCategory.Feedback
            });
            if (!_objectTweenIndices.ContainsKey(objectId))
                _objectTweenIndices[objectId] = new List<int>();
            _objectTweenIndices[objectId].Add(index);
        }

        /// <summary>
        /// Stops all active tweens associated with the given fragment ID,
        /// and removes all shader animations for that fragment.
        /// No-op if fragmentId is null or empty.
        /// </summary>
        public void StopAllForFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return;

            // Clean CPU tweens
            _removeIndices.Clear();
            for (int i = 0; i < _activeTweens.Count; i++)
            {
                if (_activeTweens[i].FragmentId == fragmentId)
                    _removeIndices.Add(i);
            }
            for (int i = _removeIndices.Count - 1; i >= 0; i--)
            {
                _activeTweens.RemoveAt(_removeIndices[i]);
            }

            // Clean GPU shader animations
            _shaderRemoveIndices.Clear();
            for (int i = 0; i < _shaderAnims.Count; i++)
            {
                if (_shaderAnims[i].FragmentId == fragmentId)
                    _shaderRemoveIndices.Add(i);
            }
            for (int i = _shaderRemoveIndices.Count - 1; i >= 0; i--)
            {
                CleanupShaderAnimEntry(_shaderAnims[_shaderRemoveIndices[i]]);
                _shaderAnims.RemoveAt(_shaderRemoveIndices[i]);
            }
        }

        /// <summary>
        /// Stops all CPU tweens and GPU shader animations associated with a specific
        /// interactable object, and resets its glow to L1_Static.
        ///
        /// Scans _activeTweens in reverse for entries whose ObjectId matches
        /// (safe against stale indices from completed tween removal). Also removes
        /// shader animations whose Target.name matches objectId.
        /// </summary>
        /// <param name="objectId">The object ID to stop all animations for.</param>
        public void StopAllForObject(string objectId)
        {
            if (string.IsNullOrEmpty(objectId)) return;

            // Remove CPU tweens for this object (reverse scan -- safe against stale dict indices)
            for (int i = _activeTweens.Count - 1; i >= 0; i--)
            {
                if (_activeTweens[i].ObjectId == objectId)
                    _activeTweens.RemoveAt(i);
            }

            // Remove shader animations whose Target name matches objectId
            for (int i = _shaderAnims.Count - 1; i >= 0; i--)
            {
                if (_shaderAnims[i].Target != null && _shaderAnims[i].Target.name == objectId)
                {
                    CleanupShaderAnimEntry(_shaderAnims[i]);
                    _shaderAnims.RemoveAt(i);
                }
            }

            // Reset glow to L1_Static (static ink dot)
            SetGlowLevel(objectId, GlowLevel.L1_Static);

            // Clean up the object tween index dictionary entry
            _objectTweenIndices.Remove(objectId);
        }

        // =========================================================================
        // GPU Shader Animation (Story 002)
        // =========================================================================

        /// <summary>
        /// Starts a GPU-driven ambient shader animation for a SpriteRenderer.
        /// Binds the ambient def to the target renderer and adds it to the
        /// shader animation list for per-frame updates.
        /// </summary>
        /// <param name="def">The ambient animation definition with shader property info.</param>
        /// <param name="target">The SpriteRenderer to animate.</param>
        /// <param name="fragmentId">The fragment this animation belongs to.</param>
        public void StartShaderAnimation(AmbientAnimDef def, SpriteRenderer target, string fragmentId)
        {
            if (def == null || target == null) return;

            // For vertex displacement, if fallback 1 is active, register as shader animation
            // but the per-frame update will use CPU Transform fallback
            var entry = new ShaderAnimEntry
            {
                FragmentId = fragmentId,
                Target = target,
                MPB = null, // Shared MPB used in hot path; per-entry MPB created if needed
                MaterialInstance = null, // Set only if fallback 2 is active
                StartTime = Time.time,
                Def = def,
                UseMaterialFallback = false
            };

            // If fallback 2 is active (MPB not working), create per-renderer material instance
            if (!_useMaterialPropertyBlock && target.material != null)
            {
                entry.MaterialInstance = new Material(target.material);
                entry.UseMaterialFallback = true;
            }

            _shaderAnims.Add(entry);
        }

        /// <summary>
        /// Cleans up per-entry resources (material instances) associated with a shader anim.
        /// Called when a shader animation is removed from the active list.
        /// </summary>
        private void CleanupShaderAnimEntry(ShaderAnimEntry entry)
        {
            if (entry.MaterialInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(entry.MaterialInstance);
                else
                    DestroyImmediate(entry.MaterialInstance);
            }
        }

        /// <summary>
        /// Per-frame update for all active GPU shader animations.
        /// Iterates shader anims and updates MaterialPropertyBlock or material
        /// instance parameters based on elapsed time, emotion preset, and fallback state.
        /// Zero-GC: no allocations in this method.
        /// </summary>
        private void UpdateShaderAnimations()
        {
            for (int i = 0; i < _shaderAnims.Count; i++)
            {
                var entry = _shaderAnims[i];
                if (entry.Target == null) continue;

                float elapsed = Time.time - entry.StartTime;
                float speed = entry.Def.DefaultSpeed > 0f ? entry.Def.DefaultSpeed : 1f;
                float amplitude = entry.Def.DefaultAmplitude;

                // Apply emotion preset modulation
                if (_activeEmotionPreset != null)
                {
                    speed *= _activeEmotionPreset.SpeedMultiplier;
                    amplitude *= _activeEmotionPreset.AmplitudeMultiplier;
                }

                // Looping: wrap elapsed into [0, speed] range
                float wrappedElapsed = elapsed % speed;
                float t = speed > 0f ? Mathf.Clamp01(wrappedElapsed / speed) : 1f;
                float easedT = MicroTween.ApplyEase(t, entry.Def.DefaultEasing);

                // Map eased t to property range
                float rangeMin = entry.Def.ShaderPropertyRange.x;
                float rangeMax = entry.Def.ShaderPropertyRange.y;
                float paramValue = Mathf.Lerp(rangeMin, rangeMax, easedT) * amplitude;

                switch (entry.Def.Implementation)
                {
                    case AnimationImpl.Shader_VertexDisplace:
                        if (_useVertexDisplacement)
                        {
                            // GPU path: set the vertex displacement parameter on the shader
                            ApplyShaderParam(entry, entry.Def.ShaderPropertyName, paramValue);
                        }
                        else
                        {
                            // Fallback 1: CPU Transform displacement
                            ApplyParallaxFallback(entry.Target, paramValue);
                        }
                        break;

                    case AnimationImpl.Shader_UVScroll:
                        // UV scroll driven by raw elapsed time (not wrapped easing)
                        ApplyShaderParam(entry, entry.Def.ShaderPropertyName, elapsed * amplitude);
                        break;

                    case AnimationImpl.Shader_MaterialPulse:
                        ApplyShaderParam(entry, entry.Def.ShaderPropertyName, paramValue);
                        break;

                    case AnimationImpl.Shader_Progress:
                        ApplyShaderParam(entry, entry.Def.ShaderPropertyName, paramValue);
                        break;

                    default:
                        // Non-shader implementations are handled by CPU tweens
                        break;
                }

                // Set emotion-driven shader globals if using MPB or material
                if (_activeEmotionPreset != null &&
                    !string.IsNullOrEmpty(entry.Def.ShaderPropertyName))
                {
                    ApplyShaderParam(entry, "_EmotionHue", _activeEmotionPreset.HueShift);
                    ApplyShaderParam(entry, "_EmotionSaturation", 1f + _activeEmotionPreset.SaturationModulation);
                    ApplyShaderParam(entry, "_GlowLevel", _activeEmotionPreset.GlowIntensity);
                }
            }
        }

        /// <summary>
        /// Applies a float shader parameter to the given entry's SpriteRenderer,
        /// using MaterialPropertyBlock when available or Material instance as fallback.
        /// Per ADR-0012 Fallback 2.
        /// </summary>
        private void ApplyShaderParam(ShaderAnimEntry entry, string paramName, float value)
        {
            if (entry.UseMaterialFallback && entry.MaterialInstance != null)
            {
                entry.MaterialInstance.SetFloat(paramName, value);
                // Material instance is already assigned to the renderer; SetFloat updates in-place
                return;
            }

            // Use shared MPB — set the property and apply to renderer
            if (_sharedMPB != null)
            {
                _sharedMPB.SetFloat(paramName, value);
                entry.Target.SetPropertyBlock(_sharedMPB);
            }
        }

        /// <summary>
        /// CPU Transform fallback for vertex displacement (ADR-0012 Fallback 1).
        /// Applies a local position offset and subtle squash/stretch distortion
        /// to simulate vertex displacement when the shader path is unavailable.
        /// </summary>
        /// <param name="sr">The target SpriteRenderer.</param>
        /// <param name="distortStrength">Normalized strength in [0, 1].</param>
        internal static void ApplyParallaxFallback(SpriteRenderer sr, float distortStrength)
        {
            if (sr == null) return;
            float halfStrength = distortStrength * 0.5f;
            sr.transform.localPosition = new Vector3(halfStrength, 0f, 0f);
            sr.transform.localScale = new Vector3(1f + halfStrength, 1f - halfStrength, 1f);
        }

        /// <summary>
        /// Detects engine capabilities for vertex displacement and MPB compatibility.
        /// Called once in Awake(). Sets _useVertexDisplacement and _useMaterialPropertyBlock.
        /// Logs warnings when fallbacks are activated.
        ///
        /// URP 2D SpriteLit shaders do not support vertex displacement — Fallback 1.
        /// If MPB + SRP Batcher conflict is detected — Fallback 2.
        /// </summary>
        private void DetectCapabilities()
        {
            // --- Vertex Displacement Detection ---
            // URP 2D Renderer with SpriteLit shader does not support vertex displacement.
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                // URP (or HDRP) is active — assume vertex displacement is unsupported on 2D sprites
                _useVertexDisplacement = false;
                Debug.LogWarning(
                    "[MicroAnimationManager] URP detected: SpriteLit shader vertex displacement " +
                    "not supported. Auto-switching to CPU Transform fallback (Fallback 1).");
            }
            else
            {
                // Built-in RP — may support vertex displacement on custom shaders
                _useVertexDisplacement = true;
            }

            // --- MaterialPropertyBlock Detection ---
            _useMaterialPropertyBlock = TestMaterialPropertyBlock();
            if (!_useMaterialPropertyBlock)
            {
                Debug.LogWarning(
                    "[MicroAnimationManager] MaterialPropertyBlock not effective on this device " +
                    "(SRP Batcher conflict detected). Auto-switching to Material instance fallback (Fallback 2).");
            }
        }

        /// <summary>
        /// Tests whether MaterialPropertyBlock is effective on this device.
        /// Creates a temporary SpriteRenderer, sets a property via MPB,
        /// and reads it back via GetPropertyBlock. If the value is unchanged,
        /// MPB is not effective (likely SRP Batcher conflict) and we fall back
        /// to Material instances.
        /// </summary>
        private bool TestMaterialPropertyBlock()
        {
            GameObject testGo = null;
            try
            {
                testGo = new GameObject("__MPB_Detect__");
                testGo.hideFlags = HideFlags.HideAndDontSave;
                var sr = testGo.AddComponent<SpriteRenderer>();
                if (sr == null) return true;

                // Create a test MPB and set a well-known value
                var testMpb = new MaterialPropertyBlock();
                testMpb.SetColor("_Color", new Color(0.123f, 0.456f, 0.789f, 1f));
                sr.SetPropertyBlock(testMpb);

                // Read back via a fresh MPB
                var readMpb = new MaterialPropertyBlock();
                sr.GetPropertyBlock(readMpb);
                Color readColor = readMpb.GetColor("_Color");

                // Compare the read-back color with our set value
                const float epsilon = 0.001f;
                return Mathf.Abs(readColor.r - 0.123f) < epsilon &&
                       Mathf.Abs(readColor.g - 0.456f) < epsilon &&
                       Mathf.Abs(readColor.b - 0.789f) < epsilon;
            }
            catch
            {
                // If detection fails for any reason, assume MPB is working
                return true;
            }
            finally
            {
                if (testGo != null)
                {
                    if (Application.isPlaying)
                        Destroy(testGo);
                    else
                        DestroyImmediate(testGo);
                }
            }
        }

        // =========================================================================
        // Performance Degradation (Story 003)
        // =========================================================================

        /// <summary>
        /// Evaluates the performance level based on current frame time.
        ///
        /// Thresholds:
        ///   High:   frameTime <= 14ms (60fps)
        ///   Medium: frameTime > 14ms (target 30fps ambient)
        ///   Low:    frameTime > 20ms (ambient paused)
        ///   Minimal: frameTime > 33ms (only feedback at ~30fps)
        /// </summary>
        public static PerfLevel EvaluatePerfLevel(float currentFrameTime)
        {
            // Convert to milliseconds for threshold comparison
            float ms = currentFrameTime * 1000f;
            if (ms > 33.0f) return PerfLevel.Minimal;
            if (ms > 20.0f) return PerfLevel.Low;
            if (ms > 14.0f) return PerfLevel.Medium;
            return PerfLevel.High;
        }

        /// <summary>
        /// Returns the 3-frame rolling average of unscaledDeltaTime.
        /// Returns 0f if not enough samples have been collected yet.
        /// </summary>
        private float GetAverageFrameTime()
        {
            if (_frameTimeSamplesCollected == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _frameTimeSamplesCollected; i++)
                sum += _frameTimeRingBuffer[i];
            return sum / _frameTimeSamplesCollected;
        }

        // =========================================================================
        // Emotion Preset (Story 003)
        // =========================================================================

        /// <summary>
        /// Applies an emotion preset by category name. If the category is not found
        /// in the catalog or if the catalog is null, falls back to the default
        /// "warmth" preset: speedMultiplier=0.8, amplitudeMultiplier=1.3, no color shift.
        /// </summary>
        public void ApplyEmotionPreset(string emotionCategory)
        {
            var preset = _catalog?.GetEmotionPreset(emotionCategory);
            if (preset == null)
                preset = GetDefaultPreset();
            _activeEmotionPreset = preset;
        }

        /// <summary>
        /// Returns the hardcoded default "warmth" emotion preset.
        /// Used when EmotionalTagSystem is not ready or a requested category is not found.
        /// Values: speedMultiplier=0.8, amplitudeMultiplier=1.3, no color shift.
        /// </summary>
        internal static EmotionPreset GetDefaultPreset()
        {
            return new EmotionPreset
            {
                EmotionCategory = "warmth",
                SpeedMultiplier = 0.8f,
                AmplitudeMultiplier = 1.3f,
                HueShift = 0f,
                SaturationModulation = 0f,
                BrightnessModulation = 0f,
                GlowIntensity = 0f
            };
        }

        // =========================================================================
        // Glow Level Control (Story 004)
        // =========================================================================

        /// <summary>
        /// Sets the glow level for an interactable object's ink dot.
        /// L1_Static: static vermilion dot, no breathing, no hue shift.
        /// L2_Breathing: sine-wave opacity pulse (100%↔75%, 2.5s period).
        /// L3_InnerGlow: warm inner glow (+300K hue shift), fades to L1 after 0.5s.
        /// </summary>
        /// <param name="objectId">The GameObject name of the interactable.</param>
        /// <param name="level">The desired glow level.</param>
        public void SetGlowLevel(string objectId, GlowLevel level)
        {
            if (string.IsNullOrEmpty(objectId)) return;

            _glowLevels[objectId] = level;

            switch (level)
            {
                case GlowLevel.L1_Static:
                    // Static ink dot — minimal glow, no hue shift
                    SetShaderGlowForObject(objectId, L1_STATIC_GLOW, 0f);
                    break;
                case GlowLevel.L2_Breathing:
                    // Start breathing MicroTween — sine wave 2.5s period
                    StartBreathingGlow(objectId);
                    break;
                case GlowLevel.L3_InnerGlow:
                    // Inner warm glow — full intensity, warm hue shift equivalent to +300K
                    SetShaderGlowForObject(objectId, L3_INNER_GLOW, L3_HUE_SHIFT);
                    // Schedule fade back to L1 after the specified duration
                    StartCoroutine(FadeGlowToL1(objectId, L3_FADE_DURATION));
                    break;
            }
        }

        /// <summary>
        /// Sets the _GlowLevel and _EmotionHue shader parameters on all shader
        /// animations bound to the given object ID.
        /// Iterates the active shader animation list and applies the parameters
        /// via MaterialPropertyBlock (or Material instance fallback).
        /// </summary>
        /// <param name="objectId">The GameObject name to match against SpriteRenderer.name.</param>
        /// <param name="glowLevel">Value to set on the _GlowLevel shader parameter.</param>
        /// <param name="hueShift">Value to set on the _EmotionHue shader parameter.</param>
        private void SetShaderGlowForObject(string objectId, float glowLevel, float hueShift)
        {
            for (int i = 0; i < _shaderAnims.Count; i++)
            {
                var entry = _shaderAnims[i];
                if (entry.Target != null && entry.Target.name == objectId)
                {
                    ApplyShaderParam(entry, "_GlowLevel", glowLevel);
                    ApplyShaderParam(entry, "_EmotionHue", hueShift);
                }
            }
        }

        /// <summary>
        /// Starts a looping breathing glow tween for the given object.
        /// Creates a MicroTween that oscillates between min/max opacity values
        /// using a SineInOut easing over the breathing period. The tween's
        /// OnUpdate callback maps the tween's [min, max] output range to the
        /// shader _GlowLevel range [0.175, 0.5] for a subtle vermilion pulse.
        ///
        /// The tween is registered as a Feedback category animation so it is
        /// always ticked even at Minimal performance level.
        /// </summary>
        /// <param name="objectId">The GameObject name of the interactable.</param>
        private void StartBreathingGlow(string objectId)
        {
            var breathingTween = new MicroTween(
                L2_BREATHING_MIN, L2_BREATHING_MAX, L2_BREATHING_PERIOD,
                EaseType.SineInOut, isLooping: true);

            _activeTweens.Add(new ActiveTween
            {
                FragmentId = _currentFragmentId,
                Tween = breathingTween,
                Category = AnimationCategory.Feedback,
                OnUpdate = (val) =>
                {
                    // Map tween value from [0.75, 1.0] to _GlowLevel range [0.175, 0.5]
                    float glow = Mathf.Lerp(0.175f, 0.5f, (val - L2_BREATHING_MIN) / (L2_BREATHING_MAX - L2_BREATHING_MIN));
                    SetShaderGlowForObject(objectId, glow, 0f);
                }
            });
        }

        /// <summary>
        /// Coroutine that waits for the specified duration and then returns
        /// the object's glow to L1_Static. Used after L3_InnerGlow to fade
        /// the warm glow back to the static ink dot state.
        /// </summary>
        /// <param name="objectId">The GameObject name of the interactable.</param>
        /// <param name="duration">Time in seconds before reverting to L1.</param>
        private IEnumerator FadeGlowToL1(string objectId, float duration)
        {
            yield return new WaitForSeconds(duration);
            SetGlowLevel(objectId, GlowLevel.L1_Static);
        }

        // =========================================================================
        // Update Tick (Story 001 base, modified for 002-003)
        // =========================================================================

        private void Update()
        {
            // --- Frame time tracking for performance degradation (Story 003) ---
            float unscaledDt = Time.unscaledDeltaTime;
            _frameTimeRingBuffer[_frameTimeBufferIndex] = unscaledDt;
            _frameTimeBufferIndex = (_frameTimeBufferIndex + 1) % 3;
            if (_frameTimeSamplesCollected < 3) _frameTimeSamplesCollected++;

            if (_frameTimeSamplesCollected >= 3)
            {
                _currentPerfLevel = EvaluatePerfLevel(GetAverageFrameTime());
            }

            // --- GPU shader animations always update (Story 002) ---
            UpdateShaderAnimations();

            // --- CPU tween updates with performance degradation (Story 003) ---
            if (_activeTweens.Count == 0) return;

            bool tickAmbient;
            bool tickTriggered;
            bool tickFeedback;

            switch (_currentPerfLevel)
            {
                case PerfLevel.High:
                    tickAmbient = true;
                    tickTriggered = true;
                    tickFeedback = true;
                    break;
                case PerfLevel.Medium:
                    _ambientTickCounter++;
                    tickAmbient = (_ambientTickCounter % 2 == 0); // Every 2nd frame
                    tickTriggered = true;
                    tickFeedback = true;
                    break;
                case PerfLevel.Low:
                    tickAmbient = false;
                    tickTriggered = true;
                    tickFeedback = true;
                    break;
                case PerfLevel.Minimal:
                default:
                    tickAmbient = false;
                    tickTriggered = false;
                    tickFeedback = true;
                    break;
            }

            ProcessTweens(Time.deltaTime, tickAmbient, tickTriggered, tickFeedback);
        }

        /// <summary>
        /// Process CPU tweens with per-category tick control.
        /// Advances elapsed time, handles looping resets, completes one-shot tweens,
        /// and applies emotion preset speed modulation.
        /// Zero-GC: reuses pre-allocated _removeIndices list.
        /// </summary>
        private void ProcessTweens(float deltaTime, bool tickAmbient, bool tickTriggered, bool tickFeedback, bool applyEmotion = true)
        {
            _removeIndices.Clear();

            // Apply emotion speed multiplier (only in production path; tests skip this)
            float speedMult = 1f;
            if (applyEmotion && _activeEmotionPreset != null)
                speedMult = _activeEmotionPreset.SpeedMultiplier;

            for (int i = 0; i < _activeTweens.Count; i++)
            {
                var entry = _activeTweens[i];

                // Determine whether to tick this category
                bool shouldTick;
                switch (entry.Category)
                {
                    case AnimationCategory.Ambient: shouldTick = tickAmbient; break;
                    case AnimationCategory.Triggered: shouldTick = tickTriggered; break;
                    case AnimationCategory.Feedback: shouldTick = tickFeedback; break;
                    default: shouldTick = tickAmbient; break;
                }

                if (!shouldTick)
                {
                    _activeTweens[i] = entry;
                    continue;
                }

                entry.Tween.Elapsed += deltaTime * speedMult;

                // Fire per-frame update callback with current evaluated value (Story 004)
                entry.OnUpdate?.Invoke(entry.Tween.Evaluate());

                if (entry.Tween.IsLooping && entry.Tween.Elapsed >= entry.Tween.Duration)
                {
                    entry.Tween.Elapsed = 0f;
                }
                else if (!entry.Tween.IsLooping && entry.Tween.IsComplete)
                {
                    _removeIndices.Add(i);
                    entry.OnComplete?.Invoke();
                }

                _activeTweens[i] = entry;
            }

            for (int i = _removeIndices.Count - 1; i >= 0; i--)
            {
                _activeTweens.RemoveAt(_removeIndices[i]);
            }
        }

        /// <summary>
        /// Advances all active tweens by the given deltaTime (all categories, no degradation).
        /// Internal (test-accessible) for deterministic testing without Time.deltaTime
        /// and without performance degradation interference.
        /// </summary>
        internal void UpdateTick(float deltaTime)
        {
            // Test path: no emotion, no degradation — pure tween advancement
            ProcessTweens(deltaTime, tickAmbient: true, tickTriggered: true, tickFeedback: true, applyEmotion: false);
        }

        /// <summary>
        /// Returns the current evaluated value of the tween at the given index.
        /// For test inspection of tween progress.
        /// </summary>
        internal float GetTweenValue(int index)
        {
            if (index < 0 || index >= _activeTweens.Count) return 0f;
            return _activeTweens[index].Tween.Evaluate();
        }

        // =========================================================================
        // Fragment Transition
        // =========================================================================

        private void HandleFragmentTransitioned(string chapterKey, string fragmentId)
        {
            StopAllForFragment(_currentFragmentId);
            _currentFragmentId = fragmentId;

            // Story 003: Recovery — reset perf level on fragment transition
            _currentPerfLevel = PerfLevel.High;
            _ambientTickCounter = 0;
        }

        // =========================================================================
        // Internal Types
        // =========================================================================

        /// <summary>
        /// Wrapper for an active tween, binding it to a fragment ID,
        /// an animation category (for perf degradation), an optional
        /// completion callback, and an optional per-frame update callback.
        /// </summary>
        private struct ActiveTween
        {
            public string FragmentId;
            /// <summary>
            /// The interactable object ID this tween is bound to (for StopAllForObject lifecycle).
            /// Null or empty for tweens not associated with a specific interactable object.
            /// </summary>
            public string ObjectId;
            public MicroTween Tween;
            public Action OnComplete;
            public AnimationCategory Category;
            /// <summary>Per-frame callback receiving the tween's current evaluated value. Fires after Elapsed is advanced.</summary>
            public Action<float> OnUpdate;
        }

        /// <summary>
        /// Tracks a single GPU-driven shader animation for a SpriteRenderer.
        /// Bound to a fragment ID for lifecycle management. Uses shared MPB
        /// in the hot path; stores a Material instance reference for fallback 2.
        ///
        /// Value type stored in List&lt;ShaderAnimEntry&gt; — reference fields
        /// (Target, MPB, MaterialInstance, Def) are heap objects pointed to by
        /// struct fields; no per-field allocation occurs.
        /// </summary>
        private struct ShaderAnimEntry
        {
            public string FragmentId;
            public SpriteRenderer Target;
            public MaterialPropertyBlock MPB;
            public Material MaterialInstance;
            public float StartTime;
            public AmbientAnimDef Def;
            public bool UseMaterialFallback;
        }
    }
}
