# ADR-0011: 跨章节状态追踪 — Immutable Flag 与跨周目保护

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

某些玩家选择的影响跨越多章（章节 1 的决策影响章节 4 的事件）。决定使用 CrossChapterFlagRegistry ScriptableObject + IsImmutable 保护 + 新游戏/章节重玩初始化语义。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — 纯 C# 数据结构 + ScriptableObject |
| **References Consulted** | `VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0007 (ChangeTracker — 通过 SetFlagRaw/GetFlag 直接读写 _flags Dictionary), ADR-0003 (存档 — CrossChapterFlags 序列化) |
| **Enables** | None |
| **Blocks** | CrossChapterTracker Epic |
| **Ordering Note** | 在 ADR-0003, ADR-0007 之后实现 |

## Context

### Problem Statement

回响 (Echoes) 的叙事跨越 5+ 章，某些早期选择需要影响后期内容：

- Ch.1 选择是否揭示一个秘密 → Ch.4 角色是否信任玩家
- Ch.2 一个角色的存活 → Ch.5 结尾是否出现

这些跨章标记需要：
- 集中定义（设计师知道哪些选择会跨章影响）
- 退出时持久化
- 章节重玩时不被误重置（IsImmutable 保护）

### Constraints

- Flag 值类型为 bool（简单、可序列化）
- 必须在 ScriptableObject 中定义所有可能的跨章标记
- IsImmutable flag 一旦设置为 true→不可恢复 false（新游戏除外）
- Flag 定义包含：ID, 设置位置, 消费位置, 默认值, 是否不可变

### Requirements

- CrossChapterFlagRegistry SO 集中注册所有跨章标记
- ChangeTracker SetFlagRaw 内部接口（与 _flags 共享 Dictionary）
- IsImmutable 保护（true 后不得被章节重玩覆盖为 false）
- 新游戏初始化所有 flag 为默认值
- 存档包含 CrossChapterFlags Dictionary

## Decision

**CrossChapterFlagRegistry SO + IsImmutable 保护 + ChangeTracker._flags 共享存储。**

### Flag 定义

```csharp
[CreateAssetMenu(menuName = "Echoes/CrossChapterFlagRegistry")]
public class CrossChapterFlagRegistry : ScriptableObject
{
    public CrossChapterFlagDef[] Flags;
}

[Serializable]
public struct CrossChapterFlagDef
{
    public string FlagId;            // 全局唯一, e.g. "mentor_alive"
    public string SetInChapter;      // 哪章设置, e.g. "chapter_1"
    public string SetInFragmentId;   // 哪个碎片设置
    public string SetByChoiceId;     // 哪个选项设置
    public bool IsImmutable;         // true = 一旦设置不可还原
    public bool DefaultValue;        // 新游戏默认值
    public string ConsumedBy;        // 哪章/碎片消费此标记 (文档用途)
}
```

### IsImmutable 保护

```csharp
public class CrossChapterTracker
{
    private CrossChapterFlagRegistry _registry;
    private IChangeTracker _changeTracker; // SetFlagRaw 内部接口

    public void InitializeAllFlags()
    {
        foreach (var def in _registry.Flags)
        {
            _changeTracker.SetFlagRaw(def.FlagId, def.DefaultValue);
        }
    }

    // 由 ChapterManager 在 OnChapterReplayStarted 时调用
    public void OnChapterReplayStarted(string chapterKey)
    {
        var flagsInThisChapter = _registry.Flags
            .Where(f => f.SetInChapter == chapterKey);

        foreach (var def in flagsInThisChapter)
        {
            if (def.IsImmutable)
            {
                // 保护：不变标记不重置
                // 章节重玩开始时 IsImmutable flag 保持不变
                continue;
            }

            // 非不可变标记：重置为默认值
            _changeTracker.SetFlagRaw(def.FlagId, def.DefaultValue);
        }
    }

