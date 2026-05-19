using UnityEngine;

/// <summary>
/// Priority-based SFX AudioSource pool implementing ADR-0013 audio architecture.
///
/// Manages a fixed-size pool of AudioSource components (default 10) for playing
/// short-lived sound effects. When the pool is full, lower-priority sounds are
/// preempted to make room for higher-priority ones. The pool does NOT handle
/// asset loading — AudioManager loads AudioClips via Addressables and passes
/// the loaded clip to Play().
///
/// Ducking is expected to be configured in the Audio Mixer itself via a Send
/// from the SFX group to the Duck Volume effect on Music and Ambience groups.
/// This class does not perform runtime ducking — it is a mixer setup requirement.
///
/// Thread safety: Not thread-safe. All methods must be called on the main thread.
///
/// Implements: Epic AudioSystem (#3), Story 002 (SFX Playback).
/// </summary>
public class SFXPool
{
    /// <summary>Maximum number of simultaneous SFX sources.</summary>
    private readonly int _maxSources;

    /// <summary>Pool of AudioSource components.</summary>
    private readonly AudioSource[] _sources;

    /// <summary>Time.time when each source started playing (for preemption tiebreaking).</summary>
    private readonly float[] _startTimes;

    /// <summary>
    /// Priority level for each source. 0 = free/available, 1+ = playing with that priority.
    /// Higher numbers = higher priority. Preemption only steals from lower-priority sources.
    /// </summary>
    private readonly int[] _priorities;

    /// <summary>
    /// Creates a new SFX pool with the specified number of AudioSource children.
    /// All sources are created immediately as children of the given parent transform
    /// and routed to the specified AudioMixerGroup.
    /// </summary>
    /// <param name="parent">The parent transform (usually AudioManager's transform).</param>
    /// <param name="sfxGroup">The Audio Mixer SFX group to route all sources to.</param>
    /// <param name="maxSources">Maximum number of simultaneous SFX sources (default 10).</param>
    public SFXPool(Transform parent, AudioMixerGroup sfxGroup, int maxSources = 10)
    {
        _maxSources = Mathf.Max(1, maxSources);
        _sources = new AudioSource[_maxSources];
        _startTimes = new float[_maxSources];
        _priorities = new int[_maxSources];

        for (int i = 0; i < _maxSources; i++)
        {
            var go = new GameObject($"SFX_Source_{i}");
            go.transform.SetParent(parent);
            _sources[i] = go.AddComponent<AudioSource>();
            _sources[i].outputAudioMixerGroup = sfxGroup;
            _sources[i].playOnAwake = false;
            _priorities[i] = 0; // Free
        }
    }

    /// <summary>
    /// Plays an AudioClip through the pool. If a free source is available, uses it.
    /// If the pool is full, attempts to preempt a lower-priority source. If all
    /// sources have equal or higher priority, the sound is discarded (not played).
    /// </summary>
    /// <param name="clip">The AudioClip to play (must not be null).</param>
    /// <param name="priority">
    /// Priority level (1+). Higher values take precedence. A new sound will only
    /// preempt an existing one if its priority is strictly greater.
    /// </param>
    /// <param name="worldPosition">
    /// Optional world-space position for 3D spatialization. When null, the sound
    /// is 2D (spatialBlend = 0). When set, spatialBlend = 1 and the source is
    /// positioned at the given world coordinate.
    /// </param>
    public void Play(AudioClip clip, int priority = 1, Vector3? worldPosition = null)
    {
        if (clip == null)
            return;

        // --- Step 1: Find a free source ---
        for (int i = 0; i < _sources.Length; i++)
        {
            if (_priorities[i] == 0 || !_sources[i].isPlaying)
            {
                PlayOnSource(i, clip, priority, worldPosition);
                return;
            }
        }

        // --- Step 2: Pool full — find a preemption victim ---
        int victimIdx = FindPreemptionVictim(priority);
        if (victimIdx >= 0)
        {
            _sources[victimIdx].Stop();
            PlayOnSource(victimIdx, clip, priority, worldPosition);
        }
        // else: new sound priority too low, discard (do not play)
    }

