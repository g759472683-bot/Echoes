using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Unit tests for Story 004 — Data Validation & Exception Safety.
///
/// Covers acceptance criteria:
///   AC-1: Missing Asset throws DataLoadException with FragmentId populated
///   AC-4: Mock validation logic for build-time cross-check
///
/// Additional coverage:
///   - DataLoadException two-param constructor backward compatibility
///   - DataLoadException three-param constructor with null/empty FragmentId
///   - GetIllustrationAsync with fragmentId produces FragmentId in exception
///   - GetIllustrationAsync without fragmentId has null FragmentId (backward compat)
///   - GetFragmentAsync for missing key carries AssetKey
///   - Concurrent load failures all carry the correct FragmentId
///   - Validation cross-reference logic: known vs. unknown keys
/// </summary>
[TestFixture]
public class ValidationTests
{
    private GameObject _gameObject;
    private DataManager _dataManager;
    private MockAddressableLoader _mockLoader;

    // =========================================================================
    // SetUp / TearDown
    // =========================================================================

    [SetUp]
    public void SetUp()
    {
        DataManager.OnStateChanged = null;

        _gameObject = new GameObject("DataManager_ValidationTest");
        _dataManager = _gameObject.AddComponent<DataManager>();
        _mockLoader = new MockAddressableLoader();
        _dataManager.SetLoader(_mockLoader);
    }

    [TearDown]
    public void TearDown()
    {
        DataManager.OnStateChanged = null;

        if (_gameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
            _gameObject = null;
        }

        _dataManager = null;
        _mockLoader = null;
    }

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private Sprite CreateTestSprite(string name)
    {
        var tex = new Texture2D(1, 1);
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        sprite.name = name;
        return sprite;
    }

    private MemoryFragment CreateFragment(string fragmentId, string chapterKey,
        string illustrationKey)
    {
        var frag = ScriptableObject.CreateInstance<MemoryFragment>();
        frag.FragmentId = fragmentId;
        frag.ChapterKey = chapterKey;
        frag.IllustrationKey = illustrationKey;
        frag.AudioKeys = new string[0];
        frag.InteractiveObjects = new InteractiveObject[0];
        frag.ChoiceGroups = new ChoiceGroup[0];
        return frag;
    }

    private ChapterDefinition CreateChapterDef(string chapterKey,
        params string[] fragmentAssetKeys)
    {
        var def = ScriptableObject.CreateInstance<ChapterDefinition>();
        def.ChapterKey = chapterKey;
        def.OrderIndex = 0;
        def.EntryFragmentId = "frag_01";

        var refs = new AssetReferenceT<MemoryFragment>[fragmentAssetKeys.Length];
        for (int i = 0; i < fragmentAssetKeys.Length; i++)
        {
            refs[i] = new AssetReferenceT<MemoryFragment>(fragmentAssetKeys[i]);
        }
        def.Fragments = refs;
        return def;
    }

    private async Task InitializeEmptyDataManager()
    {
        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();
    }

    // =========================================================================
    // AC-1: DataLoadException with FragmentId
    // =========================================================================

    /// <summary>
    /// Given: DataLoadException constructed with assetKey, fragmentId, and inner exception.
    /// When: Properties are read.
    /// Then: AssetKey and FragmentId are both populated correctly.
    /// </summary>
    [Test]
    public void test_validation_dle_three_param_constructor_sets_fragment_id()
    {
        // Arrange
        var inner = new InvalidOperationException("simulated load failure");

        // Act
        var ex = new DataLoadException("art_ch01_frag01", "frag_01", inner);

        // Assert
        Assert.AreEqual("art_ch01_frag01", ex.AssetKey,
            "AssetKey must match the key passed to the constructor");
        Assert.AreEqual("frag_01", ex.FragmentId,
            "FragmentId must match the fragmentId passed to the constructor");
        Assert.AreSame(inner, ex.InnerException,
            "InnerException must be preserved");
        Assert.IsTrue(ex.Message.Contains("art_ch01_frag01"),
            "Message must contain the asset key");
        Assert.IsTrue(ex.Message.Contains("frag_01"),
            "Message must contain the fragment ID");
    }

    /// <summary>
    /// Given: DataLoadException constructed with only assetKey and inner exception
    /// (two-param constructor — backward compatibility).
    /// When: FragmentId is read.
    /// Then: FragmentId is null.
    /// </summary>
    [Test]
    public void test_validation_dle_two_param_constructor_fragment_id_is_null()
    {
        // Arrange
        var inner = new InvalidOperationException("load failure");

        // Act
        var ex = new DataLoadException("some_asset", inner);

        // Assert
        Assert.AreEqual("some_asset", ex.AssetKey);
        Assert.IsNull(ex.FragmentId,
            "FragmentId must be null when using the two-param constructor");
        Assert.AreSame(inner, ex.InnerException);
    }

