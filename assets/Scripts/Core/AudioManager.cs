using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Audio channel for volume control. Maps 1:1 to Audio Mixer exposed parameters.
/// </summary>
public enum AudioChannel
{
    Master,
    SFX,
    Music,
    Ambience
}

/// <summary>
/// Internal state machine for the audio system lifecycle.
/// </summary>
public enum AudioSystemState
{
    Uninitialized,
    Initializing,
    Ready,
    LoadingChapterAudio,
    Error
}

/// <summary>
/// Identifies which of the two music tracks is currently active.
/// Used by the A/B crossfade system (ADR-0013).
/// </summary>
public enum MusicTrack
{
    A,
    B
}

/// <summary>
/// Core audio system implementing ADR-0013 (Audio Architecture).
///
/// Singleton MonoBehaviour with DontDestroyOnLoad. Manages 4-layer Audio Mixer routing
/// (Master > SFX / Music / Ambience), dual-track music crossfade via AudioMixerSnapshot,
/// 10-source SFX priority pool, and Addressables-based AudioClip loading.
///
/// Audio Mixer is loaded via Resources (ADR-0013 explicit exception to ADR-0002) because
/// it is boot-critical (~1KB) and must be available before Addressables initialization.
/// All AudioClips are loaded via Addressables.
///
/// Usage:
///   AudioManager.Instance.PlaySFX("sfx_touch_generic");
///   AudioManager.Instance.PlayMusic("music_ch01_theme");
///   AudioManager.Instance.PlayAmbience("amb_forest");
///   AudioManager.Instance.SetVolume(AudioChannel.Master, 0.8f);
///
/// Implements: IAudioManager (for GameSceneManager), AudioSystem Epic (#3).
/// </summary>
public class AudioManager : MonoBehaviour, IAudioManager
{
    // =========================================================================
    // Singleton
    // =========================================================================

    /// <summary>Singleton instance. Set in Awake, cleared in OnDestroy.</summary>
    public static AudioManager Instance { get; private set; }

    // =========================================================================
    // Events
    // =========================================================================

    /// <summary>Fires when an audio error occurs (load failure, init failure).</summary>
    public static event Action<string> OnAudioError;

    /// <summary>Fires when the audio system state changes.</summary>
    public static event Action<AudioSystemState> OnStateChanged;

    // =========================================================================
    // Audio Mixer Exposed Parameter Names (const to prevent typos)
    // =========================================================================

    private const string PARAM_MASTER = "MasterVolume";
    private const string PARAM_SFX = "SFXVolume";
    private const string PARAM_MUSIC = "MusicVolume";
    private const string PARAM_AMBIENCE = "AmbienceVolume";

    // =========================================================================
    // PlayerPrefs Keys
    // =========================================================================

    private const string PP_MASTER = "Audio_Master";
    private const string PP_SFX = "Audio_SFX";
    private const string PP_MUSIC = "Audio_Music";
    private const string PP_AMBIENCE = "Audio_Ambience";

    // =========================================================================
    // Serialized Fields (optional Inspector overrides)
    // =========================================================================

    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private AudioMixerGroup _sfxGroup;
    [SerializeField] private AudioMixerGroup _musicGroup;
    [SerializeField] private AudioMixerGroup _ambienceGroup;

    // =========================================================================
    // Music Crossfade (A/B dual track)
    // =========================================================================

    private AudioSource _musicSourceA;
    private AudioSource _musicSourceB;
    private AudioMixerSnapshot _snapshotA;
    private AudioMixerSnapshot _snapshotB;
    private MusicTrack _currentTrack = MusicTrack.A;
    private string _currentMusicKey;

    // =========================================================================
    // Ambience
    // =========================================================================

    private AudioSource _ambienceSource;
    private string _currentAmbienceKey;

    // =========================================================================
    // SFX Pool
    // =========================================================================

    private SFXPool _sfxPool;

