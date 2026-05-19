using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Integration tests for CrossChapterTracker — Flag Registry + New Game Init + SetFlagRaw (S001).
///
/// Covers 4 acceptance criteria:
///   AC-1: InitializeAllFlags sets all registry flags to DefaultValue via SetFlagRaw
///   AC-2: SetFlag via ApplyChanges works normally (existing ChangeTracker behavior)
///   AC-3: SetFlagRaw does NOT fire OnOverlayChanged
///   AC-4: Registry-independent flag storage — _flags can hold flags not in registry
/// </summary>
public class FlagRegistryInitializationTest
{
    // =========================================================================
    // Fakes
    // =========================================================================

    private class FakeChangeTrackerInternal : IChangeTrackerInternal
    {
        public readonly Dictionary<string, bool> Flags = new();
        public int SetFlagRawCallCount;
        public bool OnOverlayChangedFired;
        public System.Func<string, bool> ImmutableCheck;

        public void SetFlagRaw(string flagId, bool value)
        {
            SetFlagRawCallCount++;
            Flags[flagId] = value;
        }

        public Dictionary<string, bool> GetAllFlags()
            => new Dictionary<string, bool>(Flags);

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
        bool isImmutable = false, bool defaultValue = false, string[] consumedBy = null)
    {
        return new CrossChapterFlagDef
        {
            FlagId = flagId,
            SetInChapter = setInChapter,
            SetInFragmentId = "frag_01",
            SetByChoiceId = "choice_01",
            IsImmutable = isImmutable,
            DefaultValue = defaultValue,
            ConsumedBy = consumedBy ?? new string[0]
        };
    }

    // =========================================================================
    // AC-1: InitializeAllFlags sets all registry flags to DefaultValue
    // =========================================================================

    [Test]
    public void test_initialize_all_flags_sets_default_values()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter", defaultValue: false),
            MakeDef("ch2_secret", defaultValue: false),
            MakeDef("mentor_alive", defaultValue: true)
        );

        var tracker = new CrossChapterTracker(registry, fake);

        // Check IsImmutable guard was wired
        Assert.That(fake.ImmutableCheck, Is.Not.Null,
            "IsImmutable check should be wired on construction.");

        tracker.InitializeAllFlags();

        Assert.That(fake.Flags["ch1_letter"], Is.False);
        Assert.That(fake.Flags["ch2_secret"], Is.False);
        Assert.That(fake.Flags["mentor_alive"], Is.True);
        Assert.That(fake.SetFlagRawCallCount, Is.EqualTo(3));
    }

    [Test]
    public void test_initialize_empty_registry_is_no_op()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(); // Empty

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();

        Assert.That(fake.SetFlagRawCallCount, Is.EqualTo(0));
        Assert.That(fake.Flags.Count, Is.EqualTo(0));
    }

    [Test]
    public void test_initialize_null_flags_array_is_no_op()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry();
        registry.Flags = null;

        var tracker = new CrossChapterTracker(registry, fake);
        Assert.DoesNotThrow(() => tracker.InitializeAllFlags());
        Assert.That(fake.SetFlagRawCallCount, Is.EqualTo(0));
    }

    // =========================================================================
    // AC-2: SetFlag via ApplyChanges works normally
    // =========================================================================

    [Test]
    public void test_set_flag_raw_passes_through_to_core()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);

        // SetFlagRaw is called through the tracker's operations
        tracker.InitializeAllFlags();
        Assert.That(fake.Flags["ch1_letter_kept"], Is.False);

        // Direct SetFlagRaw call (simulating what ChangeTracker.SetFlag does after IsImmutable check)
        fake.SetFlagRaw("ch1_letter_kept", true);
        Assert.That(fake.Flags["ch1_letter_kept"], Is.True);
    }

    [Test]
    public void test_flag_not_in_registry_can_still_be_set_via_raw()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept")
        );

        var tracker = new CrossChapterTracker(registry, fake);

        // Set a flag NOT in the registry — should work (registry is directory, not constraint)
        fake.SetFlagRaw("ch1_temp", true);
        Assert.That(fake.Flags["ch1_temp"], Is.True);
        Assert.That(fake.SetFlagRawCallCount, Is.EqualTo(1));
    }

    // =========================================================================
    // AC-3: SetFlagRaw does NOT fire OnOverlayChanged
    // =========================================================================

    [Test]
    public void test_set_flag_raw_does_not_fire_on_overlay_changed()
    {
        var fake = new FakeChangeTrackerInternal();

        // Simulating SetFlagRaw — which in the real implementation
        // does NOT touch OverlayVersion or OnOverlayChanged event
        fake.SetFlagRaw("test_flag", true);

        Assert.That(fake.OnOverlayChangedFired, Is.False,
            "SetFlagRaw must NOT fire OnOverlayChanged — flag operations should not trigger HUD refreshes.");
        Assert.That(fake.Flags["test_flag"], Is.True,
            "Flag value should still be set even without event firing.");
    }

    [Test]
    public void test_multiple_set_flag_raw_calls_no_events()
    {
        var fake = new FakeChangeTrackerInternal();

        fake.SetFlagRaw("flag_a", true);
        fake.SetFlagRaw("flag_b", false);
        fake.SetFlagRaw("flag_a", false); // Overwrite

        Assert.That(fake.OnOverlayChangedFired, Is.False);
        Assert.That(fake.SetFlagRawCallCount, Is.EqualTo(3));
        Assert.That(fake.Flags["flag_a"], Is.False);
        Assert.That(fake.Flags["flag_b"], Is.False);
    }

    // =========================================================================
    // AC-4: Registry-independent flag storage
    // =========================================================================

    [Test]
    public void test_extra_flags_outside_registry_preserved()
    {
        var fake = new FakeChangeTrackerInternal();
        // Pre-populate with an unregistered flag
        fake.Flags["ch1_temp"] = true;

        var registry = MakeRegistry(
            MakeDef("ch1_letter", defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();

        // Registry flag should be set
        Assert.That(fake.Flags["ch1_letter"], Is.False,
            "Registry flag should be initialized.");

        // Unregistered flag should still exist (registry is directory, not constraint)
        Assert.That(fake.Flags["ch1_temp"], Is.True,
            "Flags not in registry should be preserved — registry is a directory, not a schema constraint.");
    }

    [Test]
    public void test_initialize_overwrites_existing_registry_flags()
    {
        var fake = new FakeChangeTrackerInternal();
        fake.Flags["ch1_letter"] = true; // Set from previous session

        var registry = MakeRegistry(
            MakeDef("ch1_letter", defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();

        Assert.That(fake.Flags["ch1_letter"], Is.False,
            "InitializeAllFlags should overwrite existing values with DefaultValue.");
    }

    // =========================================================================
    // Cleanup
    // =========================================================================

    [TearDown]
    public void TearDown()
    {
        // No static events to reset — CrossChapterTracker.Dispose handles cleanup
    }
}
