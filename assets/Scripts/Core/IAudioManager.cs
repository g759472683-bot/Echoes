using System.Threading.Tasks;

/// <summary>
/// Interface for audio preloading during fragment transitions.
/// Implemented by AudioManager (ADR-0013). SceneManager calls PreloadFragmentAudioAsync
/// during the Loading phase so audio is ready when the fade-in completes.
/// This minimal stub contains only the member SceneManager actually calls.
/// </summary>
public interface IAudioManager
{
    /// <summary>
    /// Preloads audio clips for a fragment before it is displayed.
    /// Called during the Loading phase of TransitionToFragmentAsync.
    /// Fire-and-forget on failure — audio errors do not block the transition.
    /// </summary>
    /// <param name="audioKeys">Array of audio clip keys from MemoryFragment.AudioKeys.</param>
    /// <returns>Task that completes when all audio clips are loaded.</returns>
    Task PreloadFragmentAudioAsync(string[] audioKeys);

    /// <summary>
    /// Preloads all audio assets for a chapter in the background.
    /// Called in parallel with IDataManager.PreloadChapterAsync via Task.WhenAll
    /// during chapter preload (ADR-0004). Fire-and-forget on failure.
    /// </summary>
    /// <param name="chapterKey">The chapter to preload audio for.</param>
    /// <returns>Task that completes when chapter audio preload finishes.</returns>
    Task PreloadChapterAudioAsync(string chapterKey);

    /// <summary>
    /// Stops currently playing music with a fade-out over the specified duration.
    /// Used during chapter transitions for music crossfade (ADR-0004).
    /// </summary>
    /// <param name="fadeTime">Fade-out duration in seconds.</param>
    void StopMusic(float fadeTime);

    /// <summary>
    /// Starts playing the music for the specified chapter with a fade-in.
    /// Called after new chapter assets are loaded during a chapter transition.
    /// </summary>
    /// <param name="chapterKey">The chapter whose music to play.</param>
    /// <param name="fadeTime">Fade-in duration in seconds.</param>
    void PlayMusic(string chapterKey, float fadeTime);

    /// <summary>
    /// Unloads all audio assets for a chapter, releasing Addressables handles.
    /// Called during chapter transitions when leaving a chapter.
    /// </summary>
    /// <param name="chapterKey">The chapter to unload audio for.</param>
    void UnloadChapterAudio(string chapterKey);
}