    /// <summary>
    /// Finds the best source to preempt given the incoming priority level.
    /// Preemption order:
    ///   1. Source nearest to completion (remaining time &lt; 0.1s) — least disruption
    ///   2. Lowest-priority source (only if incoming priority is higher)
    ///   3. Earliest started source (tiebreaker)
    /// Returns -1 if no suitable victim exists.
    /// </summary>
    private int FindPreemptionVictim(int incomingPriority)
    {
        // --- Priority 1: Source nearest to completion (remaining < 0.1s) ---
        for (int i = 0; i < _sources.Length; i++)
        {
            if (_sources[i].isPlaying && _sources[i].clip != null)
            {
                float remaining = _sources[i].clip.length - _sources[i].time;
                if (remaining < 0.1f)
                    return i;
            }
        }

        // --- Priority 2: Lowest priority source (must be < incoming) ---
        int minPriority = int.MaxValue;
        int minIdx = -1;

        for (int i = 0; i < _priorities.Length; i++)
        {
            if (_priorities[i] > 0 && _priorities[i] < incomingPriority && _priorities[i] < minPriority)
            {
                minPriority = _priorities[i];
                minIdx = i;
            }
        }

        if (minIdx >= 0)
            return minIdx;

        // --- Priority 3: Earliest started (tiebreaker, only if preemptable) ---
        float earliestTime = float.MaxValue;
        int earliestIdx = -1;

        for (int i = 0; i < _startTimes.Length; i++)
        {
            if (_priorities[i] > 0 && _priorities[i] < incomingPriority && _startTimes[i] < earliestTime)
            {
                earliestTime = _startTimes[i];
                earliestIdx = i;
            }
        }

        return earliestIdx;
    }

    /// <summary>
    /// Configures and plays the clip on the specified pool source.
    /// </summary>
    private void PlayOnSource(int index, AudioClip clip, int priority, Vector3? worldPosition)
    {
        var source = _sources[index];
        source.clip = clip;
        source.spatialBlend = worldPosition.HasValue ? 1.0f : 0.0f;

        if (worldPosition.HasValue)
            source.transform.position = worldPosition.Value;

        source.Play();
        _startTimes[index] = Time.time;
        _priorities[index] = priority;
    }

    // =========================================================================
    // Testability — Internal Accessors
    // =========================================================================

    /// <summary>Test-only: number of sources in the pool.</summary>
    internal int _testPoolSize => _maxSources;

    /// <summary>Test-only: number of currently free sources.</summary>
    internal int _testFreeCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _priorities.Length; i++)
            {
                if (_priorities[i] == 0 || !_sources[i].isPlaying)
                    count++;
            }
            return count;
        }
    }

    /// <summary>Test-only: number of currently active (playing) sources.</summary>
    internal int _testActiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _priorities.Length; i++)
            {
                if (_priorities[i] > 0 && _sources[i].isPlaying)
                    count++;
            }
            return count;
        }
    }

    /// <summary>Test-only: get priority of a specific source index.</summary>
    internal int _testGetPriority(int index)
    {
        if (index < 0 || index >= _priorities.Length)
            return -1;
        return _priorities[index];
    }

    /// <summary>Test-only: set priority of a specific source (for preemption tests).</summary>
    internal void _testSetPriority(int index, int priority)
    {
        if (index >= 0 && index < _priorities.Length)
            _priorities[index] = priority;
    }

    /// <summary>Test-only: set a source as playing (for preemption tests).</summary>
    internal void _testSetPlaying(int index, bool playing)
    {
        if (index >= 0 && index < _sources.Length)
        {
            if (!playing)
            {
                _sources[index].Stop();
                _priorities[index] = 0;
            }
        }
    }

    /// <summary>Test-only: expose sources array for direct inspection.</summary>
    internal AudioSource[] _testSources => _sources;

    /// <summary>Test-only: expose priorities array for preemption logic testing.</summary>
    internal int[] _testPriorities => _priorities;

    /// <summary>Test-only: expose start times for preemption logic testing.</summary>
    internal float[] _testStartTimes => _startTimes;
}