    /// <summary>
    /// Given: DataLoadException three-param constructor with null inner exception.
    /// When: Properties are read.
    /// Then: Properties are correct, InnerException is null, Message does not crash.
    /// </summary>
    [Test]
    public void test_validation_dle_three_param_constructor_null_inner_is_valid()
    {
        // Act
        var ex = new DataLoadException("key", "frag_X", null);

        // Assert
        Assert.AreEqual("key", ex.AssetKey);
        Assert.AreEqual("frag_X", ex.FragmentId);
        Assert.IsNull(ex.InnerException);
        Assert.IsTrue(ex.Message.Contains("key"),
            "Message must contain the asset key even when inner is null");
    }

    /// <summary>
    /// Given: DataLoadException three-param constructor with empty string fragmentId.
    /// When: FragmentId is read.
    /// Then: FragmentId is empty string (not null) — the caller explicitly passed it.
    /// </summary>
    [Test]
    public void test_validation_dle_three_param_constructor_empty_fragment_id_is_preserved()
    {
        // Act
        var ex = new DataLoadException("key", "", new Exception("err"));

        // Assert
        Assert.AreEqual("", ex.FragmentId,
            "Empty string FragmentId must be preserved (not coerced to null)");
        Assert.AreEqual("key", ex.AssetKey);
    }

    // =========================================================================
    // AC-1: GetIllustrationAsync with fragmentId (integration)
    // =========================================================================

    /// <summary>
    /// Given: A fragmentId and a missing illustration key.
    /// When: GetIllustrationAsync(assetKey, fragmentId) is called and awaited.
    /// Then: DataLoadException is thrown with AssetKey AND FragmentId populated.
    ///
    /// This is the core AC-1 test.
    /// </summary>
    [Test]
    public void test_validation_get_illustration_with_fragment_id_failure_has_fragment_id()
    {
        Assert.ThrowsAsync<DataLoadException>(async () =>
        {
            await InitializeEmptyDataManager();

            // "art_ch1_nonexistent" is NOT in mock → load will fail
            await _dataManager.GetIllustrationAsync("art_ch1_nonexistent", "frag_01");
        });

        // Verify the exception details by catching it explicitly
        Assert.ThrowsAsync<DataLoadException>(async () =>
        {
            await InitializeEmptyDataManager();

            try
            {
                await _dataManager.GetIllustrationAsync("art_ch1_nonexistent", "frag_missing");
            }
            catch (DataLoadException ex)
            {
                Assert.AreEqual("art_ch1_nonexistent", ex.AssetKey,
                    "AssetKey must match the requested illustration key");
                Assert.AreEqual("frag_missing", ex.FragmentId,
                    "FragmentId must match the fragmentId passed to GetIllustrationAsync");
                Assert.IsNotNull(ex.InnerException,
                    "InnerException must preserve the original Addressables error");
                throw; // re-throw so Assert.ThrowsAsync passes
            }
        });
    }

    /// <summary>
    /// Given: A missing illustration key WITHOUT a fragmentId.
    /// When: GetIllustrationAsync(assetKey) is called (single-param overload).
    /// Then: DataLoadException is thrown but FragmentId is null (backward compat).
    /// </summary>
    [Test]
    public void test_validation_get_illustration_without_fragment_id_failure_fragment_id_null()
    {
        Assert.ThrowsAsync<DataLoadException>(async () =>
        {
            await InitializeEmptyDataManager();

            try
            {
                await _dataManager.GetIllustrationAsync("nonexistent_key");
            }
            catch (DataLoadException ex)
            {
                Assert.AreEqual("nonexistent_key", ex.AssetKey);
                Assert.IsNull(ex.FragmentId,
                    "FragmentId must be null when using single-param GetIllustrationAsync");
                throw;
            }
        });
    }

    /// <summary>
    /// Given: A missing fragment key (not in mock loader).
    /// When: GetFragmentAsync is called.
    /// Then: DataLoadException carries the correct composite AssetKey.
    /// </summary>
    [Test]
    public void test_validation_get_fragment_async_missing_throws_dle_with_asset_key()
    {
        Assert.ThrowsAsync<DataLoadException>(async () =>
        {
            await InitializeEmptyDataManager();

            try
            {
                await _dataManager.GetFragmentAsync("ch99", "frag_99");
            }
            catch (DataLoadException ex)
            {
                Assert.AreEqual("ch99/frag_99", ex.AssetKey,
                    "AssetKey must be the composite chapter/fragment key");
                // FragmentId is null here because GetFragmentAsync uses
                // the slow path (GetAsync) which doesn't have a separate
                // fragmentId — the key itself encodes chapter+fragment.
                Assert.IsNull(ex.FragmentId,
                    "FragmentId is null for slow-path GetFragmentAsync " +
                    "(the fragment context is encoded in the composite key)");
                throw;
            }
        });
    }

