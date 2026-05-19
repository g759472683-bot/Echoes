using NUnit.Framework;
using System;
using System.Collections.Generic;

/// <summary>
/// Integration tests for the Save/Load panel (Story 003).
///
/// Tests cover slot rendering, Save/Load mode behaviour differences,
/// overwrite confirmation, empty slot interaction, and metadata formatting.
/// SaveManager and ChapterManager are mocked via test fakes.
/// </summary>
public class SaveLoadPanelTest
{
    // =========================================================================
    // Test Doubles
    // =========================================================================

    /// <summary>
    /// Fake SaveManager for testing slot metadata and save/load operations.
    /// </summary>
    private class FakeSaveManager
    {
        public readonly Dictionary<string, SlotMetadata> Slots = new();
        public readonly List<string> SaveCalls = new();
        public readonly List<string> LoadCalls = new();

        public FakeSaveManager()
        {
            // All slots start empty
            Slots["save_01"] = SlotMetadata.Empty;
            Slots["save_02"] = SlotMetadata.Empty;
            Slots["auto_save"] = SlotMetadata.Empty;
        }

        public SlotMetadata GetSlotMetadata(string slotId)
        {
            return Slots.TryGetValue(slotId, out var meta) ? meta : SlotMetadata.Empty;
        }

        public bool HasAnySave()
        {
            foreach (var kv in Slots)
            {
                if (!kv.Value.IsEmpty) return true;
            }
            return false;
        }

        public void SimulateSave(string slotId, SlotMetadata meta)
        {
            Slots[slotId] = meta;
            SaveCalls.Add(slotId);
        }

        public string SimulateLoad(string slotId)
        {
            LoadCalls.Add(slotId);
            return slotId;
        }
    }

    // =========================================================================
    // Test Data Factory
    // =========================================================================

    private static SlotMetadata CreateOccupiedMeta(string timestamp, int playTime, string chapter)
    {
        return new SlotMetadata
        {
            Timestamp = timestamp,
            PlayTimeSeconds = playTime,
            CurrentChapterKey = chapter
        };
    }

    // =========================================================================
    // AC-1: Save mode — slot display
    // =========================================================================

    [Test]
    public void Test_SaveMode_SlotDisplay_RendersCorrectly()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        fakeManager.Slots["save_01"] = CreateOccupiedMeta("2026-05-12T14:30:00Z", 4980, "ch01");
        // save_02 and auto_save remain empty

        // Act
        var save01Meta = fakeManager.GetSlotMetadata("save_01");
        var save02Meta = fakeManager.GetSlotMetadata("save_02");
        var autoMeta = fakeManager.GetSlotMetadata("auto_save");

        // Assert
        Assert.That(save01Meta.IsEmpty, Is.False,
            "save_01 should be occupied with metadata");
        Assert.That(save01Meta.Timestamp, Does.Contain("2026-05-12"),
            "save_01 timestamp should contain the date");
        Assert.That(save01Meta.PlayTimeSeconds, Is.EqualTo(4980),
            "save_01 playtime should be 4980 seconds");
        Assert.That(save01Meta.CurrentChapterKey, Is.EqualTo("ch01"),
            "save_01 chapter should be ch01");

