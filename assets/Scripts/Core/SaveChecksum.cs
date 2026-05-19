using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Static utility for SHA-256 checksum computation and validation on
/// <see cref="SaveData"/> structs. All methods are pure functions —
/// no state, no Unity dependencies, fully testable.
///
/// Usage:
///   var data = new SaveData { Version = 1, ... };
///   data.Checksum = SaveChecksum.ComputeChecksum(data);
///   // ... persist ...
///   SaveChecksum.ValidateChecksum(loadedData);  // throws on mismatch
/// </summary>
public static class SaveChecksum
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Computes the SHA-256 hex digest of all <see cref="SaveData"/> fields
    /// EXCEPT <see cref="SaveData.Checksum"/> itself. The Checksum field is
    /// temporarily cleared before serialization so it does not affect the hash.
    ///
    /// Same input always produces the same output (deterministic).
    /// </summary>
    /// <param name="data">The SaveData to hash. Passed by value (struct copy) — original is not modified.</param>
    /// <returns>Lowercase hex string of the SHA-256 digest.</returns>
    public static string ComputeChecksum(SaveData data)
    {
        // Struct is passed by value — we can mutate the copy safely
        data.Checksum = null;

        string json = JsonSerializer.Serialize(data, _jsonOptions);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(jsonBytes);

        return ToHexString(hashBytes);
    }

    /// <summary>
    /// Validates that the embedded <see cref="SaveData.Checksum"/> matches
    /// a freshly-computed SHA-256 hash of all other fields.
    ///
    /// Throws <see cref="SaveCorruptedException"/> on mismatch, null Checksum,
    /// or empty Checksum — callers must NOT attempt partial recovery.
    /// </summary>
    /// <param name="data">The SaveData to validate.</param>
    /// <exception cref="SaveCorruptedException">Checksum is null, empty, or does not match.</exception>
    public static void ValidateChecksum(SaveData data)
    {
        if (string.IsNullOrEmpty(data.Checksum))
            throw new SaveCorruptedException(
                $"Save file is missing its integrity checksum. " +
                $"The file may have been truncated or manually edited.");

        string expected = ComputeChecksum(data);

        if (!string.Equals(expected, data.Checksum, StringComparison.OrdinalIgnoreCase))
            throw new SaveCorruptedException(
                $"Checksum mismatch: expected {expected}, got {data.Checksum}. " +
                $"The save file may be corrupted or tampered.");
    }

    private static string ToHexString(byte[] bytes)
    {
        // Avoid per-byte allocation from BitConverter.ToString().Replace("-","").
        // This is called on save/load, not in hot paths, but still cleaner.
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.AppendFormat("{0:x2}", b);
        return sb.ToString();
    }
}