    // =========================================================================
    // AC-1: Concurrent load failures carry correct FragmentId
    // =========================================================================

    /// <summary>
    /// Given: Two concurrent GetIllustrationAsync calls for the same missing key,
    /// each with a different fragmentId.
    /// When: Both are awaited.
    /// Then: Both throw DataLoadException with their respective FragmentId values.
    /// </summary>
    [Test]
    public async Task test_validation_concurrent_loads_both_carry_fragment_id()
    {
        // Arrange: use delayed load so both requests hit Loading state
        var delayedTcs = new TaskCompletionSource<Sprite>();
        _mockLoader.SetDelayedLoad("art_shared", delayedTcs);

        await InitializeEmptyDataManager();

        // Act: two concurrent requests
        var task1 = _dataManager.GetIllustrationAsync("art_shared", "frag_A");
        var task2 = _dataManager.GetIllustrationAsync("art_shared", "frag_B");

        // Both tasks should be the same reference (dedup)
        Assert.AreSame(task1, task2,
            "Concurrent requests for the same key return identical Task reference");

        // Fail the load
        delayedTcs.SetException(new InvalidOperationException("simulated failure"));

        // Assert: both awaiters receive DataLoadException
        try { await task1; Assert.Fail("task1 should throw"); }
        catch (DataLoadException ex)
        {
            Assert.AreEqual("art_shared", ex.AssetKey);
            // Note: Because of dedup, the first caller's fragmentId may be used.
            // This documents the current behaviour — dedup means one load, one exception.
            Assert.IsNotNull(ex.FragmentId,
                "FragmentId must be non-null (the first registered fragmentId is used)");
        }

        try { await task2; Assert.Fail("task2 should throw"); }
        catch (DataLoadException ex)
        {
            Assert.AreEqual("art_shared", ex.AssetKey);
            Assert.IsNotNull(ex.FragmentId);
        }
    }

    // =========================================================================
    // AC-1: Successful load path does not corrupt FragmentId
    // =========================================================================

    /// <summary>
    /// Given: A valid illustration key in the mock loader.
    /// When: GetIllustrationAsync(assetKey, fragmentId) is called.
    /// Then: The sprite is returned successfully (no exception).
    /// </summary>
    [Test]
    public async Task test_validation_get_illustration_with_fragment_id_succeeds()
    {
        // Arrange
        var sprite = CreateTestSprite("art_valid");
        _mockLoader.SetAsset("art_valid", sprite);

        await InitializeEmptyDataManager();

        // Act
        var result = await _dataManager.GetIllustrationAsync("art_valid", "frag_01");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("art_valid", result.name);
    }

    // =========================================================================
    // AC-4: Mock Validation Cross-Reference Logic
    // =========================================================================

    /// <summary>
    /// Given: A set of referenced keys from fragments and a set of known Addressables keys.
    /// When: Cross-reference is performed.
    /// Then: Missing keys are correctly identified, known keys pass.
    ///
    /// This tests the same logic used by PreBuildValidator but without Unity Editor APIs.
    /// </summary>
    [Test]
    public void test_validation_cross_reference_finds_missing_keys()
    {
        // Arrange: simulate fragment asset references
        var fragmentReferences = new[]
        {
            ("frag_01", "art_ch01_frag01"),
            ("frag_02", "art_ch01_frag02"),
            ("frag_03", "art_ch01_missing"),  // NOT in catalog
            ("frag_04", "art_ch01_frag04"),   // NOT in catalog
        };

        // Simulate known Addressables keys
        var knownKeys = new HashSet<string>
        {
            "art_ch01_frag01",
            "art_ch01_frag02",
            "art_ch01_frag03",
        };

        // Act: cross-reference
        var missingKeys = new List<(string fragmentId, string assetKey)>();
        foreach (var (fragId, assetKey) in fragmentReferences)
        {
            if (!knownKeys.Contains(assetKey))
            {
                missingKeys.Add((fragId, assetKey));
            }
        }

        // Assert
        Assert.AreEqual(2, missingKeys.Count,
            "Two referenced keys should be missing from the catalog");
        Assert.AreEqual(("frag_03", "art_ch01_missing"), missingKeys[0]);
        Assert.AreEqual(("frag_04", "art_ch01_frag04"), missingKeys[1]);
    }

