using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Integration tests for CrossChapterTracker — Flag Persistence Bridge (S003).
///
/// Covers 4 acceptance criteria:
///   AC-1: GetPersistableFlags returns registry flags with current values
///   AC-2: RestoreFlags correctly restores saved flag values
///   AC-3: Orphan flags (in save but not in registry) preserved with warning
///   AC-4: Round-trip fidelity — GetPersistableFlags → RestoreFlags preserves all values
/// </summary>
public class PersistenceBridgeTest
{
    // =========================================================================
    // Fakes
    // =========================================================================

    private class FakeChangeTrackerInternal : IChangeTrackerInternal
    {
        public readonly Dictionary<string, bool> Flags = new();
        public int SetFlagRawCallCount;
        public System.Func<string, bool> ImmutableCheck;

        public void SetFlagRaw(string flagId, bool value)
        {
            SetFlagRawCallCount++;
            Flags[flagId] = value;
        }

        public Dictionary<string, bool> GetAllFlags()
        {
            SetFlagRawCallCount++; // Count as observable action
            return new Dictionary<string, bool>(Flags);
        }

        public void SetImmutableFlagCheck(System.Func<string, bool> isImmutableFunc)
        {
            ImmutableCheck = isImmutableFunc;
        }
    }

    private CrossChapterFlagRegistry MakeRegistry(params CrossChapterFlagDef[] flags)
    {
        var reg = ScriptableObject.CreateInstance<CrossChapterFlagRegistry>();
        reg.Flags = flags;
        return reg;
    }

    private CrossChapterFlagDef MakeDef(string flagId, string setInChapter = "ch01",
        bool isImmutable = false, bool defaultValue = false)
    {
        return new CrossChapterFlagDef
        {
            FlagId = flagId,
            SetInChapter = setInChapter,
            SetInFragmentId = "frag_01",
            SetByChoiceId = "choice_01",
            IsImmutable = isImmutable,
            DefaultValue = defaultValue,
            ConsumedBy = new string[0]
        };
    }

    // =========================================================================
    // AC-1: GetPersistableFlags returns registry flags with current values
    // =========================================================================

    [Test]
    public void test_get_persistable_flags_returns_current_values()
    {
        var fake = new FakeChangeTrackerInternal();
        fake.Flags["ch1_letter_kept"] = true;
        fake.Flags["ch2_secret"] = false;

        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept"),
            MakeDef("ch2_secret")
        );

        var tracker = new CrossChapterTracker(registry, fake);
        var result = tracker.GetPersistableFlags();

        Assert.That(result["ch1_letter_kept"], Is.True);
        Assert.That(result["ch2_secret"], Is.False);
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void test_get_persistable_flags_returns_default_for_unset_registry_flags()
    {
        var fake = new FakeChangeTrackerInternal();
        // Only "ch1_letter_kept" was ever set
        fake.Flags["ch1_letter_kept"] = true;

        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", defaultValue: false),
            MakeDef("ch2_secret", defaultValue: true),
            MakeDef("ch3_key", defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        var result = tracker.GetPersistableFlags();

        Assert.That(result["ch1_letter_kept"], Is.True,
            "Set flag should return its current value.");
        Assert.That(result["ch2_secret"], Is.True,
            "Unset flag should return its DefaultValue (true).");
        Assert.That(result["ch3_key"], Is.False,
            "Unset flag should return its DefaultValue (false).");
        Assert.That(result.Count, Is.EqualTo(3));
    }

    [Test]
    public void test_get_persistable_flags_excludes_non_registry_flags()
    {
        var fake = new FakeChangeTrackerInternal();
        fake.Flags["ch1_letter_kept"] = true;
        fake.Flags["ch1_temp"] = true; // Not in registry

        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept")
        );

        var tracker = new CrossChapterTracker(registry, fake);
        var result = tracker.GetPersistableFlags();

        Assert.That(result.ContainsKey("ch1_letter_kept"), Is.True);
        Assert.That(result.ContainsKey("ch1_temp"), Is.False,
            "Non-registry flags should NOT appear in persistable flags.");
        Assert.That(result.Count, Is.EqualTo(1));
    }

    // =========================================================================
    // AC-2: RestoreFlags correctly restores saved flag values
    // =========================================================================

    [Test]
    public void test_restore_flags_sets_values_correctly()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", defaultValue: false),
            MakeDef("ch2_secret", defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);

        var savedFlags = new Dictionary<string, bool>
        {
            { "ch1_letter_kept", true },
            { "ch2_secret", false }
        };

        tracker.RestoreFlags(savedFlags);

