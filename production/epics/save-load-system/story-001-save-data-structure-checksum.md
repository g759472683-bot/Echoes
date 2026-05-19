# Story 001: SaveData 结构 + SHA-256 校验和

> **Epic**: 存档系统 (SaveManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/save-load-system.md`
**Requirement**: `TR-save-load-system-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0003: 存档序列化格式与版本迁移策略
**ADR Decision Summary**: JSON + SHA-256 校验 + 原子写入 (.tmp → .sav) + 3 存档槽位 + 版本迁移链

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `System.Text.Json` 在 IL2CPP 中需验证 Dictionary<string, ContentOverrides> 的多态类型序列化正确性

**Control Manifest Rules (Foundation Layer)**:
- Required: JSON + SHA-256 checksum for save files — System.Text.Json, atomic write (.tmp → .sav), version migration chain
- Forbidden: Never serialize base SO data into save files — save only overlay + flags
- Guardrail: Save file write: <200ms (JSON serialize + SHA-256 + atomic write)

---

## Acceptance Criteria

*From GDD `design/gdd/save-load-system.md`, scoped to this story:*

- [ ] `SaveData` struct 包含所有必需字段：Version, Timestamp, LocaleCode, PlayTimeSeconds, CurrentChapterKey, CurrentFragmentId, CurrentFragmentIndex, CompletedChapters, UnlockedChapters, ChangeOverlay, CrossChapterFlags, 音量设置, TriggeredEndingConditionIds, Checksum
- [ ] `ComputeChecksum(SaveData data)` 计算所有字段（除 Checksum 本身）的 JSON SHA-256 哈希——返回 hex 字符串
- [ ] `ValidateChecksum(SaveData data)` 重新计算并比对——不匹配时抛出 `SaveCorruptedException`
- [ ] GIVEN 存档文件的 Checksum 与内容不匹配（文件损坏），WHEN 尝试加载，THEN 显示"存档文件已损坏"提示，不尝试部分恢复

---

## Implementation Notes

*Derived from ADR-0003:*

SaveData struct (精简——详细字段定义见 GDD 规则 2):
```csharp
[Serializable]
public struct SaveData
{
    public int Version;
    public string Timestamp;       // ISO 8601 UTC
    public string LocaleCode;
    public int PlayTimeSeconds;
    // ... 章节进度、ChangeOverlay、CrossChapterFlags、音量、结局条件
    
    [JsonIgnore]  // 不参与 JSON 序列化——单独计算
    public string Checksum;
}
```

SHA-256 校验和:
```csharp
public static string ComputeChecksum(SaveData data)
{
    // 创建不含 Checksum 的副本
    var temp = data;
    temp.Checksum = null;
    
    var json = JsonSerializer.Serialize(temp, _jsonOptions);
    using var sha256 = SHA256.Create();
    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
    return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
}

public static void ValidateChecksum(SaveData data)
{
    var expected = ComputeChecksum(data);
    if (!string.Equals(expected, data.Checksum, StringComparison.OrdinalIgnoreCase))
        throw new SaveCorruptedException(
            $"Checksum mismatch: expected {expected}, got {data.Checksum}");
}

public class SaveCorruptedException : Exception
{
    public SaveCorruptedException(string message) : base(message) { }
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 原子文件 I/O + 3 槽位管理 — 文件写入/读取/原子替换
- Story 003: 收集/恢复编排 — CollectSaveData, RestoreSaveData
- Story 004: 自动存档引擎 — 触发点、防抖

---

## QA Test Cases

- **AC-1**: SaveData 序列化后可反序列化
  - Given: 一个填充了测试数据的 SaveData struct
  - When: `JsonSerializer.Serialize(saveData)` 然后 `JsonSerializer.Deserialize<SaveData>(json)`
  - Then: 反序列化的结构与原始结构所有字段值相等（Checksum 除外）
  - Edge cases: Dictionary 字段为空 → 序列化为 "{}"；null 数组 → 序列化为 "null"

- **AC-2**: SHA-256 校验和计算一致
  - Given: 相同内容的 SaveData
  - When: 调用 `ComputeChecksum` 两次
  - Then: 两次返回相同的 hex 字符串
  - Edge cases: 任何字段改变 → 校验和不同；Checksum 字段本身改变 → 不改变校验和（被排除）

- **AC-3**: ValidateChecksum 不匹配时抛异常
  - Given: SaveData 内容被篡改（Checksum 与内容不符）
  - When: 调用 `ValidateChecksum(saveData)`
  - Then: 抛出 `SaveCorruptedException`；消息包含预期和实际的 checksum
  - Edge cases: Checksum 为 null → 抛异常；Checksum 为空字符串 → 抛异常

- **AC-4**: 损坏存档不部分恢复
  - Given: 加载流程中 `ValidateChecksum` 失败
  - When: SaveCorruptedException 被捕获
  - Then: 不调用任何 `RestoreFromSave` 方法；显示错误提示；游戏停留在主菜单
  - Edge cases: 玩家选择"返回"→ 回到主菜单；不残留任何已恢复的状态

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/save-load-system/checksum_test.cs` — must exist and pass

**Status**: [x] Created (15 test functions, all 4 ACs covered)

---

## Dependencies

- Depends on: None
- Unlocks: Story 002 (原子 I/O 需要 SaveData struct + Checksum)

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 4/4 passing — all auto-verified via unit tests
**Deviations**: ADVISORY — `SaveCorruptedException` vs ADR-0003 sketch `SaveCorruptionException` (intentional story-level simplification)
**Test Evidence**: Logic — `tests/unit/save-load-system/checksum_test.cs` (15 test functions)
**Code Review**: Complete — APPROVED (unity-specialist + qa-tester, no blocking issues)
**Engine Notes**: IL2CPP `JsonSerializerContext` source generator needed before IL2CPP builds (tracked in ADR-0003 risk table)