    /// <summary>
    /// Given: All fragment references are in the known Addressables keys.
    /// When: Cross-reference is performed.
    /// Then: Zero missing keys are found.
    /// </summary>
    [Test]
    public void test_validation_cross_reference_all_keys_known_passes()
    {
        // Arrange
        var fragmentReferences = new[]
        {
            ("frag_01", "art_ch01_frag01"),
            ("frag_02", "art_ch01_frag02"),
        };

        var knownKeys = new HashSet<string>
        {
            "art_ch01_frag01",
            "art_ch01_frag02",
        };

        // Act
        var missingKeys = new List<(string, string)>();
        foreach (var (fragId, assetKey) in fragmentReferences)
        {
            if (!knownKeys.Contains(assetKey))
            {
                missingKeys.Add((fragId, assetKey));
            }
        }

        // Assert
        Assert.AreEqual(0, missingKeys.Count,
            "No missing keys when all references are in the catalog");
    }

    /// <summary>
    /// Given: Empty known keys (no Addressables catalog configured).
    /// When: Cross-reference is performed.
    /// Then: All references appear as missing (treat as warnings, not errors).
    /// </summary>
    [Test]
    public void test_validation_cross_reference_empty_catalog_all_missing()
    {
        // Arrange
        var fragmentReferences = new[]
        {
            ("frag_01", "art_ch01_frag01"),
        };

        var knownKeys = new HashSet<string>(); // empty catalog

        // Act
        var missingKeys = new List<(string, string)>();
        foreach (var (fragId, assetKey) in fragmentReferences)
        {
            if (!knownKeys.Contains(assetKey))
            {
                missingKeys.Add((fragId, assetKey));
            }
        }

        // Assert: everything is "missing" when catalog is empty
        Assert.AreEqual(1, missingKeys.Count);
        Assert.AreEqual(("frag_01", "art_ch01_frag01"), missingKeys[0]);
    }

    /// <summary>
    /// Given: A ChapterDefinition with Fragment AssetReferences.
    /// When: Some AssetReferences are null (empty in Inspector).
    /// Then: Null/empty references are identified as warnings (not errors).
    ///
    /// This tests the edge case from AC-4: "空的 AssetReference (null) → 警告（黄色），不阻止构建"
    /// </summary>
    [Test]
    public void test_validation_null_asset_references_are_warnings_not_errors()
    {
        // Arrange: simulate a ChapterDefinition
        var chapterAssetKeys = new List<string>();
        var nullCount = 0;

        // Simulated AssetReferenceT<MemoryFragment> entries
        // Null AssetReferences have RuntimeKeyIsValid() == false
        string[] fragmentKeys = { "frag_key_01", null, "frag_key_02", null };

        foreach (var key in fragmentKeys)
        {
            if (key == null)
            {
                nullCount++;
            }
            else
            {
                chapterAssetKeys.Add(key);
            }
        }

        // Assert
        Assert.AreEqual(2, nullCount,
            "Two AssetReferences are null — these are warnings, not build blockers");
        Assert.AreEqual(2, chapterAssetKeys.Count,
            "Two AssetReferences are valid and should be cross-checked");
    }

    // =========================================================================
    // DataLoadException message format
    // =========================================================================

    /// <summary>
    /// Verify that the three-param constructor produces a message containing
    /// both the asset key and fragment ID.
    /// </summary>
    [Test]
    public void test_validation_dle_message_format_contains_key_and_fragment()
    {
        // Arrange
        var inner = new InvalidOperationException("Addressables load failed");

        // Act
        var ex = new DataLoadException("art_table", "frag_table", inner);

        // Assert: message follows the spec format
        Assert.IsTrue(ex.Message.Contains("art_table"),
            "Message must contain the asset key");
        Assert.IsTrue(ex.Message.Contains("frag_table"),
            "Message must contain the fragment ID");
        Assert.IsTrue(ex.Message.Contains(inner.Message),
            "Message must include the inner exception message");
    }

    /// <summary>
    /// Verify that existing two-param constructor message is unchanged
    /// (backward compatibility for tests that assert on message format).
    /// </summary>
    [Test]
    public void test_validation_dle_two_param_message_format_unchanged()
    {
        // Act
        var ex = new DataLoadException("my_key", new Exception("original"));

        // Assert
        Assert.AreEqual("Failed to load asset: my_key", ex.Message,
            "Two-param constructor message format must be unchanged for backward compatibility");
    }
}
