using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Save/Load state machine guard. Operations are rejected when state is not Idle.
/// </summary>
public enum SaveState
{
    Idle,
    Saving,
    Loading,
    Error
}

/// <summary>
/// Lightweight metadata for a single save slot — read without deserializing
/// the full SaveData payload.
/// </summary>
public struct SlotMetadata
{
    public string Timestamp;
    public int PlayTimeSeconds;
    public string CurrentChapterKey;

    public static readonly SlotMetadata Empty = new()
    {
        Timestamp = "",
        PlayTimeSeconds = 0,
        CurrentChapterKey = ""
    };

    public bool IsEmpty => string.IsNullOrEmpty(Timestamp);
}

/// <summary>
/// Thin file I/O abstraction so SaveManager is unit-testable without touching
/// the real filesystem. Production implementation delegates to System.IO.
/// </summary>
public interface IFileAccess
{
    Task WriteAllTextAsync(string path, string contents);
    string ReadAllText(string path);
    void Move(string source, string dest, bool overwrite);
    bool Exists(string path);
    void CreateDirectory(string path);
    void Delete(string path);
}

/// <summary>
/// Production implementation of <see cref="IFileAccess"/> — delegates to
/// <see cref="System.IO.File"/> and <see cref="System.IO.Directory"/>.
/// </summary>
public class PhysicalFileAccess : IFileAccess
{
    public Task WriteAllTextAsync(string path, string contents) =>
        File.WriteAllTextAsync(path, contents);

    public string ReadAllText(string path) =>
        File.ReadAllText(path);

    public void Move(string source, string dest, bool overwrite) =>
        File.Move(source, dest, overwrite);

    public bool Exists(string path) =>
        File.Exists(path);

    public void CreateDirectory(string path) =>
        Directory.CreateDirectory(path);

    public void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

/// <summary>
/// Handles atomic file I/O for save game persistence. Wraps
/// <see cref="SaveChecksum"/> for integrity and enforces single-operation
/// concurrency via <see cref="SaveState"/> guard.
///
/// All public operations are async and return <see cref="Task"/>.
/// </summary>
public class SaveManager
{
    private readonly string _saveDirectory;
    private readonly IFileAccess _file;
    private SaveState _currentState = SaveState.Idle;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SaveState CurrentState => _currentState;

