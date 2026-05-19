using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Unit tests for CrossChapterTracker — IsImmutable protection + chapter replay lifecycle (S002).
///
/// Covers 4 acceptance criteria:
///   AC-1: IsImmutable=true flag rejects SetFlag(false) when current value is true
///   AC-2: OnChapterReplayStarted activates protection — immutable preserved, non-immutable reset
///   AC-3: Non-immutable flag resets to DefaultValue on OnChapterReplayStarted
///   AC-4: Idempotent SetFlag(true) on already-true immutable flag
/// </summary>
public class ImmutableProtectionReplayTest
{
    // =========================================================================
    // Fakes
    // =========================================================================

    private class FakeChangeTrackerInternal : IChangeTrackerInternal
    {
        public readonly Dictionary<string, bool> Flags = new();
        public System.Func<string, bool> ImmutableCheck;
        public string LastRejectedFlagId;
        public int RejectCount;

        public void SetFlagRaw(string flagId, bool value)
        {
            Flags[flagId] = value;
        }

        public Dictionary<string, bool> GetAllFlags()
            => new Dictionary<string, bool>(Flags);

        public void SetImmutableFlagCheck(System.Func<string, bool> isImmutableFunc)
        {
            ImmutableCheck = isImmutableFunc;
        }

        // Simulates what ChangeTrackerCore.SetFlag does with IsImmutable guard
        public bool TrySetFlag(string flagId, bool value)
        {
            if (string.IsNullOrEmpty(flagId)) return false;

            if (Flags.TryGetValue(flagId, out bool existing) && existing == value)
                return true; // Idempotent

            // IsImmutable guard
            if (existing && !value && ImmutableCheck != null && ImmutableCheck(flagId))
            {
                LastRejectedFlagId = flagId;
                RejectCount++;
                Debug.LogWarning(
                    $"ChangeTracker: Immutable flag '{flagId}' is already true — " +
                    $"SetFlag(false) rejected.");
                return false; // Rejected
            }

            Flags[flagId] = value;
            return true;
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
    // AC-1: IsImmutable flag rejects SetFlag(false) when current value is true
    // =========================================================================

    [Test]
    public void test_immutable_flag_rejects_set_false_when_currently_true()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", isImmutable: true, defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();

        // Player choice sets flag to true
        fake.TrySetFlag("ch1_letter_kept", true);
        Assert.That(fake.Flags["ch1_letter_kept"], Is.True);

        // Replay with different choice tries to set false
        LogAssert.Expect(LogType.Warning,
            "ChangeTracker: Immutable flag 'ch1_letter_kept' is already true — SetFlag(false) rejected.");

        bool result = fake.TrySetFlag("ch1_letter_kept", false);
        Assert.That(result, Is.False, "SetFlag(false) should be rejected.");
        Assert.That(fake.Flags["ch1_letter_kept"], Is.True,
            "Flag should stay true after rejection.");
        Assert.That(fake.LastRejectedFlagId, Is.EqualTo("ch1_letter_kept"));
        Assert.That(fake.RejectCount, Is.EqualTo(1));
    }

    [Test]
    public void test_non_immutable_flag_allows_set_false()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_window_opened", isImmutable: false, defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();

        fake.TrySetFlag("ch1_window_opened", true);
        Assert.That(fake.Flags["ch1_window_opened"], Is.True);

        // Non-immutable: setting false should succeed
        bool result = fake.TrySetFlag("ch1_window_opened", false);
        Assert.That(result, Is.True);
        Assert.That(fake.Flags["ch1_window_opened"], Is.False);
        Assert.That(fake.RejectCount, Is.EqualTo(0));
    }

    [Test]
    public void test_immutable_flag_set_false_when_currently_false_is_allowed()
    {
        // Immutable only blocks true→false. false→false or false→true are fine.
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", isImmutable: true, defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();

        // Flag starts false, setting false is idempotent
        bool result = fake.TrySetFlag("ch1_letter_kept", false);
        Assert.That(result, Is.True,
            "SetFlag(false) when already false should be idempotent, not rejected.");
        Assert.That(fake.RejectCount, Is.EqualTo(0));
    }

    // =========================================================================
    // AC-2: OnChapterReplayStarted — immutable preserved, non-immutable reset
    // =========================================================================

