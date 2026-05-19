# Epic: 跨章节状态追踪 (CrossChapterTracker)

> **Layer**: Feature
> **GDD**: design/gdd/cross-chapter-state-tracking.md
> **Architecture Module**: CrossChapterTracker (#16)
> **Status**: Complete
> **Stories**: 3/3 Complete

## Overview

实现《回响》中"选择的涟漪跨越章节边界"的机制——一个在 ChangeTracker (#12) 和存档系统 (#7) 之间的数据协调层。定义 CrossChapterFlagRegistry ScriptableObject（全局 Flag 目录——每个 Flag 包含 FlagId/Description/SetInChapter/SetInFragmentId/SetByChoiceId/IsImmutable/DefaultValue/ConsumedBy[]），新游戏 Flag 初始化（InitializeAllFlags → ChangeTracker.SetFlagRaw 批量设置 DefaultValue），Flag 持久化桥梁（GetPersistableFlags → SaveData.CrossChapterFlags → RestoreFlags），章节重玩时的 Flag 保护（IsImmutable=true 且当前值为 true → 拒绝 SetFlag(false) → LogWarning；非 IsImmutable Flag 在 OnChapterReplayStarted 时重置为 DefaultValue）。本系统不持有 Flag 字典——ChangeTracker._flags 是唯一事实来源。本系统通过 SetFlagRaw 内部接口直接写入，不经过 ApplyChanges 流程（Flag 是全局状态，不在 overlay 中）。MVP 包含 Flag 注册表 SO、初始化、持久化桥梁、IsImmutable 保护。Editor 引用验证和依赖图可视化推迟到 Vertical Slice/Full Vision。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0011: 跨章节状态追踪 | CrossChapterFlagRegistry SO + IsImmutable 保护 + ChangeTracker._flags 共享存储 + SetFlagRaw 内部接口 | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-cross-chapter-state-001 | CrossChapterFlagRegistry SO——集中定义所有跨章 Flag（FlagId/SetInChapter/SetByChoiceId/IsImmutable/DefaultValue/ConsumedBy[]），运行时只读 | ADR-0011 ✅ |
| TR-cross-chapter-state-002 | 新游戏 Flag 初始化——InitializeAllFlags → ChangeTracker.SetFlagRaw 批量设置 DefaultValue | ADR-0011 ✅ |
| TR-cross-chapter-state-003 | Flag 持久化桥梁——GetPersistableFlags（ChangeTracker.GetAllFlags 浅拷贝）→ SaveData.CrossChapterFlags → RestoreFlags（SetFlagRaw 批量恢复） | ADR-0011 ✅ |
| TR-cross-chapter-state-004 | 章节重玩 Flag 保护——IsImmutable=true 且当前值为 true → 拒绝 SetFlag(false)；非 IsImmutable → OnChapterReplayStarted 时重置为 DefaultValue | ADR-0011 ✅ |
| TR-cross-chapter-state-005 | SetFlagRaw 内部接口——直接写入 _flags[key]=value，不经过 ApplyChanges，不触发 OnOverlayChanged | ADR-0011 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/cross-chapter-state-tracking.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | Flag 注册表 + 新游戏初始化 + SetFlagRaw | Integration | Complete | ADR-0011 |
| 002 | IsImmutable 保护 + 章节重玩 Flag 生命周期 | Logic | Complete | ADR-0011 |
| 003 | Flag 持久化桥梁 | Integration | Complete | ADR-0011 |