    // =========================================================================
    // State & Caching
    // =========================================================================

    private AudioSystemState _state = AudioSystemState.Uninitialized;
    private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, Task<AudioClip>> _pendingLoads = new Dictionary<string, Task<AudioClip>>();

    /// <summary>Chapters whose audio has been preloaded via Addressables.</summary>
    private readonly HashSet<string> _loadedChapterKeys = new HashSet<string>();

    /// <summary>Default volume values used when no PlayerPrefs entry exists.</summary>
    private const float DefaultMasterVolume = 0.8f;
    private const float DefaultSFXVolume = 0.7f;
    private const float DefaultMusicVolume = 0.6f;
    private const float DefaultAmbienceVolume = 0.5f;

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    private void Awake()
    {
        // Singleton enforcement
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure a single AudioListener exists on this GameObject
        if (GetComponent<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();

        Initialize();
    }

    /// <summary>
    /// Asynchronous initialization: loads the Audio Mixer from Resources,
    /// creates all AudioSource children, configures the SFX pool, and
    /// restores persisted volume settings. Transitions state through
    /// Uninitialized -> Initializing -> Ready (or Error).
    /// </summary>
    private async void Initialize()
    {
        _state = AudioSystemState.Initializing;
        OnStateChanged?.Invoke(_state);

        try
        {
            // --- 1. Load Audio Mixer from Resources (ADR-0013 exception to ADR-0002) ---
            if (_mixer == null)
                _mixer = Resources.Load<AudioMixer>("Audio/MasterMixer");

            if (_mixer == null)
            {
                EnterErrorState("Audio Mixer not found at Resources/Audio/MasterMixer. " +
                    "Create a MasterMixer asset in Resources/Audio/ with SFX, Music, " +
                    "Music_A, Music_B, and Ambience groups.");
                return;
            }

            // --- 2. Find mixer groups ---
            var sfxGroups = _mixer.FindMatchingGroups("SFX");
            var musicGroups = _mixer.FindMatchingGroups("Music");
            var ambienceGroups = _mixer.FindMatchingGroups("Ambience");

            if (sfxGroups.Length == 0 || musicGroups.Length == 0 || ambienceGroups.Length == 0)
            {
                EnterErrorState("Audio Mixer is missing required groups. " +
                    "Expected: SFX, Music, Ambience. " +
                    $"Found SFX={sfxGroups.Length}, Music={musicGroups.Length}, Ambience={ambienceGroups.Length}.");
                return;
            }

            _sfxGroup = sfxGroups[0];
            _musicGroup = musicGroups[0];
            _ambienceGroup = ambienceGroups[0];

            // --- 3. Find music sub-groups (Music_A, Music_B) ---
            var musicAGroups = _mixer.FindMatchingGroups("Music_A");
            var musicBGroups = _mixer.FindMatchingGroups("Music_B");
            var musicAGroup = musicAGroups.Length > 0 ? musicAGroups[0] : _musicGroup;
            var musicBGroup = musicBGroups.Length > 0 ? musicBGroups[0] : _musicGroup;

            // --- 4. Find snapshots for A/B crossfade ---
            _snapshotA = _mixer.FindSnapshot("Music_A_Active");
            _snapshotB = _mixer.FindSnapshot("Music_B_Active");

            if (_snapshotA == null || _snapshotB == null)
            {
                // Snapshots are optional — degrade gracefully to hard cuts
                Debug.LogWarning("[AudioManager] Music_A_Active or Music_B_Active snapshot not found. " +
                    "Music crossfade will use hard cuts instead of smooth transitions.");
            }

            // --- 5. Create Music sources (children of this GameObject) ---
            _musicSourceA = CreateAudioSource("MusicSourceA", musicAGroup);
            _musicSourceA.loop = true;
            _musicSourceB = CreateAudioSource("MusicSourceB", musicBGroup);
            _musicSourceB.loop = true;

            // --- 6. Create Ambience source ---
            _ambienceSource = CreateAudioSource("AmbienceSource", _ambienceGroup);
            _ambienceSource.loop = true;

            // --- 7. Create SFX pool (10 sources routed to SFX group) ---
            _sfxPool = new SFXPool(transform, _sfxGroup, maxSources: 10);

            // --- 8. Restore volumes from PlayerPrefs ---
            RestoreVolumes();

            // --- 9. Preload Shared_Audio assets ---
            await PreloadSharedAudioAsync();

            _state = AudioSystemState.Ready;
            OnStateChanged?.Invoke(_state);
        }
        catch (Exception ex)
        {
            EnterErrorState($"Audio system initialization failed: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            OnAudioError = null;
            OnStateChanged = null;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Pause/resume AudioListener on focus loss
        var listener = GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = hasFocus;
    }

    // =========================================================================
    // IAudioManager — PlaySFX
    // =========================================================================

    /// <summary>
    /// Plays a sound effect by clip key. Fire-and-forget — the caller does not
    /// block on audio loading. If the clip is already cached, plays immediately.
    /// If not cached, begins async loading and plays when ready.
    ///
    /// Does nothing when the audio system is not in Ready or LoadingChapterAudio state.
    /// </summary>
    /// <param name="clipKey">The Addressables key for the AudioClip.</param>
    /// <param name="worldPosition">
    /// Optional world-space position for spatialized SFX. When null, the sound
    /// is 2D (non-spatialized). When set, spatialBlend is set to 1.0 for 3D audio.
    /// </param>
    public void PlaySFX(string clipKey, Vector3? worldPosition = null)
    {
        if (_state != AudioSystemState.Ready && _state != AudioSystemState.LoadingChapterAudio)
            return;

        _ = PlaySFXDeferred(clipKey, worldPosition);
    }

    /// <summary>
    /// Loads the AudioClip (or retrieves from cache) and routes it to the SFX pool.
    /// </summary>
    private async Task PlaySFXDeferred(string clipKey, Vector3? worldPosition)
    {
        var clip = await LoadClipAsync(clipKey);
        if (clip != null)
            _sfxPool.Play(clip, priority: 1, worldPosition: worldPosition);
    }

    // =========================================================================
    // IAudioManager — PlayMusic / StopMusic
    // =========================================================================

    /// <summary>
    /// Starts playing music for the given clip key with a crossfade from the
    /// current track. If the same clip is already playing, the call is idempotent.
    /// Does nothing if the audio system is not Ready.
    /// </summary>
    /// <param name="clipKey">The Addressables key for the music AudioClip.</param>
    /// <param name="fadeTime">Crossfade duration in seconds (default 1.5s per ADR-0013).</param>
    public async void PlayMusic(string clipKey, float fadeTime = 1.5f)
    {
        if (_state != AudioSystemState.Ready)
            return;

        if (clipKey == _currentMusicKey)
            return; // Idempotent — same track already playing

        await CrossfadeMusicAsync(clipKey, fadeTime);
    }

    /// <summary>
    /// Internal crossfade implementation. Loads the new clip, starts it on the
    /// inactive track, transitions the mixer snapshot, and stops the old track.
    /// If snapshots are unavailable, performs a hard cut.
    /// </summary>
    private async Task CrossfadeMusicAsync(string clipKey, float fadeTime)
    {
        var clip = await LoadClipAsync(clipKey);
        if (clip == null)
        {
            OnAudioError?.Invoke($"Failed to load music clip: {clipKey}");
            return;
        }

        var targetTrack = _currentTrack == MusicTrack.A ? MusicTrack.B : MusicTrack.A;
        var targetSource = targetTrack == MusicTrack.A ? _musicSourceA : _musicSourceB;
        var targetSnapshot = targetTrack == MusicTrack.A ? _snapshotA : _snapshotB;

        // Set clip on target source and start playing
        targetSource.clip = clip;
        targetSource.Play();

        // Transition snapshot if available, otherwise hard cut
        if (targetSnapshot != null)
        {
            targetSnapshot.TransitionTo(fadeTime);
            // Wait for the crossfade to complete, then stop the old source
            await Task.Delay((int)(fadeTime * 1000));
        }

        // Stop the old source
        var oldSource = _currentTrack == MusicTrack.A ? _musicSourceA : _musicSourceB;
        oldSource.Stop();

        _currentTrack = targetTrack;
        _currentMusicKey = clipKey;
    }

    /// <summary>
    /// Stops the currently playing music with a fade-out. If no music is playing,
    /// this is a no-op.
    /// </summary>
    /// <param name="fadeTime">Fade-out duration in seconds (default 1.5s).</param>
    public void StopMusic(float fadeTime = 1.5f)
    {
        if (_currentMusicKey == null)
            return;

        var activeSource = _currentTrack == MusicTrack.A ? _musicSourceA : _musicSourceB;

        // If snapshots are available, transition to a silent state
        // For MVP, we stop directly — a silent snapshot would be ideal but
        // requires a third snapshot in the mixer. The stop itself is clean
        // because the mixer handles the AudioSource life cycle.
        activeSource.Stop();
        _currentMusicKey = null;
    }

    // =========================================================================
    // IAudioManager — PlayAmbience
    // =========================================================================

    /// <summary>
    /// Starts playing an ambient sound loop. If the same clip is already playing,
    /// the call is idempotent. Does nothing if the audio system is not Ready.
    /// </summary>
    /// <param name="clipKey">The Addressables key for the ambience AudioClip.</param>
    public async void PlayAmbience(string clipKey)
    {
        if (_state != AudioSystemState.Ready)
            return;

        if (clipKey == _currentAmbienceKey)
            return; // Idempotent

        await PlayAmbienceAsync(clipKey);
    }

    /// <summary>
    /// Loads the ambience clip and starts it on the dedicated ambience source.
    /// </summary>
    private async Task PlayAmbienceAsync(string clipKey)
    {
        var clip = await LoadClipAsync(clipKey);
        if (clip == null)
        {
            OnAudioError?.Invoke($"Failed to load ambience clip: {clipKey}");
            return;
        }

        _ambienceSource.clip = clip;
        _ambienceSource.Play();
        _currentAmbienceKey = clipKey;
    }

    // =========================================================================
    // IAudioManager — Volume Control
    // =========================================================================

    /// <summary>
    /// Sets the volume for a given audio channel. Converts the linear value
    /// (0.0-1.0) to dB and applies it to the Audio Mixer's exposed parameter.
    /// Persists to PlayerPrefs immediately.
    /// </summary>
    /// <param name="channel">Which audio channel to adjust.</param>
    /// <param name="linearValue">Volume in linear scale (0.0 = silent, 1.0 = full).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if channel is invalid.</exception>
    public void SetVolume(AudioChannel channel, float linearValue)
    {
        float dB = LinearToDb(linearValue);
        string param = GetMixerParameterName(channel);
        string ppKey = GetPlayerPrefsKey(channel);

        if (_mixer != null)
            _mixer.SetFloat(param, dB);

        PlayerPrefs.SetFloat(ppKey, linearValue);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Gets the persisted volume for a given audio channel. Reads from PlayerPrefs
    /// with a sensible default if no value has been saved yet.
    /// </summary>
    /// <param name="channel">Which audio channel to query.</param>
    /// <returns>Volume in linear scale (0.0-1.0).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if channel is invalid.</exception>
    public float GetVolume(AudioChannel channel)
    {
        string ppKey = GetPlayerPrefsKey(channel);
        float defaultValue = channel switch
        {
            AudioChannel.Master => DefaultMasterVolume,
            AudioChannel.SFX => DefaultSFXVolume,
            AudioChannel.Music => DefaultMusicVolume,
            AudioChannel.Ambience => DefaultAmbienceVolume,
            _ => 1.0f
        };
        return PlayerPrefs.GetFloat(ppKey, defaultValue);
    }

    // =========================================================================
    // IAudioManager — Chapter/Fragment Audio Preload
    // =========================================================================

    /// <summary>
    /// Preloads all audio assets for a chapter in the background via Addressables.
    /// Uses Addressables.LoadResourceLocationsAsync to find all assets in the
    /// "Audio_{chapterKey}" group and loads them into the clip cache.
    ///
    /// Idempotent — if the chapter is already loaded, returns immediately.
    /// Non-fatal on failure — logs a warning but does not enter error state.
    /// </summary>
    /// <param name="chapterKey">The chapter to preload audio for.</param>
    /// <returns>Task that completes when preload finishes (or fails).</returns>
    public async Task PreloadChapterAudioAsync(string chapterKey)
    {
        if (_loadedChapterKeys.Contains(chapterKey))
            return;

        var prevState = _state;
        _state = AudioSystemState.LoadingChapterAudio;
        OnStateChanged?.Invoke(_state);

        try
        {
            var label = $"Audio_{chapterKey}";
            var handle = Addressables.LoadResourceLocationsAsync(label, typeof(AudioClip));
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                foreach (var loc in handle.Result)
                {
                    if (_clipCache.ContainsKey(loc.PrimaryKey))
                        continue;

                    var loadHandle = Addressables.LoadAssetAsync<AudioClip>(loc.PrimaryKey);
                    await loadHandle.Task;

                    if (loadHandle.Status == AsyncOperationStatus.Succeeded && loadHandle.Result != null)
                    {
                        _clipCache[loc.PrimaryKey] = loadHandle.Result;
                    }
                }
            }

            _loadedChapterKeys.Add(chapterKey);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AudioManager] Chapter audio preload failed for '{chapterKey}': {ex.Message}");
            // Non-fatal — chapter will play with whatever loaded on demand
        }
        finally
        {
            _state = prevState;
            OnStateChanged?.Invoke(_state);
        }
    }

    /// <summary>
    /// Preloads audio clips for a specific fragment before it is displayed.
    /// Called by GameSceneManager during the Loading phase of fragment transitions.
    /// Uses LoadClipAsync for each key so already-cached clips return immediately.
    /// </summary>
    /// <param name="audioKeys">Array of audio clip keys from MemoryFragment.AudioKeys.</param>
    /// <returns>Task that completes when all clip loads are done.</returns>
    public async Task PreloadFragmentAudioAsync(string[] audioKeys)
    {
        if (audioKeys == null || audioKeys.Length == 0)
            return;

        try
        {
            var tasks = audioKeys.Select(key => LoadClipAsync(key));
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AudioManager] Fragment audio preload partially failed: {ex.Message}");
            // Non-fatal — fragment uses whatever loaded
        }
    }

    /// <summary>
    /// Unloads all cached audio assets for a chapter, releasing Addressables handles.
    /// In a production build this would call Addressables.Release for each asset.
    /// For MVP, removes the chapter from the loaded set so it can be re-preloaded.
    /// </summary>
    /// <param name="chapterKey">The chapter to unload audio for.</param>
    public void UnloadChapterAudio(string chapterKey)
    {
        if (!_loadedChapterKeys.Remove(chapterKey))
            return;

        // In production, we would track the Addressables handles and release them.
        // For MVP, we simply remove the chapter key — clips remain cached until
        // memory pressure forces Unity to unload them.
        // Full implementation with precise handle tracking:
        //   foreach (var key in _chapterAssetKeys[chapterKey])
        //   {
        //       if (_loadedHandles.TryGetValue(key, out var handle))
        //       {
        //           Addressables.Release(handle);
        //           _loadedHandles.Remove(key);
        //       }
        //       _clipCache.Remove(key);
        //   }

        Debug.Log($"[AudioManager] Unloaded chapter audio: {chapterKey}");
    }

    // =========================================================================
    // Formula Helpers — Linear / dB Conversion (ADR-0013)
    // =========================================================================

    /// <summary>
    /// Converts a linear volume value (0.0-1.0) to decibels for the Audio Mixer.
    /// Formula: dB = (linear <= 0.0001) ? -80 : log10(linear) * 20
    /// </summary>
    /// <param name="linear">Linear volume in range [0.0, 1.0].</param>
    /// <returns>dB value suitable for AudioMixer.SetFloat.</returns>
    public static float LinearToDb(float linear)
    {
        return linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
    }

    /// <summary>
    /// Converts a dB value back to linear scale (0.0-1.0).
    /// Formula: linear = 10^(dB / 20)
    /// </summary>
    /// <param name="db">Decibel value (typically -80 to 0).</param>
    /// <returns>Linear volume in range [0.0, 1.0].</returns>
    public static float DbToLinear(float db)
    {
        return Mathf.Pow(10f, db / 20f);
    }

    // =========================================================================
    // Internal Helpers
    // =========================================================================

    /// <summary>
    /// Creates a child GameObject with an AudioSource routed to the specified mixer group.
    /// </summary>
    private static AudioSource CreateAudioSource(string name, AudioMixerGroup group)
    {
        var go = new GameObject(name);
        var source = go.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = group;
        source.playOnAwake = false;
        return source;
    }

    /// <summary>
    /// Loads an AudioClip from Addressables (or cache). Deduplicates concurrent
    /// loads — if the same key is already being loaded, returns the existing Task.
    /// </summary>
    /// <param name="clipKey">The Addressables key for the AudioClip.</param>
    /// <returns>The loaded AudioClip, or null if loading failed.</returns>
    private async Task<AudioClip> LoadClipAsync(string clipKey)
    {
        if (string.IsNullOrEmpty(clipKey))
            return null;

        // Cache hit — return immediately
        if (_clipCache.TryGetValue(clipKey, out var cached))
            return cached;

        // Dedup: if already loading, await the existing Task
        if (_pendingLoads.TryGetValue(clipKey, out var pending))
            return await pending;

        var tcs = new TaskCompletionSource<AudioClip>();
        _pendingLoads[clipKey] = tcs.Task;

        try
        {
            var handle = Addressables.LoadAssetAsync<AudioClip>(clipKey);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                _clipCache[clipKey] = handle.Result;
                tcs.SetResult(handle.Result);
                return handle.Result;
            }

            Debug.LogWarning($"[AudioManager] Failed to load AudioClip: '{clipKey}' (status: {handle.Status})");
            tcs.SetResult(null);
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AudioManager] Exception loading AudioClip '{clipKey}': {ex.Message}");
            tcs.SetException(ex);
            OnAudioError?.Invoke($"Failed to load audio clip: {clipKey}");
            return null;
        }
        finally
        {
            _pendingLoads.Remove(clipKey);
        }
    }

    /// <summary>
    /// Preloads all audio assets labeled "Shared_Audio" in Addressables.
    /// These are UI sounds and other assets needed across all chapters.
    /// </summary>
    private async Task PreloadSharedAudioAsync()
    {
        try
        {
            var handle = Addressables.LoadResourceLocationsAsync("Shared_Audio", typeof(AudioClip));
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                var tasks = handle.Result.Select(loc => LoadClipAsync(loc.PrimaryKey));
                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AudioManager] Shared audio preload failed: {ex.Message}");
            // Non-fatal — shared audio loads on first use
        }
    }

    /// <summary>
    /// Restores all four volume channels from PlayerPrefs (or defaults).
    /// Called during initialization to apply persisted volume settings.
    /// </summary>
    private void RestoreVolumes()
    {
        foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
        {
            float savedValue = GetVolume(channel);
            SetVolumeInternal(channel, savedValue);
        }
    }

    /// <summary>
    /// Sets the volume without writing to PlayerPrefs (used during restore).
    /// </summary>
    private void SetVolumeInternal(AudioChannel channel, float linearValue)
    {
        float dB = LinearToDb(linearValue);
        string param = GetMixerParameterName(channel);
        if (_mixer != null)
            _mixer.SetFloat(param, dB);
    }

    /// <summary>
    /// Maps an AudioChannel to the corresponding Audio Mixer exposed parameter name.
    /// </summary>
    private static string GetMixerParameterName(AudioChannel channel)
    {
        return channel switch
        {
            AudioChannel.Master => PARAM_MASTER,
            AudioChannel.SFX => PARAM_SFX,
            AudioChannel.Music => PARAM_MUSIC,
            AudioChannel.Ambience => PARAM_AMBIENCE,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), $"Unknown audio channel: {channel}")
        };
    }

    /// <summary>
    /// Maps an AudioChannel to the corresponding PlayerPrefs key.
    /// </summary>
    private static string GetPlayerPrefsKey(AudioChannel channel)
    {
        return channel switch
        {
            AudioChannel.Master => PP_MASTER,
            AudioChannel.SFX => PP_SFX,
            AudioChannel.Music => PP_MUSIC,
            AudioChannel.Ambience => PP_AMBIENCE,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), $"Unknown audio channel: {channel}")
        };
    }

    /// <summary>
    /// Enters the Error state, fires OnAudioError and OnStateChanged events,
    /// and logs the error message. Playback methods will no-op while in Error state.
    /// </summary>
    private void EnterErrorState(string message)
    {
        _state = AudioSystemState.Error;
        OnStateChanged?.Invoke(_state);
        OnAudioError?.Invoke(message);
        Debug.LogError($"[AudioManager] {message}");
    }

    // =========================================================================
    // Testability — Internal Accessors
    // =========================================================================

    /// <summary>Test-only: current audio system state.</summary>
    internal AudioSystemState _testState => _state;

    /// <summary>Test-only: currently playing music key.</summary>
    internal string _testCurrentMusicKey => _currentMusicKey;

    /// <summary>Test-only: currently playing ambience key.</summary>
    internal string _testCurrentAmbienceKey => _currentAmbienceKey;

    /// <summary>Test-only: current active music track.</summary>
    internal MusicTrack _testCurrentTrack => _currentTrack;

    /// <summary>Test-only: set of loaded chapter keys.</summary>
    internal HashSet<string> _testLoadedChapterKeys => _loadedChapterKeys;

    /// <summary>Test-only: number of cached clips.</summary>
    internal int _testClipCacheCount => _clipCache.Count;

    /// <summary>Test-only: number of pending clip loads.</summary>
    internal int _testPendingLoadsCount => _pendingLoads.Count;

    /// <summary>Test-only: inject a loaded clip into the cache (bypasses Addressables).</summary>
    internal void _testInjectClip(string key, AudioClip clip)
    {
        _clipCache[key] = clip;
    }

    /// <summary>Test-only: manually set the system state.</summary>
    internal void _testSetState(AudioSystemState state)
    {
        _state = state;
    }

    /// <summary>Test-only: inject a mixer for volume tests (bypasses Resources).</summary>
    internal void _testInjectMixer(AudioMixer mixer)
    {
        _mixer = mixer;
    }

    /// <summary>Test-only: clear all internal state for clean test isolation.</summary>
    internal void _testReset()
    {
        _clipCache.Clear();
        _pendingLoads.Clear();
        _loadedChapterKeys.Clear();
        _currentMusicKey = null;
        _currentAmbienceKey = null;
        _currentTrack = MusicTrack.A;
        _state = AudioSystemState.Ready;
    }
}
