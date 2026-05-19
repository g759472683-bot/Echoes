using System;
using System.Collections.Generic;
using System.Text.Json;
using NUnit.Framework;

/// <summary>
/// Unit tests for SaveData struct serialization and SHA-256 checksum validation.
/// Covers all 4 acceptance criteria from Story 001 (SaveData Structure + SHA-256 Checksum).
///
/// These are pure C# tests — no Unity, no MonoBehaviour, no Addressables required.
/// </summary>
public class checksum_test
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private static SaveData CreatePopulated()
    {
        return new SaveData
        {
            Version = 1,
            Timestamp = "2026-05-17T12:00:00Z",
            LocaleCode = "zh-Hans",
            PlayTimeSeconds = 3600,
            CurrentChapterKey = "ch01",
            CurrentFragmentId = "frag_03",
            CurrentFragmentIndex = 2,
            CompletedChapters = new[] { "ch01" },
            UnlockedChapters = new[] { "ch01", "ch02" },
            ChangeOverlay = new Dictionary<string, string>
            {
                { "frag_01:choice_01", "{\"type\":\"ToggleVisualLayer\",\"layerId\":\"ink_wash\"}" },
                { "frag_02:choice_02", "{\"type\":\"SetFlag\",\"flagId\":\"found_key\"}" }
            },
            CrossChapterFlags = new Dictionary<string, bool>
            {
                { "met_elder", true },
                { "found_secret", false }
            },
            MasterVolume = 0.8f,
            SFXVolume = 0.7f,
            MusicVolume = 0.6f,
            AmbienceVolume = 0.5f,
            TriggeredEndingConditionIds = new[] { "end_001", "end_003" }
            // Checksum is set by individual tests
        };
    }

    // =========================================================================
    // AC-1: SaveData struct contains all required fields + serialization round-trip
    // =========================================================================

    [Test]
    public void test_savedata_serialize_deserialize_round_trip_preserves_all_fields()
    {
        // Arrange
        var original = CreatePopulated();
        original.Checksum = "abc123";

        // Act — Checksum is [JsonIgnore], so it should NOT appear in JSON
        string json = JsonSerializer.Serialize(original, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        // Assert — Checksum was excluded
        Assert.That(json, Does.Not.Contain("checksum"));
        Assert.That(json, Does.Not.Contain("abc123"));

        // Deserialize back
        var restored = JsonSerializer.Deserialize<SaveData>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.That(restored.Version, Is.EqualTo(1));
        Assert.That(restored.Timestamp, Is.EqualTo("2026-05-17T12:00:00Z"));
        Assert.That(restored.LocaleCode, Is.EqualTo("zh-Hans"));
        Assert.That(restored.PlayTimeSeconds, Is.EqualTo(3600));
        Assert.That(restored.CurrentChapterKey, Is.EqualTo("ch01"));
        Assert.That(restored.CurrentFragmentId, Is.EqualTo("frag_03"));
        Assert.That(restored.CurrentFragmentIndex, Is.EqualTo(2));
        Assert.That(restored.CompletedChapters, Is.EquivalentTo(new[] { "ch01" }));
        Assert.That(restored.UnlockedChapters, Is.EquivalentTo(new[] { "ch01", "ch02" }));
        Assert.That(restored.ChangeOverlay, Is.Not.Null);
        Assert.That(restored.ChangeOverlay.Count, Is.EqualTo(2));
        Assert.That(restored.ChangeOverlay["frag_01:choice_01"], Is.EqualTo("{\"type\":\"ToggleVisualLayer\",\"layerId\":\"ink_wash\"}"));
        Assert.That(restored.CrossChapterFlags["met_elder"], Is.True);
        Assert.That(restored.CrossChapterFlags["found_secret"], Is.False);
        Assert.That(restored.MasterVolume, Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(restored.SFXVolume, Is.EqualTo(0.7f).Within(0.001f));
        Assert.That(restored.MusicVolume, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(restored.AmbienceVolume, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(restored.TriggeredEndingConditionIds, Is.EquivalentTo(new[] { "end_001", "end_003" }));
        // Checksum should be default (null) after deserialization since [JsonIgnore]
        Assert.That(restored.Checksum, Is.Null);
    }

    [Test]
    public void test_savedata_empty_dictionary_serializes_as_empty_object()
    {
        // Arrange
        var data = new SaveData
        {
            Version = 1,
            Timestamp = "2026-05-17T00:00:00Z",
            ChangeOverlay = new Dictionary<string, string>(),
            CrossChapterFlags = new Dictionary<string, bool>()
        };

        // Act
        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert — empty dictionaries should serialize
        Assert.That(json, Does.Contain("\"changeOverlay\":{}"));
        Assert.That(json, Does.Contain("\"crossChapterFlags\":{}"));

        var restored = JsonSerializer.Deserialize<SaveData>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.That(restored.ChangeOverlay, Is.Not.Null);
        Assert.That(restored.ChangeOverlay.Count, Is.EqualTo(0));
        Assert.That(restored.CrossChapterFlags, Is.Not.Null);
        Assert.That(restored.CrossChapterFlags.Count, Is.EqualTo(0));
    }

    [Test]
    public void test_savedata_null_arrays_serialize_as_null()
    {
        // Arrange
        var data = new SaveData
        {
            Version = 1,
            Timestamp = "2026-05-17T00:00:00Z",
            CompletedChapters = null,
            TriggeredEndingConditionIds = null
        };

        // Act
        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert — null arrays serialize as null
        Assert.That(json, Does.Contain("\"completedChapters\":null"));
        Assert.That(json, Does.Contain("\"triggeredEndingConditionIds\":null"));

        var restored = JsonSerializer.Deserialize<SaveData>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.That(restored.CompletedChapters, Is.Null);
        Assert.That(restored.TriggeredEndingConditionIds, Is.Null);
    }

    // =========================================================================
    // AC-2: SHA-256 checksum consistency
    // =========================================================================

    [Test]
    public void test_compute_checksum_same_input_produces_same_output()
    {
        // Arrange
        var data = CreatePopulated();

        // Act
        string hash1 = SaveChecksum.ComputeChecksum(data);
        string hash2 = SaveChecksum.ComputeChecksum(data);

        // Assert
        Assert.That(hash1, Is.EqualTo(hash2));
        Assert.That(hash1.Length, Is.EqualTo(64)); // SHA-256 = 32 bytes = 64 hex chars
    }

    [Test]
    public void test_compute_checksum_field_change_produces_different_hash()
    {
        // Arrange
        var data1 = CreatePopulated();
        var data2 = CreatePopulated();
        data2.PlayTimeSeconds = 9999; // change one field

        // Act
        string hash1 = SaveChecksum.ComputeChecksum(data1);
        string hash2 = SaveChecksum.ComputeChecksum(data2);

        // Assert
        Assert.That(hash1, Is.Not.EqualTo(hash2));
    }

    [Test]
    public void test_compute_checksum_checksum_field_excluded_from_hash()
    {
        // Arrange
        var data = CreatePopulated();
        string hashWithoutChecksum = SaveChecksum.ComputeChecksum(data);

        data.Checksum = "abc123def456";
        string hashWithChecksum = SaveChecksum.ComputeChecksum(data);

        // Act & Assert — Checksum field is nulled before hashing, so hashes must be equal
        Assert.That(hashWithChecksum, Is.EqualTo(hashWithoutChecksum));
    }

    [Test]
    public void test_compute_checksum_hex_format_is_lowercase()
    {
        // Arrange
        var data = CreatePopulated();

        // Act
        string hash = SaveChecksum.ComputeChecksum(data);

        // Assert — all hex digits should be lowercase
        Assert.That(hash, Is.EqualTo(hash.ToLowerInvariant()));
        // Should only contain hex characters
        Assert.That(hash, Does.Match("^[0-9a-f]{64}$"));
    }

    // =========================================================================
    // AC-3: ValidateChecksum throws SaveCorruptedException on mismatch
    // =========================================================================

    [Test]
    public void test_validate_checksum_valid_save_passes()
    {
        // Arrange
        var data = CreatePopulated();
        data.Checksum = SaveChecksum.ComputeChecksum(data);

        // Act & Assert — no exception
        Assert.DoesNotThrow(() => SaveChecksum.ValidateChecksum(data));
    }

    [Test]
    public void test_validate_checksum_mismatch_throws_save_corrupted_exception()
    {
        // Arrange
        var data = CreatePopulated();
        data.Checksum = "0000000000000000000000000000000000000000000000000000000000000000"; // wrong hash

        // Act & Assert
        var ex = Assert.Throws<SaveCorruptedException>(() =>
            SaveChecksum.ValidateChecksum(data));

        Assert.That(ex.Message, Does.Contain("Checksum mismatch"));
        Assert.That(ex.Message, Does.Contain("expected"));
        Assert.That(ex.Message, Does.Contain("got"));
    }

    [Test]
    public void test_validate_checksum_null_throws_save_corrupted_exception()
    {
        // Arrange
        var data = CreatePopulated();
        data.Checksum = null;

        // Act & Assert
        var ex = Assert.Throws<SaveCorruptedException>(() =>
            SaveChecksum.ValidateChecksum(data));

        Assert.That(ex.Message, Does.Contain("missing"));
    }

    [Test]
    public void test_validate_checksum_empty_string_throws_save_corrupted_exception()
    {
        // Arrange
        var data = CreatePopulated();
        data.Checksum = "";

        // Act & Assert
        var ex = Assert.Throws<SaveCorruptedException>(() =>
            SaveChecksum.ValidateChecksum(data));

        Assert.That(ex.Message, Does.Contain("missing"));
    }

    [Test]
    public void test_validate_checksum_tampered_content_detected()
    {
        // Arrange
        var data = CreatePopulated();
        data.Checksum = SaveChecksum.ComputeChecksum(data);

        // Tamper with content after checksum was computed
        data.MasterVolume = 0.1f;

        // Act & Assert
        var ex = Assert.Throws<SaveCorruptedException>(() =>
            SaveChecksum.ValidateChecksum(data));
        Assert.That(ex.Message, Does.Contain("corrupted"));
    }

    // =========================================================================
    // AC-4: Corrupted save — no partial restore semantics
    // =========================================================================

    [Test]
    public void test_save_corrupted_exception_is_catchable_as_exception()
    {
        // Arrange
        var data = CreatePopulated();
        data.Checksum = "bad";

        // Act
        try
        {
            SaveChecksum.ValidateChecksum(data);
            Assert.Fail("Expected SaveCorruptedException was not thrown.");
        }
        catch (SaveCorruptedException ex)
        {
            // Assert — caught as the specific type
            Assert.That(ex, Is.InstanceOf<Exception>());
            Assert.That(ex.Message, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public void test_checksum_mismatch_produces_descriptive_message()
    {
        // Arrange
        var data = CreatePopulated();
        string correctHash = SaveChecksum.ComputeChecksum(data);
        data.Checksum = "deadbeef"; // deliberately wrong

        // Act
        var ex = Assert.Throws<SaveCorruptedException>(() =>
            SaveChecksum.ValidateChecksum(data));

        // Assert — message contains both expected and actual for debugging
        Assert.That(ex.Message, Does.Contain(correctHash));
        Assert.That(ex.Message, Does.Contain("deadbeef"));
    }

    [Test]
    public void test_compute_checksum_original_struct_not_mutated()
    {
        // Arrange
        var original = CreatePopulated();
        string originalChecksum = original.Checksum; // null initially

        // Act
        SaveChecksum.ComputeChecksum(original);

        // Assert — struct is passed by value, original unchanged
        Assert.That(original.Checksum, Is.EqualTo(originalChecksum));
    }
}