    /// <summary>
    /// Creates a SaveManager targeting the given directory.
    /// </summary>
    /// <param name="saveDirectory">Absolute path to the saves folder.</param>
    /// <param name="fileAccess">File I/O abstraction (use <see cref="PhysicalFileAccess"/> in production).</param>
    public SaveManager(string saveDirectory, IFileAccess fileAccess)
    {
        _saveDirectory = saveDirectory ?? throw new ArgumentNullException(nameof(saveDirectory));
        _file = fileAccess ?? throw new ArgumentNullException(nameof(fileAccess));
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Serializes <paramref name="saveData"/> with SHA-256 checksum and writes
    /// atomically to <c>[slotId].sav</c>. Rejects concurrent operations.
    /// </summary>
    /// <exception cref="InvalidOperationException">Another save/load is in progress.</exception>
    /// <exception cref="SaveFileException">Disk full, permission denied, or other I/O failure.</exception>
    public async Task SaveAsync(string slotId, SaveData saveData)
    {
        if (_currentState != SaveState.Idle)
        {
            Debug.LogWarning($"[SaveManager] Save/Load in progress (state={_currentState}), ignoring SaveAsync(\"{slotId}\")");
            return;
        }

        _currentState = SaveState.Saving;

        try
        {
            // 1. Compute checksum
            saveData.Checksum = SaveChecksum.ComputeChecksum(saveData);

            // 2. Serialize
            string json = JsonSerializer.Serialize(saveData, _jsonOptions);
            string tmpPath = GetSlotPath(slotId) + ".tmp";
            string finalPath = GetSlotPath(slotId);

            // 3. Atomic write
            _file.CreateDirectory(_saveDirectory);

            // Clean up any stale .tmp from a previous crash
            if (_file.Exists(tmpPath))
                _file.Delete(tmpPath);

            await _file.WriteAllTextAsync(tmpPath, json);
            _file.Move(tmpPath, finalPath, overwrite: true);
        }
        catch (IOException ex)
        {
            _currentState = SaveState.Error;
            CleanupTempFile(slotId);
            Debug.LogError($"[SaveManager] Save failed (IO): {ex.Message}");
            throw new SaveFileException("disk_full_or_permission", slotId, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _currentState = SaveState.Error;
            CleanupTempFile(slotId);
            Debug.LogError($"[SaveManager] Save failed (permission): {ex.Message}");
            throw new SaveFileException("permission_denied", slotId, ex);
        }
        finally
        {
            // On success, reset to Idle. On error, _currentState is already Error — keep it.
            if (_currentState == SaveState.Saving)
                _currentState = SaveState.Idle;
        }
    }

    /// <summary>
    /// Reads and deserializes the save file for the given slot without
    /// checksum validation. Returns <c>null</c> if the file does not exist.
    ///
    /// Callers should run <see cref="SaveChecksum.ValidateChecksum"/> on the result.
    /// </summary>
    public Task<SaveData?> LoadAsync(string slotId)
    {
        if (_currentState != SaveState.Idle)
        {
            Debug.LogWarning($"[SaveManager] Save/Load in progress (state={_currentState}), ignoring LoadAsync(\"{slotId}\")");
            return Task.FromResult<SaveData?>(null);
        }

        _currentState = SaveState.Loading;

        try
        {
            string path = GetSlotPath(slotId);
            if (!_file.Exists(path))
            {
                _currentState = SaveState.Idle;
                return Task.FromResult<SaveData?>(null);
            }

            string json = _file.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SaveData>(json, _jsonOptions);
            _currentState = SaveState.Idle;
            return Task.FromResult<SaveData?>(data);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _currentState = SaveState.Error;
            Debug.LogError($"[SaveManager] Load failed: {ex.Message}");
            throw new SaveFileException("read_failed", slotId, ex);
        }
    }

    /// <summary>
    /// Resets the state machine from Error back to Idle so the next operation
    /// is not rejected. Call after the caller has handled the error.
    /// </summary>
    public void ClearError()
    {
        if (_currentState == SaveState.Error)
            _currentState = SaveState.Idle;
    }

    /// <summary>
    /// Fast metadata scan — reads only Timestamp, PlayTimeSeconds, and
    /// CurrentChapterKey from the JSON file without deserializing the full
    /// SaveData payload.
    /// </summary>
    /// <returns><see cref="SlotMetadata.Empty"/> if the file does not exist.</returns>
    public SlotMetadata GetSlotMetadata(string slotId)
    {
        string path = GetSlotPath(slotId);
        if (!_file.Exists(path))
            return SlotMetadata.Empty;

        try
        {
            string json = _file.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new SlotMetadata
            {
                Timestamp = root.TryGetProperty("timestamp", out var ts) ? ts.GetString() ?? "" : "",
                PlayTimeSeconds = root.TryGetProperty("playTimeSeconds", out var pts) ? pts.GetInt32() : 0,
                CurrentChapterKey = root.TryGetProperty("currentChapterKey", out var cck) ? cck.GetString() ?? "" : ""
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Debug.LogWarning($"[SaveManager] Failed to read metadata for slot \"{slotId}\": {ex.Message}");
            return SlotMetadata.Empty;
        }
    }

    /// <summary>
    /// Returns true if any of the three known save slots has a .sav file.
    /// </summary>
    public bool HasAnySave()
    {
        return _file.Exists(GetSlotPath("save_01"))
            || _file.Exists(GetSlotPath("save_02"))
            || _file.Exists(GetSlotPath("auto_save"));
    }

    /// <summary>
    /// Deletes the save file for the given slot. No-op if the file does not exist.
    /// Rejected if a save/load operation is in progress.
    /// </summary>
    public void DeleteSave(string slotId)
    {
        if (_currentState != SaveState.Idle)
        {
            Debug.LogWarning($"[SaveManager] Save/Load in progress (state={_currentState}), ignoring DeleteSave(\"{slotId}\")");
            return;
        }

        string path = GetSlotPath(slotId);
        if (_file.Exists(path))
            _file.Delete(path);
    }

    // =========================================================================
    // Internals
    // =========================================================================

    private string GetSlotPath(string slotId) =>
        Path.Combine(_saveDirectory, $"{slotId}.sav");

    private void CleanupTempFile(string slotId)
    {
        string tmpPath = GetSlotPath(slotId) + ".tmp";
        try
        {
            if (_file.Exists(tmpPath))
                _file.Delete(tmpPath);
        }
        catch
        {
            // Best-effort cleanup — don't mask the original exception
        }
    }
}

/// <summary>
/// Thrown when a save/load file operation fails due to I/O error
/// (disk full, permission denied, read failure).
/// </summary>
public class SaveFileException : Exception
{
    public string ErrorCode { get; }
    public string SlotId { get; }

    public SaveFileException(string errorCode, string slotId, Exception inner)
        : base($"Save file error [{errorCode}] on slot \"{slotId}\": {inner.Message}", inner)
    {
        ErrorCode = errorCode;
        SlotId = slotId;
    }
}