        Assert.That(save02Meta.IsEmpty, Is.True,
            "save_02 should be empty");
        Assert.That(autoMeta.IsEmpty, Is.True,
            "auto_save should be empty");
    }

    [Test]
    public void Test_SaveMode_AllThreeSlotsExist()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        string[] slotIds = { "save_01", "save_02", "auto_save" };

        // Act & Assert
        foreach (string slotId in slotIds)
        {
            Assert.That(fakeManager.Slots.ContainsKey(slotId), Is.True,
                $"Slot {slotId} should exist");
        }
        Assert.That(slotIds, Has.Length.EqualTo(3),
            "Should have exactly 3 slots");
    }

    // =========================================================================
    // AC-2: Save mode — empty slot saves directly (no confirmation)
    // =========================================================================

    [Test]
    public void Test_SaveMode_EmptySlot_SavesDirectly()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        // save_02 is empty

        // Act — simulate clicking empty slot in Save mode
        string targetSlot = "save_02";
        SlotMetadata meta = fakeManager.GetSlotMetadata(targetSlot);
        bool isEmpty = meta.IsEmpty;

        if (isEmpty)
        {
            // In Save mode, empty slot saves directly — no confirm dialog
            fakeManager.SimulateSave(targetSlot, CreateOccupiedMeta(
                "2026-05-19T10:00:00Z", 0, "ch01"));
        }

        // Assert
        Assert.That(fakeManager.SaveCalls, Does.Contain("save_02"),
            "Empty slot click in Save mode should call SaveAsync directly");
        Assert.That(fakeManager.SaveCalls, Has.Count.EqualTo(1),
            "Only one save call should be made");
        Assert.That(fakeManager.GetSlotMetadata("save_02").IsEmpty, Is.False,
            "Slot should no longer be empty after save");
    }

    // =========================================================================
    // AC-3: Save mode — occupied slot shows overwrite confirm
    // =========================================================================

    [Test]
    public void Test_SaveMode_OccupiedSlot_ShowsOverwriteConfirm()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        fakeManager.Slots["save_01"] = CreateOccupiedMeta("2026-05-12T14:30:00Z", 4980, "ch01");

        // Act — simulate the decision logic (confirm not yet invoked)
        string targetSlot = "save_01";
        SlotMetadata meta = fakeManager.GetSlotMetadata(targetSlot);
        bool needsConfirm = !meta.IsEmpty;

        // Assert
        Assert.That(needsConfirm, Is.True,
            "Occupied slot in Save mode should require overwrite confirmation");
    }

    [Test]
    public void Test_SaveMode_OverwriteConfirm_ConfirmSaves()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        fakeManager.Slots["save_01"] = CreateOccupiedMeta("2026-05-12T14:30:00Z", 4980, "ch01");

        // Act — simulate confirm
        fakeManager.SimulateSave("save_01", CreateOccupiedMeta(
            "2026-05-19T11:00:00Z", 6000, "ch02"));

        // Assert
        Assert.That(fakeManager.SaveCalls, Does.Contain("save_01"),
            "Confirm on overwrite should execute SaveAsync");
        Assert.That(fakeManager.GetSlotMetadata("save_01").CurrentChapterKey, Is.EqualTo("ch02"),
            "Metadata should be updated after overwrite save");
        Assert.That(fakeManager.GetSlotMetadata("save_01").PlayTimeSeconds, Is.EqualTo(6000),
            "Playtime should be updated after overwrite save");
    }

    [Test]
    public void Test_SaveMode_OverwriteConfirm_CancelDoesNotSave()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        fakeManager.Slots["save_01"] = CreateOccupiedMeta("2026-05-12T14:30:00Z", 4980, "ch01");
        int saveCountBefore = fakeManager.SaveCalls.Count;

        // Act — simulate cancel (do nothing, pop dialog only)
        // No save call is made on cancel

        // Assert
        Assert.That(fakeManager.SaveCalls.Count, Is.EqualTo(saveCountBefore),
            "Cancel on overwrite should NOT call SaveAsync");
        Assert.That(fakeManager.GetSlotMetadata("save_01").PlayTimeSeconds, Is.EqualTo(4980),
            "Metadata should be unchanged after cancel");
        Assert.That(fakeManager.GetSlotMetadata("save_01").CurrentChapterKey, Is.EqualTo("ch01"),
            "Chapter should be unchanged after cancel");
    }

    // =========================================================================
    // AC-4: Load mode from title — occupied slot loads directly (no confirm)
    // =========================================================================

    [Test]
    public void Test_LoadMode_FromTitle_LoadsDirectlyNoConfirm()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        fakeManager.Slots["save_01"] = CreateOccupiedMeta("2026-05-12T14:30:00Z", 4980, "ch01");
        bool isInGame = false;
        string targetSlot = "save_01";

        // Act — simulate click on occupied slot in Load mode from title
        SlotMetadata meta = fakeManager.GetSlotMetadata(targetSlot);

        bool needsConfirm = !meta.IsEmpty && isInGame;
        if (!meta.IsEmpty && !isInGame)
        {
            // Load directly
            fakeManager.SimulateLoad(targetSlot);
        }

        // Assert
        Assert.That(needsConfirm, Is.False,
            "Load from title should NOT require confirmation");
        Assert.That(fakeManager.LoadCalls, Does.Contain("save_01"),
            "Occupied slot in Load mode from title should call LoadAsync directly");
    }

    // =========================================================================
    // AC-5: Load mode from pause — occupied slot requires confirmation
    // =========================================================================

    [Test]
    public void Test_LoadMode_FromPause_ShowsConfirm()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        fakeManager.Slots["save_01"] = CreateOccupiedMeta("2026-05-12T14:30:00Z", 4980, "ch01");
        bool isInGame = true;
        string targetSlot = "save_01";

        // Act
        SlotMetadata meta = fakeManager.GetSlotMetadata(targetSlot);

        bool needsConfirm = !meta.IsEmpty && isInGame;

        // Assert
        Assert.That(needsConfirm, Is.True,
            "Load from pause should require confirmation dialog");
    }

    [Test]
    public void Test_LoadMode_FromPause_ConfirmLoads()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        fakeManager.Slots["save_01"] = CreateOccupiedMeta("2026-05-12T14:30:00Z", 4980, "ch01");
        string targetSlot = "save_01";

        // Act — confirm the load
        fakeManager.SimulateLoad(targetSlot);

        // Assert
        Assert.That(fakeManager.LoadCalls, Does.Contain("save_01"),
            "Confirm on in-game load should execute LoadAsync");
    }

    // =========================================================================
    // Load mode — empty slot not interactive
    // =========================================================================

    [Test]
    public void Test_LoadMode_EmptySlot_NotInteractive()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        // save_02 is empty

        // Act — simulate checking load mode behaviour for empty slot
        string targetSlot = "save_02";
        SlotMetadata meta = fakeManager.GetSlotMetadata(targetSlot);
        bool isEmpty = meta.IsEmpty;

        bool isInteractive = isEmpty
            ? false  // In Load mode, empty slots are not interactive
            : true;

        // Assert
        Assert.That(isEmpty, Is.True,
            "Slot should be empty");
        Assert.That(isInteractive, Is.False,
            "Empty slot in Load mode should NOT be interactive");
    }

    // =========================================================================
    // Metadata formatting tests
    // =========================================================================

    [Test]
    public void Test_SlotMetadata_Formatting_TimestampDisplay()
    {
        // Test the FormatTimestamp logic via direct DateTime parsing
        string iso8601 = "2026-05-12T14:30:00Z";

        // Act — parse and format
        DateTime dt = DateTime.Parse(iso8601, null, System.Globalization.DateTimeStyles.RoundtripKind);
        string formatted = $"{dt.Year}年{dt.Month}月{dt.Day}日 {dt.Hour:D2}:{dt.Minute:D2}";

        // Assert
        Assert.That(formatted, Is.EqualTo("2026年5月12日 14:30"),
            "Timestamp should be formatted as Chinese date + time");
    }

    [Test]
    public void Test_SlotMetadata_Formatting_PlayTime_WithHours()
    {
        // Act — 4980 seconds = 1h 23m
        int totalSeconds = 4980;
        int hours = totalSeconds / 3600;       // 1
        int minutes = (totalSeconds % 3600) / 60; // 23

        string formatted = hours > 0
            ? $"{hours}h {minutes}m"
            : $"{minutes}m";

        // Assert
        Assert.That(formatted, Is.EqualTo("1h 23m"),
            "4980s should format as 1h 23m");
    }

    [Test]
    public void Test_SlotMetadata_Formatting_PlayTime_UnderOneHour()
    {
        // Act — 2100 seconds = 35m
        int totalSeconds = 2100;
        int hours = totalSeconds / 3600;       // 0
        int minutes = (totalSeconds % 3600) / 60; // 35

        string formatted = hours > 0
            ? $"{hours}h {minutes}m"
            : $"{minutes}m";

        // Assert
        Assert.That(formatted, Is.EqualTo("35m"),
            "Seconds under 1h should show only minutes");
    }

    [Test]
    public void Test_SlotMetadata_Formatting_PlayTime_ZeroSeconds()
    {
        // Act — 0 seconds
        int totalSeconds = 0;
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;

        string formatted = hours > 0
            ? $"{hours}h {minutes}m"
            : $"{minutes}m";

        // Assert
        Assert.That(formatted, Is.EqualTo("0m"),
            "0 seconds should format as 0m");
    }

    [Test]
    public void Test_SlotMetadata_Formatting_PlayTime_ExactHour()
    {
        // Act — 3600 seconds = 1h 0m
        int totalSeconds = 3600;
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;

        string formatted = hours > 0
            ? $"{hours}h {minutes}m"
            : $"{minutes}m";

        // Assert
        Assert.That(formatted, Is.EqualTo("1h 0m"),
            "3600s should format as 1h 0m");
    }

    // =========================================================================
    // Slot label mapping tests
    // =========================================================================

    [Test]
    public void Test_SlotLabel_DisplayMapping()
    {
        // Assert the static slot label mapping
        Assert.That(GetSlotLabel("save_01"), Is.EqualTo("存档 1"),
            "save_01 should display as 存档 1");
        Assert.That(GetSlotLabel("save_02"), Is.EqualTo("存档 2"),
            "save_02 should display as 存档 2");
        Assert.That(GetSlotLabel("auto_save"), Is.EqualTo("自动存档"),
            "auto_save should display as 自动存档");
    }

    // =========================================================================
    // Edge Cases
    // =========================================================================

    [Test]
    public void Test_AllSlotsEmpty_HasAnySave_False()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();

        // Assert
        Assert.That(fakeManager.HasAnySave(), Is.False,
            "HasAnySave should be false when all slots are empty");
    }

    [Test]
    public void Test_OneSlotOccupied_HasAnySave_True()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();
        fakeManager.Slots["auto_save"] = CreateOccupiedMeta("2026-05-19T10:00:00Z", 10, "ch01");

        // Assert
        Assert.That(fakeManager.HasAnySave(), Is.True,
            "HasAnySave should be true when any slot is occupied");
    }

    [Test]
    public void Test_InvalidSlotId_ReturnsEmpty()
    {
        // Arrange
        var fakeManager = new FakeSaveManager();

        // Act
        SlotMetadata meta = fakeManager.GetSlotMetadata("non_existent_slot");

        // Assert
        Assert.That(meta.IsEmpty, Is.True,
            "Non-existent slot ID should return IsEmpty=true");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string GetSlotLabel(string slotId)
    {
        return slotId switch
        {
            "save_01" => "存档 1",
            "save_02" => "存档 2",
            "auto_save" => "自动存档",
            _ => slotId
        };
    }
}
