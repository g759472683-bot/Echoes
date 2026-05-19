# 跨章节状态追踪 (Cross-Chapter State Tracking)

> **Status**: Designed (pending review)
> **Author**: 用户 + game-designer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 2 (不完美才是力量) — 间接支撑——跨章状态是"选择有持久后果"的技术基础

## Overview

跨章节状态追踪是《回响》中"选择的涟漪跨越章节边界"的机制。变化追踪系统 (#12) 管理全局 Flag 的读写——SetFlag 写入，FlagSet 条件查询。存档系统 (#7) 负责将 Flag 字典序列化到 `CrossChapterFlags`。本系统夹在两者之间，做三件事：

1. **Flag 注册表**: 定义游戏中所有跨章节 Flag 的目录——每个 Flag 的 ID、描述、哪个章节/碎片设置它、哪些章节消费它
2. **引用验证**: Editor 时检查——若 Ch03 的 ConditionGroup 引用了一个 Flag，但该 Flag 从未在任何章节被 SetFlag 设置 → 警告设计师
3. **生命周期管理**: 新游戏时初始化 Flag 默认值、章节重玩时保护跨章 Flag 不被重置、标记哪些 Flag 是"一旦设置永不改变"的

没有它，跨章节的隐藏结局条件链（"第一章保留信 → 第三章 NPC 出现"）仍然是可工作的——ChangeTracker 的 Flag 字典 + SaveSystem 的持久化足以运行。但设计师在编辑器中看不到 Flag 的全貌，无法验证引用完整性，容易写出"永远无法满足"或"引用不存在 Flag"的条件。本系统给这些静默的跨章线索一张可见的地图。

## Player Fantasy

纯基础设施——玩家不会"感受"Flag 注册表。但每个跨章节的隐藏结局、每个"之前的选择让这个 NPC 出现了"的瞬间，背后是跨章节状态追踪在确保那条线索没有被遗忘。

## Detailed Design

### Core Rules

**规则 1 — CrossChapterFlag 注册表**

```
CrossChapterFlagRegistry (ScriptableObject, assets/data/)

每个条目:
  FlagId : string               // 唯一标识。e.g. "ch1_letter_kept"
  Description : string          // 仅供设计师——"玩家在 Ch01 frag_07 中保留了信"
  SetInChapter : string         // 哪个章节设置此 Flag。e.g. "ch01"
  SetInFragmentId : string      // 哪个碎片设置此 Flag
  SetByChoiceId : string        // 哪个选择设置此 Flag
  IsImmutable : bool            // 若 true，一旦设置为 true 永不改变（不可逆选择）
  DefaultValue : bool           // 新游戏时的初始值。通常 false
  ConsumedBy : string[]         // 哪些 EndingId / ConditionGroup 消费此 Flag
```

注册表是一个 ScriptableObject，在 Editor 中作为 Flag 目录维护。运行时只读。

**规则 2 — Flag 初始化 (New Game)**

```csharp
void InitializeAllFlags()
{
    foreach (var flag in _registry.Flags)
    {
        ChangeTracker.SetFlagRaw(flag.FlagId, flag.DefaultValue);
        // SetFlagRaw 是 ChangeTracker 内部方法——直接设置 _flags[key] = value
        // 不经过 ApplyChanges 流程（不在叠加层中——Flag 是全局状态）
    }
}
```

所有 Flag 的初始值由注册表定义——通常为 false（尚未触发）。

**规则 3 — Flag 持久化桥梁**

ChangeTracker 的 `_flags` 字典在保存时序列化到 `SaveData.CrossChapterFlags`：

```csharp
// 保存 (SaveManager.CollectSaveData → 调用本系统)
Dictionary<string, bool> GetPersistableFlags()
{
    return new Dictionary<string, bool>(ChangeTracker.GetAllFlags());
    // GetAllFlags 返回 _flags 的浅拷贝
}

// 加载 (SaveManager.RestoreSaveData → 调用本系统)
void RestoreFlags(Dictionary<string, bool> savedFlags)
{
    foreach (var kv in savedFlags)
    {
        ChangeTracker.SetFlagRaw(kv.Key, kv.Value);
    }
}
```

本系统自身不持有 Flag 字典——ChangeTracker 是唯一事实来源。

**规则 4 — 章节重玩时的 Flag 保护**

章节重玩 (#15 规则 9) 重置 `_chapterVisitedFragments` 但不重置 `_flags`。跨章 Flag 在重玩期间持续存在：

- 重玩 Ch01 时: 之前 Ch01 设置的 Flag（如 "ch1_letter_kept"=true）**保留**
- 玩家在重玩中做出不同选择: 新的 SetFlag 可以覆盖旧值
- IsImmutable=true 的 Flag: 一旦为 true，即使玩家在重玩中做出不同选择也不可逆转为 false

```
IF flag.IsImmutable AND ChangeTracker.GetFlag(flag.FlagId) == true:
  // 拒绝任何 SetFlag(flagId, false) 操作
  // LogWarning: "不可逆 Flag [flagId] 已为 true——忽略 SetFlag(false)"
```

这保证了"保留信→NPC出现"这类关键选择的不可撤销性。

**规则 5 — Flag 引用验证 (Editor 工具)**

Editor 验证遍历所有碎片和章节配置，检查：

1. **所有被引用的 Flag 都已在注册表中注册**: ConditionGroup 中的 `FlagSet(flagId, value)` → flagId 必须存在于注册表
2. **所有注册的 Flag 至少有一个 SetFlag 来源**: 注册表中的 Flag 必须有 `SetInFragmentId` 指向实际设置它的碎片
3. **跨章引用一致性**: 若 Flag 的 `SetInChapter="ch01"` 且 `ConsumedBy=["ending_hidden"]` 在 Ch03，验证 Ch01→Ch03 之间的章节序列是完整的（Ch01 在 Ch03 之前已解锁）

验证在 Editor 批量验证窗口中运行——不阻塞构建，但显示警告列表。

**规则 6 — Flag 依赖图可视化 (Editor)**

生成 Flag 之间的依赖关系图（非运行时——仅供设计师查看）:

```
"ch1_letter_kept" (Ch01) ──→ "hidden_reunion" (Ch03 Ending)
"ch2_secret_found" (Ch02) ──┤
```

这帮助设计师理解"哪些 Flag 需要在新游戏中被触发才能解锁特定隐藏结局"。

**规则 7 — MVP 范围**

MVP 包含:
- Flag 注册表 ScriptableObject
- 新游戏 Flag 初始化
- Flag 持久化桥梁 (ChangeTracker ↔ SaveSystem)
- 章节重玩时的 Flag 保护 (含 IsImmutable)

MVP 不包含:
- Editor 引用验证 (Vertical Slice——可在 Inspector 中手动检查)
- Flag 依赖图可视化 (Full Vision——Tools Programmer)

### States and Transitions

本系统无运行时状态机。Flag 的读写全部委托给 ChangeTracker (#12)。本系统仅在以下时机执行操作：
- **New Game**: InitializeAllFlags — 批量设置默认值
- **Save**: GetPersistableFlags — 读取全量 Flag 字典
- **Load**: RestoreFlags — 批量恢复
- **Chapter Replay Entry**: 验证 IsImmutable Flag 保护

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 下游 | **变化追踪 (#12)** | SetFlagRaw, GetAllFlags, GetFlag | Flag 的唯一存储——本系统不持有 Flag，只协调 |
| 上游 | **存档系统 (#7)** | CrossChapterFlags 字典 | 保存时提供 Flag 快照；加载时恢复 |
| 上游 | **章节管理 (#15)** | OnChapterReplayStarted 事件 | 重玩入口时执行 Flag 保护检查 |
| 下游 | **多结局 (#14)** | FlagSet 条件型 | 结局条件间接消费 Flag——通过变化追踪的条件评估 |
| 下游 | **Editor 工具** | CrossChapterFlagRegistry.asset | 设计师在 Inspector 中查看/编辑 Flag 目录 |

## Formulas

本系统不含运行时计算公式。Flag 是布尔值——没有数学公式。

- **IsImmutable 守护**: `IF flag.IsImmutable AND currentValue == true THEN reject SetFlag(false)`
- **默认值**: `newGame → allFlags = registry.DefaultValue` (通常 false)

## Edge Cases

- **注册表中的 Flag 从未被任何碎片 SetFlag 引用**: 死 Flag——Editor 验证警告。运行时无害——始终保持默认值。
- **ConditionGroup 引用了未注册的 Flag**: 运行时 ChangeTracker.EvaluateCondition → Flag 不存在于 _flags → 返回 false。Editor 验证应预先捕获此问题。
- **存档中的 Flag 在注册表中不存在 (旧存档，Flag 被重构)**: 加载时恢复已存在的 Flag；注册表中新增的 Flag 使用 DefaultValue。孤儿 Flag（存档中有但注册表中无）保留在 ChangeTracker._flags 中但不被任何条件引用——无害。
- **IsImmutable Flag 在不同章节被多个碎片设置**: 合法——第一个设置为 true 后锁定。后续 SetFlag(false) 被拒绝。同一碎片中的不同选择都可以 SetFlag(true)——幂等。
- **章节重玩时玩家做出完全不同的选择——之前设置为 true 的非 IsImmutable Flag 需要被翻转**: 新选择的 SetFlag(false) 正常执行——非 IsImmutable Flag 可翻转。这可能导致之前解锁的隐藏结局条件不再满足——正确的行为（玩家改变了选择）。

## Dependencies

### 硬依赖

| 系统 | 性质 | 接口 |
|------|------|------|
| **变化追踪 (#12)** | 硬依赖 — Flag 的唯一存储 | SetFlagRaw, GetAllFlags, GetFlag |
| **存档系统 (#7)** | 硬依赖 — Flag 持久化 | CrossChapterFlags 字典 |

### 软依赖

| 系统 | 性质 | 接口 |
|------|------|------|
| **章节管理 (#15)** | 软依赖 — 重玩入口检测 | OnChapterReplayStarted 事件 |
| **多结局 (#14)** | 软依赖 — Flag 消费方 | 通过变化追踪条件评估间接消费 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| Flag 默认值 | false | false | 几乎所有 Flag 默认为 false——选择后才变为 true。不建议改变 |
| IsImmutable (每 Flag) | false | — | 由设计师在注册表中逐个标记。不可逆 Flag 应谨慎使用——每章最多 2-3 个 |

## Visual/Audio Requirements

纯数据协调层——无视觉或音频输出。

## UI Requirements

无运行时 UI。CrossChapterFlagRegistry Inspector 视图（Editor 工具）显示 Flag 目录——属于开发工具。

## Acceptance Criteria

- **GIVEN** 新游戏启动，**WHEN** InitializeAllFlags 执行，**THEN** ChangeTracker._flags 包含注册表中所有 Flag，每个 Flag 的值为其 DefaultValue（通常 false）。

- **GIVEN** 玩家在 Ch01 frag_07 中选择 "keep_letter"，该选择的 ContentChange 包含 SetFlag("ch1_letter_kept", true)，**WHEN** ChangeTracker.ApplyChanges 执行，**THEN** ChangeTracker._flags["ch1_letter_kept"] = true。

- **GIVEN** 游戏中有 Flag "ch1_letter_kept"=true，**WHEN** SaveManager 调用 GetPersistableFlags，**THEN** 返回的字典包含 {"ch1_letter_kept": true}，被序列化到 SaveData.CrossChapterFlags。

- **GIVEN** 存档中 CrossChapterFlags = {"ch1_letter_kept": true}，**WHEN** 加载存档并调用 RestoreFlags，**THEN** ChangeTracker._flags["ch1_letter_kept"] = true。Ch03 中依赖此 Flag 的隐藏结局条件可评估为 true。

- **GIVEN** Flag "ch1_letter_kept" 的 IsImmutable=true 且当前值为 true，**WHEN** 玩家重玩 Ch01 并做出不同选择（触发 SetFlag("ch1_letter_kept", false)），**THEN** SetFlag(false) 被拒绝。Flag 保持 true。LogWarning 记录。

- **GIVEN** Ch03 的 ConditionGroup 引用 FlagSet("ch1_letter_kept", true)，而 "ch1_letter_kept" 未在注册表中注册，**WHEN** Editor 验证运行，**THEN** 验证警告"Flag [ch1_letter_kept] 未注册"。

- **GIVEN** 玩家完成 Ch01 并重玩 Ch01（通过章节选择），**WHEN** 重玩入口 OnChapterReplayStarted 触发，**THEN** IsImmutable Flag 的保护逻辑激活。非 IsImmutable Flag 可在重玩中被自由修改。

## Open Questions

- **Flag 注册表是否应支持"跨章节 Flag 迁移"**: 若 Full Vision 中 Flag 被重命名或拆分，旧存档中的 Flag 如何处理？建议预留 `LegacyFlagIds: string[]` 字段——加载时若旧 Flag ID 存在则迁移到新 ID。(Owner: gameplay-programmer, Full Vision)

- **IsImmutable Flag 的"后悔"机制**: 若玩家真正想撤销一个不可逆选择——是否提供"完全重置存档"选项？还是在章节选择中提示"此章节的关键选择不可撤销"？(Owner: ux-designer, Vertical Slice)
