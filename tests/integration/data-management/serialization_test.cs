using System;
using System.Collections.Generic;
using System.Text.Json;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Test-only data class for serialization round-trip testing.
/// Covers common field types: string, int, float, bool, List&lt;string&gt;,
/// Vector2, Color, and nullable string (null edge case).
/// </summary>
public class TestPlayerProgress
{
    public string PlayerName { get; set; }
    public int Score { get; set; }
    public float PlayTime { get; set; }
    public bool HasCompletedTutorial { get; set; }
    public List<string> VisitedFragments { get; set; }
    public Vector2 LastPosition { get; set; }
    public Color ThemeColor { get; set; }
    public string NullableField { get; set; }
}

/// <summary>
/// Integration tests for DataManager.SerializeState and DeserializeState.
/// Covers all 4 acceptance criteria from Story 005 (JSON Serialization Bridge).
///
/// AC-1: SerializeState returns valid JSON (non-null, parseable, all fields)
/// AC-2: DeserializeState round-trips correctly (preserves values, empty JSON, unknown fields)
/// AC-3: Corrupt JSON throws DataLoadException (invalid, null, type mismatch, null literal)
/// AC-4: Uses System.Text.Json only (verified by code review — no Newtonsoft references)
/// </summary>
public class serialization_test
{
    private DataManager _dm;
    private GameObject _go;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("TestDM");
        _dm = _go.AddComponent<DataManager>();
    }

    [TearDown]
    public void TearDown()
    {
        DataManager.OnStateChanged = null;
        UnityEngine.Object.DestroyImmediate(_go);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static TestPlayerProgress CreatePopulated()
    {
        return new TestPlayerProgress
        {
            PlayerName = "TestPlayer",
            Score = 42,
            PlayTime = 123.45f,
            HasCompletedTutorial = true,
            VisitedFragments = new List<string> { "frag_01", "frag_02", "frag_03" },
            LastPosition = new Vector2(1.5f, 2.5f),
            ThemeColor = new Color(0.1f, 0.2f, 0.3f, 0.4f),
            NullableField = "not_null"
        };
    }

    // =========================================================================
    // AC-1: SerializeState returns valid JSON
    // =========================================================================

    [Test]
    public void test_serialize_populated_object_returns_valid_json()
    {
        // Arrange
        var progress = CreatePopulated();

        // Act
        string json = _dm.SerializeState(progress);

        // Assert
        Assert.That(json, Is.Not.Null.And.Not.Empty);

        // Must be parseable
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Contains all fields (camelCase per _jsonOptions)
        Assert.That(root.GetProperty("playerName").GetString(), Is.EqualTo("TestPlayer"));
        Assert.That(root.GetProperty("score").GetInt32(), Is.EqualTo(42));
        Assert.That(root.GetProperty("playTime").GetSingle(), Is.EqualTo(123.45f).Within(0.01f));
        Assert.That(root.GetProperty("hasCompletedTutorial").GetBoolean(), Is.True);

        var visited = root.GetProperty("visitedFragments");
        Assert.That(visited.GetArrayLength(), Is.EqualTo(3));

        var pos = root.GetProperty("lastPosition");
        Assert.That(pos.GetProperty("x").GetSingle(), Is.EqualTo(1.5f).Within(0.01f));
        Assert.That(pos.GetProperty("y").GetSingle(), Is.EqualTo(2.5f).Within(0.01f));

        var color = root.GetProperty("themeColor");
        Assert.That(color.GetProperty("r").GetSingle(), Is.EqualTo(0.1f).Within(0.01f));
        Assert.That(color.GetProperty("g").GetSingle(), Is.EqualTo(0.2f).Within(0.01f));
        Assert.That(color.GetProperty("b").GetSingle(), Is.EqualTo(0.3f).Within(0.01f));
        Assert.That(color.GetProperty("a").GetSingle(), Is.EqualTo(0.4f).Within(0.01f));

        Assert.That(root.GetProperty("nullableField").GetString(), Is.EqualTo("not_null"));
    }

    [Test]
    public void test_serialize_empty_object_returns_empty_braces()
    {
        // Arrange
        var empty = new TestPlayerProgress();

        // Act
        string json = _dm.SerializeState(empty);

        // Assert
        Assert.That(json, Is.Not.Null);

        using var doc = JsonDocument.Parse(json);
        // All default values should be present in JSON
        var root = doc.RootElement;
        Assert.That(root.GetProperty("score").GetInt32(), Is.EqualTo(0));
        Assert.That(root.GetProperty("hasCompletedTutorial").GetBoolean(), Is.False);
    }

    [Test]
    public void test_serialize_null_field_produces_null_in_json()
    {
        // Arrange
        var progress = CreatePopulated();
        progress.NullableField = null;

        // Act
        string json = _dm.SerializeState(progress);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.That(root.GetProperty("nullableField").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    // =========================================================================
    // AC-2: DeserializeState round-trips correctly
    // =========================================================================

    [Test]
    public void test_deserialize_round_trip_preserves_all_field_values()
    {
        // Arrange
        var original = CreatePopulated();
        string json = _dm.SerializeState(original);

        // Act
        var restored = _dm.DeserializeState<TestPlayerProgress>(json);

        // Assert
        Assert.That(restored, Is.Not.Null);
        Assert.That(restored.PlayerName, Is.EqualTo(original.PlayerName));
        Assert.That(restored.Score, Is.EqualTo(original.Score));
        Assert.That(restored.PlayTime, Is.EqualTo(original.PlayTime).Within(0.01f));
        Assert.That(restored.HasCompletedTutorial, Is.EqualTo(original.HasCompletedTutorial));
        Assert.That(restored.VisitedFragments, Is.Not.Null);
        Assert.That(restored.VisitedFragments.Count, Is.EqualTo(3));
        Assert.That(restored.VisitedFragments[0], Is.EqualTo("frag_01"));
        Assert.That(restored.VisitedFragments[2], Is.EqualTo("frag_03"));
        Assert.That(restored.LastPosition.x, Is.EqualTo(1.5f).Within(0.01f));
        Assert.That(restored.LastPosition.y, Is.EqualTo(2.5f).Within(0.01f));
        Assert.That(restored.ThemeColor.r, Is.EqualTo(0.1f).Within(0.01f));
        Assert.That(restored.ThemeColor.g, Is.EqualTo(0.2f).Within(0.01f));
        Assert.That(restored.ThemeColor.b, Is.EqualTo(0.3f).Within(0.01f));
        Assert.That(restored.ThemeColor.a, Is.EqualTo(0.4f).Within(0.01f));
        Assert.That(restored.NullableField, Is.EqualTo("not_null"));
    }

    [Test]
    public void test_deserialize_empty_json_returns_default_filled_object()
    {
        // Arrange & Act
        var result = _dm.DeserializeState<TestPlayerProgress>("{}");

        // Assert — must return non-null object with default values
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PlayerName, Is.Null); // default string
        Assert.That(result.Score, Is.EqualTo(0));
        Assert.That(result.PlayTime, Is.EqualTo(0f));
        Assert.That(result.HasCompletedTutorial, Is.False);
        Assert.That(result.VisitedFragments, Is.Null); // default List<T>
        Assert.That(result.NullableField, Is.Null);
    }

    [Test]
    public void test_deserialize_partial_json_fills_missing_fields_with_defaults()
    {
        // Arrange — only a single field present (forward-compat: old save file)
        string json = "{\"playerName\":\"LegacySave\"}";

        // Act
        var result = _dm.DeserializeState<TestPlayerProgress>(json);

        // Assert — present field populated, missing fields get defaults
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PlayerName, Is.EqualTo("LegacySave"));
        Assert.That(result.Score, Is.EqualTo(0));
        Assert.That(result.HasCompletedTutorial, Is.False);
        Assert.That(result.VisitedFragments, Is.Null);
    }

    [Test]
    public void test_deserialize_unknown_json_fields_ignored_no_exception()
    {
        // Arrange
        string json = "{\"playerName\":\"test\",\"score\":10,\"unknownField\":\"should_be_ignored\",\"anotherUnknown\":999}";

        // Act
        var result = _dm.DeserializeState<TestPlayerProgress>(json);

        // Assert — no exception thrown, known fields populated
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PlayerName, Is.EqualTo("test"));
        Assert.That(result.Score, Is.EqualTo(10));
    }

    [Test]
    public void test_vector2_converter_round_trip()
    {
        // Arrange
        var original = new Vector2(-3.14f, 6.28f);
        var wrapper = new TestPlayerProgress { LastPosition = original, PlayerName = "vec2test" };

        // Act
        string json = _dm.SerializeState(wrapper);
        var restored = _dm.DeserializeState<TestPlayerProgress>(json);

        // Assert
        Assert.That(restored.LastPosition.x, Is.EqualTo(-3.14f).Within(0.01f));
        Assert.That(restored.LastPosition.y, Is.EqualTo(6.28f).Within(0.01f));
    }

    [Test]
    public void test_color_converter_round_trip()
    {
        // Arrange
        var original = new Color(0.9f, 0.8f, 0.7f, 0.6f);
        var wrapper = new TestPlayerProgress { ThemeColor = original, PlayerName = "colortest" };

        // Act
        string json = _dm.SerializeState(wrapper);
        var restored = _dm.DeserializeState<TestPlayerProgress>(json);

        // Assert
        Assert.That(restored.ThemeColor.r, Is.EqualTo(0.9f).Within(0.01f));
        Assert.That(restored.ThemeColor.g, Is.EqualTo(0.8f).Within(0.01f));
        Assert.That(restored.ThemeColor.b, Is.EqualTo(0.7f).Within(0.01f));
        Assert.That(restored.ThemeColor.a, Is.EqualTo(0.6f).Within(0.01f));
    }

    // =========================================================================
    // AC-3: Corrupt JSON throws DataLoadException
    // =========================================================================

    [Test]
    public void test_deserialize_invalid_json_throws_data_load_exception_with_json_parse_key()
    {
        // Arrange
        string brokenJson = "{broken";

        // Act & Assert
        var ex = Assert.Throws<DataLoadException>(() =>
            _dm.DeserializeState<TestPlayerProgress>(brokenJson));

        Assert.That(ex.AssetKey, Is.EqualTo("json_parse"));
        Assert.That(ex.InnerException, Is.InstanceOf<JsonException>());
    }

    [Test]
    public void test_deserialize_null_json_throws_data_load_exception()
    {
        // Act & Assert
        var ex = Assert.Throws<DataLoadException>(() =>
            _dm.DeserializeState<TestPlayerProgress>(null));

        Assert.That(ex.AssetKey, Is.EqualTo("json_parse"));
        Assert.That(ex.InnerException, Is.InstanceOf<ArgumentNullException>());
    }

    [Test]
    public void test_deserialize_type_mismatch_throws_data_load_exception()
    {
        // Arrange — score should be int but we pass a string
        string json = "{\"playerName\":\"test\",\"score\":\"not_a_number\"}";

        // Act & Assert
        var ex = Assert.Throws<DataLoadException>(() =>
            _dm.DeserializeState<TestPlayerProgress>(json));

        Assert.That(ex.AssetKey, Is.EqualTo("json_parse"));
        Assert.That(ex.InnerException, Is.InstanceOf<JsonException>());
    }

    [Test]
    public void test_deserialize_json_null_literal_throws_data_load_exception()
    {
        // Arrange — JsonSerializer.Deserialize<TestPlayerProgress>("null") returns null
        string json = "null";

        // Act & Assert
        var ex = Assert.Throws<DataLoadException>(() =>
            _dm.DeserializeState<TestPlayerProgress>(json));

        Assert.That(ex.AssetKey, Is.EqualTo("deserialize"));
        Assert.That(ex.Message, Does.Contain("returned null"));
    }

    // =========================================================================
    // AC-4: System.Text.Json only (verified by code review)
    // =========================================================================

    [Test]
    public void test_serialize_uses_camel_case_naming()
    {
        // Arrange
        var progress = new TestPlayerProgress { PlayerName = "NamingTest", Score = 100 };

        // Act
        string json = _dm.SerializeState(progress);

        // Assert — camelCase keys in JSON
        Assert.That(json, Does.Contain("\"playerName\""));
        Assert.That(json, Does.Contain("\"score\""));
        Assert.That(json, Does.Not.Contain("\"PlayerName\""));
        Assert.That(json, Does.Not.Contain("\"Score\""));
    }

    [Test]
    public void test_serialize_produces_compressed_json_no_indentation()
    {
        // Arrange
        var progress = CreatePopulated();

        // Act
        string json = _dm.SerializeState(progress);

        // Assert — compressed: no newlines (Unity JsonSerializer with WriteIndented=false)
        Assert.That(json, Does.Not.Contain("\n"));
    }
}