    public Dictionary<string, bool> GetPersistableFlags()
    {
        // 返回所有 flag 当前值（供 SaveManager 序列化）
        return _changeTracker.GetAllFlags()
            .Where(kv => _registry.Flags.Any(f => f.FlagId == kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public void RestoreFlags(Dictionary<string, bool> savedFlags)
    {
        foreach (var (flagId, value) in savedFlags)
        {
            _changeTracker.SetFlagRaw(flagId, value);
        }
    }
}
```

### 生命周期

```
新游戏:
  InitializeAllFlags() → 所有 flag = DefaultValue

章节中 (玩家做选择):
  ContentChange 触发 → ChangeTracker.SetFlag("mentor_alive", true)
  → _flags["mentor_alive"] = true

章节完成:
  IsImmutable? → true: 值锁定, 不可还原

章节重玩 (OnChapterReplayStarted):
  IsImmutable = false 的 flag → 重置为 DefaultValue
  IsImmutable = true 的 flag → 保持不变 (保护)

全游戏完成 + 新周目:
  新游戏 → 所有 flag 重新初始化 (包括 IsImmutable)
```

### Architecture Diagram

```
┌────────────────────────────────────────────┐
│  CrossChapterFlagRegistry (SO)             │
│  ├─ Flags: CrossChapterFlagDef[]           │
│  │   ├─ FlagId: "mentor_alive"             │
│  │   ├─ SetInChapter: "chapter_1"          │
│  │   ├─ IsImmutable: true                  │
│  │   └─ DefaultValue: false                │
│  └─ ...                                    │
└────────────────────────────────────────────┘
         │
         ▼
┌────────────────────────────────────────────┐
│  CrossChapterTracker                        │
│                                            │
│  InitializeAllFlags()                       │
│    └─ SetFlagRaw(id, defaultValue)  (全部)  │
│                                            │
│  OnChapterReplayStarted(chapterKey)         │
│    ├─ IsImmutable flags → 跳过 (保护)       │
│    └─ 非 Immutable → 重置为 default         │
│                                            │
│  GetPersistableFlags() → SaveData           │
│  RestoreFlags(Dictionary) ← SaveData        │
└────────────────────────────────────────────┘
         │
         ▼
┌────────────────────────────────────────────┐
│  ChangeTracker._flags (共享 Dictionary)     │
│  ├─ SetFlagRaw(flagId, value)              │
│  ├─ GetAllFlags() → Dictionary             │
│  └─ GetFlag(flagId) → bool                 │
│                                            │
│  同一 _flags Dictionary 包含:               │
│  ├─ 章节内 Flag (ChapterManager 管理)       │
│  └─ 跨章 Flag (CrossChapterTracker 管理)    │
└────────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public interface ICrossChapterTracker
{
    void InitializeAllFlags();
    Dictionary<string, bool> GetPersistableFlags();
    void RestoreFlags(Dictionary<string, bool> flags);
}

// ChangeTracker 内部接口 (仅 CrossChapterTracker 调用)
// IChangeTracker 公开接口不暴露 SetFlagRaw
internal interface IChangeTrackerInternal
{
    void SetFlagRaw(string flagId, bool value);
}
```

### Implementation Guidelines

1. Flag 命名规范: `snake_case`, 全局唯一前缀 (如 `ch1_`, `ch2_`)
2. CrossChapterFlagRegistry 在 Addressables 中存储（同其他 SO）
3. `SetFlagRaw` 不触发 `OnOverlayChanged` 事件（跨章标记的读写不应产生 HUD 刷新）
4. IsImmutable 保护在 `OnChapterReplayStarted` 中落实
5. 新游戏开头调用 `InitializeAllFlags()`（由 #15 ChapterManager.StartNewGame 间接触发）

## Alternatives Considered

### Alternative 1: 所有 Flag 存储在 ChangeTracker（无独立 Registry）

- **Description**: 不使用 CrossChapterFlagRegistry SO，所有 flag 由 ContentChange 直接 SetFlag，ChangeTracker 自己分辨哪些是跨章的
- **Pros**: 代码更少（去掉 CrossChapterTracker）
- **Cons**: 设计师无法集中看到"哪些选择是跨章的"；无 IsImmutable 约束（运行时无法分辨哪些不应重置）；存档时不知道哪些 flag 需要持久化
- **Rejection Reason**: CrossChapterFlagRegistry 提供集中定义和 IsImmutable 强制保护，这两者都是关键设计需求

### Alternative 2: 完全隔离的存储（与 ChangeTracker._flags 分开 Dictionary）

- **Description**: CrossChapterTracker 维护独立的 `_crossFlags` Dictionary，与 ChangeTracker._flags 完全隔离
- **Pros**: 隔离更干净
- **Cons**: 两处存储 → 同步问题；设计文档和 ContentChange 无法直接使用跨章标记（需要额外同步步骤）
- **Rejection Reason**: 共享 _flags Dictionary 更简单（单一事实来源），SetFlagRaw 内部接口解决耦合

## Consequences

### Positive

- 集中注册：CrossChapterFlagRegistry SO 提供完整的跨章标记目录
- IsImmutable 自动保护：章节重玩时不会破坏关键叙事标记
- 共享存储：不产生两次查找（跨章标记 = _flags 中的条目）
- 存档中自动包含（_flags 已在 SaveData 中）

### Negative

- SetFlagRaw 内部接口打破了 IChangeTracker 封装
- 设计师需理解 IsImmutable 概念并正确配置
- 跨章标记和章节内标记混在同一 _flags Dictionary 中

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| IsImmutable 标记被错误地设为 true → 无法还原 | Low | Medium | Editor 验证工具；Playtest 测试关键路径 |
| 跨章标记命名冲突（两个章节用同一 FlagId） | Low | Medium | Flag Registry 验证重复 ID |
| 章节重玩时 IsImmutable 保护未生效 | Low | High | 单元测试覆盖 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (InitializeAllFlags, ~30 flags) | < 0.1ms (Dictionary insert) |
| CPU (OnChapterReplayStarted) | < 0.1ms |
| Memory (CrossChapterFlagRegistry SO) | ~2KB |
| Memory (30 flags in _flags Dictionary) | ~1.5KB |
| GC Allocation | 0 (无每次调用的分配) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] 新游戏 → 所有 flag = DefaultValue
- [ ] IsImmutable flag 设为 true 后，章节重玩不重置它
- [ ] 非 IsImmutable flag 在章节重玩时重置为 DefaultValue
- [ ] GetPersistableFlags 返回所有跨章标记的当前值
- [ ] RestoreFlags 正确恢复存档中的标记值
- [ ] SetFlagRaw 不触发 OnOverlayChanged 事件

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `cross-chapter-state.md` (#16) | 跨章状态 | CrossChapterFlagRegistry | ScriptableObject 集中注册 |
| `cross-chapter-state.md` (#16) | 跨章状态 | IsImmutable 不可变标记 | OnChapterReplayStarted 保护 |
| `cross-chapter-state.md` (#16) | 跨章状态 | 新游戏初始化 | InitializeAllFlags → DefaultValue |
| `cross-chapter-state.md` (#16) | 跨章状态 | 跨章标记持久化 | GetPersistableFlags/RestoreFlags |
| `save-load-system.md` (#7) | 存档 | CrossChapterFlags 序列化 | SaveData.CrossChapterFlags Dictionary |
| `chapter-management.md` (#15) | 章节管理 | 章节重玩保护 | OnChapterReplayStarted → IsImmutable 保护 |

## Related

- ADR-0007 — ChangeTracker._flags 共享存储
- ADR-0003 — CrossChapterFlags 序列化到存档
- `docs/architecture/architecture.md` §2 — CrossChapterTracker 模块所有权
