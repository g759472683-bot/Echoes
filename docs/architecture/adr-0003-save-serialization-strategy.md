# ADR-0003: 存档序列化格式与版本迁移策略

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

游戏需要保存/恢复玩家进度、选择累积、跨章节状态等。决定使用 JSON 序列化 + SHA-256 校验 + 原子写入 (.tmp → .sav) + 3 存档槽位 + 版本迁移链。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — 纯 C# I/O，不依赖 Unity 特定 API |
| **References Consulted** | `VERSION.md`, `current-best-practices.md` |
| **Post-Cutoff APIs Used** | `System.Text.Json` (C# 9, .NET Standard 2.1+) |
| **Verification Required** | IL2CPP 中 `System.Text.Json` 序列化 `Dictionary<string, ContentOverrides>` 的多态类型正确性 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0007 (SO + overlay 模式 — 存档只序列化 overlay，不序列化 base SO) |
| **Enables** | ADR-0011 (跨章节状态追踪 — CrossChapterFlags 序列化) |
| **Blocks** | MainMenu + ChapterManager Epic — 存档功能未就绪前无法开始 |
| **Ordering Note** | 在 ADR-0007 之后创建（依赖 overlay 序列化格式） |

## Context

### Problem Statement

回响 (Echoes) 的玩家进度跨多个章节累积：选择产生的 ContentOverlay、跨章标记、解锁结局等。存档必须在玩家退出后完整恢复，容忍写入中断（断电/崩溃），支持未来版本格式升级。

### Constraints

- 3 个存档槽位 (save_01, save_02, auto_save)
- 存档文件大小目标 < 500KB（纯文本 JSON）
- 存档完整写入时间 < 100ms
- 必须防止写入中断导致存档损坏
- 游戏更新后必须能读取旧版本存档
- `Application.persistentDataPath` 为目标路径（Unity 标准）

### Requirements

- 统一 SaveData 结构体聚合 6 个系统的状态
- SHA-256 校验和防损坏
- 原子写入（先写临时文件，成功后重命名）
- 版本号 + 迁移链（version N 存档自动迁移到 version N+1）
- 存档槽位元数据快速扫描（不反序列化完整数据）

## Decision

**使用 JSON + SHA-256 + 原子写入 + 版本迁移链。**

### SaveData 结构

```csharp
[Serializable]
public struct SaveData
{
    public int Version;                              // 当前存档格式版本
    public DateTime Timestamp;                       // 保存时间
    public string CurrentChapterKey;                 // 当前章节
    public string CurrentFragmentId;                 // 当前碎片
    public List<string> VisitedFragments;            // 已访问碎片
    public List<string> CompletedChapters;           // 已完成章节
    public Dictionary<string, bool> Flags;           // ChangeTracker flags
    public Dictionary<string, ContentOverrides> Overlay; // ChangeTracker overlay
    public Dictionary<string, bool> CrossChapterFlags;   // 跨章标记
    public List<string> UnlockedEndingIds;           // 解锁结局
    public string LocaleCode;                        // 语言设置
    public VolumeSettings Volume;                    // 音量设置
}

[Serializable]
public struct VolumeSettings
{
    public float Master;
    public float SFX;
    public float Music;
    public float Ambience;
}

[Serializable]
public struct SlotMetaData
{
    public DateTime Timestamp;
    public string ChapterName;      // 本地化章节名
    public int VisitedCount;
    public TimeSpan PlayTime;
}
```

### 原子写入流程

```
SaveAsync(slotId)
  │
  ├─ 1. SaveData = CollectSaveData()        ← 聚合 6 个系统
  ├─ 2. json = JsonSerializer.Serialize(SaveData)
  ├─ 3. hash = SHA256.ComputeHash(json)
  ├─ 4. WriteAllText("save_01.tmp", json + hash)
  ├─ 5. File.Move("save_01.tmp", "save_01.sav")  ← 原子操作
  └─ 6. 成功 → 返回; 失败 → 保留旧 .sav
```

### 版本迁移链

```csharp
private static readonly Dictionary<int, Func<JsonDocument, JsonDocument>> Migrations = new()
{
    // 示例：v1 → v2 添加新字段
    [1] = doc => {
        var root = doc.RootElement.Clone();
        // 添加 v2 新字段的默认值
        // root.Add("CrossChapterFlags", new JsonObject());
        return root;
    },
};

public SaveData LoadWithMigration(string path)
{
    var json = File.ReadAllText(path);
    var doc = JsonDocument.Parse(json);
    var version = doc.RootElement.GetProperty("Version").GetInt32();

    while (Migrations.ContainsKey(version))
    {
        doc = Migrations[version](doc);
        version++;
    }

    return JsonSerializer.Deserialize<SaveData>(doc.RootElement.GetRawText());
}
```

### Architecture Diagram

```
┌───────────────────────────────────────────┐
│              ISaveManager                   │
│                                            │
│  SaveAsync(slotId)                         │
│    CollectSaveData() ──聚合──► SaveData    │
│    Serialize ──► JSON                      │
│    SHA256 ──► checksum                     │
│    Write .tmp ──► File.Move ──► .sav       │
│                                            │
│  LoadAsync(slotId)                         │
│    Read .sav ──► Verify SHA256             │
│    Deserialize ──► Version Check           │
│    Migrate if needed ──► SaveData          │
│    → ChapterManager.LoadAndRestore()       │
└───────────────────────────────────────────┘
         │
         ▼
┌───────────────────────────────────────────┐
│  持久化路径 (Application.persistentDataPath) │
│  /saves/                                   │
│  ├─ save_01.sav                            │
│  ├─ save_02.sav                            │
│  ├─ auto_save.sav                          │
│  └─ save_01.meta  (SlotMetaData, 独立文件)  │
└───────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public interface ISaveManager
{
    Task SaveAsync(int slotId);
    Task<SaveData> LoadAsync(int slotId);
    bool HasAnySave();
    SlotMetaData GetSlotMetaData(int slotId);
    SaveData CollectSaveData();
    void RestoreSaveData(SaveData data);
}

public class SaveCorruptionException : Exception
{
    public string FilePath { get; }
    public SaveCorruptionException(string path, string reason)
        : base($"Save file corrupted: {path} — {reason}") { }
}
```

### Implementation Guidelines

1. 元数据独立存储为 `.meta` 文件 — 扫描存档列表时不反序列化完整 SaveData
2. 写入前校验收集到的状态一致性（不在转场中保存）
3. 版本迁移是无损的 — 旧版本存档在迁移后不覆盖源文件
4. 校验失败时保留旧 .sav（不覆盖），返回 `SaveCorruptionException`
5. `CollectSaveData` 由 ChapterManager 在保存前调用，确保状态一致

## Alternatives Considered

### Alternative 1: BinaryFormatter / .NET Binary Serialization

- **Description**: 使用 .NET 内置二进制序列化
- **Pros**: 文件更小；序列化更快
- **Cons**: BinaryFormatter 在 .NET 5+ 已被标记为 obsolete（安全风险）；二进制格式不可调试（玩家反馈存档问题无法自查）；版本迁移困难
- **Rejection Reason**: BinaryFormatter 已废弃且不安全；JSON 可读性对调试非常重要

### Alternative 2: Unity PlayerPrefs

- **Description**: 使用 Unity 内置 PlayerPrefs（注册表/plist 存储）
- **Pros**: 零代码；Unity 内置
- **Cons**: 仅支持 int/float/string；无嵌套结构；Windows 上存注册表（不可移植）；无校验和；不适合 500KB 数据
- **Rejection Reason**: 不支持复杂数据结构，无完整性校验

## Consequences

### Positive

- 人可读 JSON（调试友好，玩家可检查存档）
- 原子写入保证断电/崩溃安全
- 版本迁移支持长期迭代
- 元数据独立文件支持快速存档列表展示
- SHA-256 检测到任何数据损坏

### Negative

- JSON 比二进制大 2-3 倍（但 500KB 预算内仍充裕）
- 序列化 Dictionary 需要自定义 JsonConverter（多态类型处理）
- 版本迁移链需要每个版本手动维护
- `System.Text.Json` 在 IL2CPP 中可能有限制（需验证）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| IL2CPP 中 Dictionary<string, ContentOverrides> 序列化失败 | Low | High | Pre-Production IL2CPP 测试；准备 Newtonsoft.Json 回退方案 |
| 版本迁移逻辑错误导致存档数据丢失 | Low | High | 每次迁移有单元测试；迁移前备份原文件 |
| 存档文件被用户手动编辑破坏 | Low | Medium | SHA-256 检测 → 提示存档损坏，不复原 |
| SaveData 随系统增长超过 500KB | Medium | Low | 监控大小；如需要可压缩 (GZip) |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (SaveAsync, 含序列化) | < 50ms |
| CPU (LoadAsync, 含反序列化+迁移) | < 50ms |
| Memory (SaveData 对象) | ~50-200KB (取决于 overlay 大小) |
| I/O (写入 .sav) | < 10ms (SSD, < 500KB) |
| I/O (读取 .sav) | < 5ms (SSD) |

## Migration Plan

新建项目，无迁移需求。未来版本迁移步骤：
1. 新版本修改 SaveData 结构 → 递增 Version 字段
2. 在 Migration dictionary 添加迁移函数
3. 添加迁移单元测试（旧版本 .sav 作为 fixture）

## Validation Criteria

- [ ] 写入 .tmp 中途崩溃 → .sav 不受影响（保留上一个有效版本）
- [ ] SHA-256 不匹配 → 拒绝加载，返回 `SaveCorruptionException`
- [ ] v1 存档 → 经迁移链 → 正确反序列化为当前 SaveData
- [ ] `HasAnySave()` 仅检查 .sav 文件存在，不读取内容
- [ ] 3 个槽位独立，互不影响

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `save-load-system.md` (#7) | 存档系统 | 3 个存档槽位 | save_01/save_02/auto_save |
| `save-load-system.md` (#7) | 存档系统 | SHA-256 校验和 | 写入时计算，读取时验证 |
| `save-load-system.md` (#7) | 存档系统 | 原子写入防损坏 | .tmp → .sav File.Move |
| `save-load-system.md` (#7) | 存档系统 | 版本迁移机制 | Migration chain |
| `memory-change-tracking.md` (#12) | 变化追踪 | Overlay 序列化 | SaveData.Overlay Dictionary |
| `cross-chapter-state.md` (#16) | 跨章状态 | CrossChapterFlags 持久化 | SaveData.CrossChapterFlags |
| `multi-ending-system.md` (#14) | 多结局 | 解锁结局 ID 持久化 | SaveData.UnlockedEndingIds |

## Related

- ADR-0007 — SO + overlay 模式（存档不序列化 base SO）
- ADR-0011 — 跨章节状态（CrossChapterFlags 结构）
- `docs/architecture/architecture.md` §4.1 — ISaveManager API 边界