    [Test]
    public void test_replay_preserves_immutable_flag()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", setInChapter: "ch01", isImmutable: true, defaultValue: false),
            MakeDef("ch1_window_opened", setInChapter: "ch01", isImmutable: false, defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();

        // Player made choices in first playthrough
        fake.TrySetFlag("ch1_letter_kept", true);
        fake.TrySetFlag("ch1_window_opened", true);

        // Simulate replay
        ChapterManager.OnChapterReplayStarted?.Invoke("ch01");

        Assert.That(fake.Flags["ch1_letter_kept"], Is.True,
            "IsImmutable flag should be preserved across replay.");
        Assert.That(fake.Flags["ch1_window_opened"], Is.False,
            "Non-immutable flag should be reset to DefaultValue (false).");
    }

    [Test]
    public void test_replay_only_affects_flags_in_correct_chapter()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", setInChapter: "ch01", isImmutable: false, defaultValue: false),
            MakeDef("ch2_secret", setInChapter: "ch02", isImmutable: false, defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();

        fake.TrySetFlag("ch1_letter_kept", true);
        fake.TrySetFlag("ch2_secret", true);

        // Replay only ch01
        ChapterManager.OnChapterReplayStarted?.Invoke("ch01");

        Assert.That(fake.Flags["ch1_letter_kept"], Is.False,
            "Ch01 flag should be reset on Ch01 replay.");
        Assert.That(fake.Flags["ch2_secret"], Is.True,
            "Ch02 flag should NOT be affected by Ch01 replay.");
    }

    [Test]
    public void test_replay_with_no_matching_flags_is_no_op()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", setInChapter: "ch01", isImmutable: true, defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();
        fake.TrySetFlag("ch1_letter_kept", true);

        // Replay a chapter with no registered flags
        Assert.DoesNotThrow(() =>
            ChapterManager.OnChapterReplayStarted?.Invoke("ch99"));

        Assert.That(fake.Flags["ch1_letter_kept"], Is.True,
            "Replaying unrelated chapter should not affect flags.");
    }

    // =========================================================================
    // AC-3: Non-immutable flag resets to DefaultValue on OnChapterReplayStarted
    // =========================================================================

    [Test]
    public void test_non_immutable_resets_to_default_false()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_window_opened", setInChapter: "ch01", isImmutable: false, defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();
        fake.TrySetFlag("ch1_window_opened", true);

        ChapterManager.OnChapterReplayStarted?.Invoke("ch01");

        Assert.That(fake.Flags["ch1_window_opened"], Is.False,
            "Should reset to DefaultValue (false).");
    }

    [Test]
    public void test_non_immutable_resets_to_default_true()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_found_hint", setInChapter: "ch01", isImmutable: false, defaultValue: true)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();
        Assert.That(fake.Flags["ch1_found_hint"], Is.True, "DefaultValue should be true.");

        // Player changed it to false
        fake.TrySetFlag("ch1_found_hint", false);

        ChapterManager.OnChapterReplayStarted?.Invoke("ch01");

        Assert.That(fake.Flags["ch1_found_hint"], Is.True,
            "Should reset to DefaultValue (true).");
    }

    // =========================================================================
    // AC-4: Idempotent SetFlag(true) on already-true immutable flag
    // =========================================================================

    [Test]
    public void test_idempotent_set_true_on_immutable_flag()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", isImmutable: true, defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();

        fake.TrySetFlag("ch1_letter_kept", true);
        Assert.That(fake.Flags["ch1_letter_kept"], Is.True);

        // Same choice in replay — SetFlag(true) again
        bool result = fake.TrySetFlag("ch1_letter_kept", true);
        Assert.That(result, Is.True,
            "SetFlag(true) when already true should be idempotent.");
        Assert.That(fake.Flags["ch1_letter_kept"], Is.True);
        Assert.That(fake.RejectCount, Is.EqualTo(0),
            "Idempotent set should NOT count as rejection.");
    }

    [Test]
    public void test_rapid_double_set_true_on_immutable_flag()
    {
        var fake = new FakeChangeTrackerInternal();
        var registry = MakeRegistry(
            MakeDef("ch1_letter_kept", isImmutable: true, defaultValue: false)
        );

        var tracker = new CrossChapterTracker(registry, fake);
        tracker.InitializeAllFlags();

        // Both calls succeed — first sets, second is idempotent
        bool r1 = fake.TrySetFlag("ch1_letter_kept", true);
        bool r2 = fake.TrySetFlag("ch1_letter_kept", true);

        Assert.That(r1, Is.True);
        Assert.That(r2, Is.True);
        Assert.That(fake.Flags["ch1_letter_kept"], Is.True);
        Assert.That(fake.RejectCount, Is.EqualTo(0));
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