        Assert.That(fake.Flags["ch1_letter_kept"], Is.True);
        Assert.That(fake.Flags["ch2_secret"], Is.False);
    }

    [Test]
    public void test_restore_flags_sets_default_for_missing_registry_flags()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", defaultValue: false),
            MakeDef("ch2_secret", defaultValue: true) // Default true
        );

        var tracker = new CrossChapterTracker(registry, fake);

        // Save only has ch1_letter_kept — ch2_secret was added in a newer build
        var savedFlags = new Dictionary<string, bool>
        {
            { "ch1_letter_kept", true }
        };

        tracker.RestoreFlags(savedFlags);

        Assert.That(fake.Flags["ch1_letter_kept"], Is.True);
        Assert.That(fake.Flags["ch2_secret"], Is.True,
            "Missing registry flags should get their DefaultValue.");
    }

    [Test]
    public void test_restore_empty_saved_flags()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", defaultValue: true)
        );

        var tracker = new CrossChapterTracker(registry, fake);

        tracker.RestoreFlags(new Dictionary<string, bool>());

        Assert.That(fake.Flags["ch1_letter_kept"], Is.True,
            "Empty save should set all registry flags to DefaultValue.");
    }

    [Test]
    public void test_restore_null_saved_flags_is_no_op()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", defaultValue: false)
        );
        fake.Flags["ch1_letter_kept"] = true; // Pre-existing

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.RestoreFlags(null);

        // Null save → no change
        Assert.That(fake.Flags["ch1_letter_kept"], Is.True,
            "Null savedFlags should leave existing state unchanged.");
    }

    // =========================================================================
    // AC-3: Orphan flags preserved with warning
    // =========================================================================

    [Test]
    public void test_orphan_flag_in_save_preserved_with_warning()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("new_flag", defaultValue: false) // Current registry
        );

        var tracker = new CrossChapterTracker(registry, fake);

        var savedFlags = new Dictionary<string, bool>
        {
            { "old_flag", true }, // Orphan — removed from registry in newer build
            { "new_flag", true }
        };

        LogAssert.Expect(LogType.Warning,
            "CrossChapterTracker: Saved flag 'old_flag' not found in CrossChapterFlagRegistry — value preserved but not tracked by registry.");

        tracker.RestoreFlags(savedFlags);

        Assert.That(fake.Flags["old_flag"], Is.True,
            "Orphan flag should still be restored — it may be consumed by conditions.");
        Assert.That(fake.Flags["new_flag"], Is.True);
    }

    [Test]
    public void test_all_orphan_flags_restored_with_warnings()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(); // Current registry is empty

        var tracker = new CrossChapterTracker(registry, fake);

        var savedFlags = new Dictionary<string, bool>
        {
            { "old_flag_a", true },
            { "old_flag_b", false }
        };

        LogAssert.Expect(LogType.Warning,
            "CrossChapterTracker: Saved flag 'old_flag_a' not found in CrossChapterFlagRegistry — value preserved but not tracked by registry.");
        LogAssert.Expect(LogType.Warning,
            "CrossChapterTracker: Saved flag 'old_flag_b' not found in CrossChapterFlagRegistry — value preserved but not tracked by registry.");

        tracker.RestoreFlags(savedFlags);

        Assert.That(fake.Flags["old_flag_a"], Is.True);
        Assert.That(fake.Flags["old_flag_b"], Is.False);
    }

    // =========================================================================
    // AC-4: Round-trip fidelity
    // =========================================================================

    [Test]
    public void test_round_trip_preserves_all_values()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("flag_a", defaultValue: false),
            MakeDef("flag_b", defaultValue: false),
            MakeDef("flag_c", defaultValue: true),
            MakeDef("flag_d", defaultValue: false),
            MakeDef("flag_e", defaultValue: false),
            MakeDef("flag_f", defaultValue: true),
            MakeDef("flag_g", defaultValue: false),
            MakeDef("flag_h", defaultValue: false),
            MakeDef("flag_i", defaultValue: true),
            MakeDef("flag_j", defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);

        // Set initial values (simulating game state)
        fake.Flags["flag_a"] = true;
        fake.Flags["flag_b"] = false;
        fake.Flags["flag_c"] = false; // Override default
        fake.Flags["flag_d"] = true;
        fake.Flags["flag_e"] = true;
        fake.Flags["flag_f"] = true;  // Same as default
        fake.Flags["flag_g"] = false;
        fake.Flags["flag_h"] = true;
        fake.Flags["flag_i"] = false; // Override default
        fake.Flags["flag_j"] = true;

        // Round-trip
        var saved = tracker.GetPersistableFlags();
        fake.Flags.Clear();
        tracker.RestoreFlags(saved);

        Assert.That(fake.Flags["flag_a"], Is.True);
        Assert.That(fake.Flags["flag_b"], Is.False);
        Assert.That(fake.Flags["flag_c"], Is.False);
        Assert.That(fake.Flags["flag_d"], Is.True);
        Assert.That(fake.Flags["flag_e"], Is.True);
        Assert.That(fake.Flags["flag_f"], Is.True);
        Assert.That(fake.Flags["flag_g"], Is.False);
        Assert.That(fake.Flags["flag_h"], Is.True);
        Assert.That(fake.Flags["flag_i"], Is.False);
        Assert.That(fake.Flags["flag_j"], Is.True);

        Assert.That(saved.Count, Is.EqualTo(10));
    }

    [Test]
    public void test_round_trip_with_zero_flags()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry();

        var tracker = new CrossChapterTracker(registry, fake);
        var saved = tracker.GetPersistableFlags();

        Assert.That(saved.Count, Is.EqualTo(0),
            "Empty registry should produce empty persistable flags.");

        tracker.RestoreFlags(saved);
        Assert.That(fake.Flags.Count, Is.EqualTo(0));
    }

    [Test]
    public void test_round_trip_with_unset_flags_gets_defaults()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("flag_a", defaultValue: false),
            MakeDef("flag_b", defaultValue: true),
            MakeDef("flag_c", defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        // No flags set — all should get defaults
        var saved = tracker.GetPersistableFlags();

        Assert.That(saved["flag_a"], Is.False);
        Assert.That(saved["flag_b"], Is.True);
        Assert.That(saved["flag_c"], Is.False);

        // Round-trip
        tracker.RestoreFlags(saved);
        Assert.That(fake.Flags["flag_a"], Is.False);
        Assert.That(fake.Flags["flag_b"], Is.True);
        Assert.That(fake.Flags["flag_c"], Is.False);
    }

    // =========================================================================
    // Cleanup
    // =========================================================================

    [TearDown]
    public void TearDown()
    {
        ChapterManager.OnChapterReplayStarted = null;
    }
}
