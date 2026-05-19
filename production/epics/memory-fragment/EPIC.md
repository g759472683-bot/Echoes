# Epic: 记忆碎片数据模型 (MemoryFragment)

> **Layer**: Core
> **GDD**: design/gdd/memory-fragment-data-model.md
> **Architecture Module**: MemoryFragment (#8)
> **Status**: Complete
> **Stories**: 3 created — 1 Config/Data, 2 Logic (all Complete)

## Overview

定义《回响》中游戏世界最小原子单位——MemoryFragment ScriptableObject 的完整 Schema。一个碎片包含 8 个数据类别：核心标识、视觉图层、可交互物件、情感标签、选项分支、内容变化（6 种 ContentChange 类型）、显式关联、结局触发器。采用"已干的墨"(Immutable) 与"未干的墨"(Mutable) 分类——设计时创作 SO，运行时只读，所有玩家选择产生的变化由 ChangeTracker overlay 管理。定义 ConditionGroup 条件系统（6 种叶子条件 + All/Any/Not 组合器，最大深度 3，短路径求值）供所有需要条件判断的系统使用。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0007: SO 不可变 + Overlay | Base SO (immutable) + ChangeTracker._overlay (mutable) 两层模型; 6 种 ContentChange overlay 算法 | MEDIUM |
| ADR-0008: ConditionGroup 引擎 | 6 leaf conditions + All/Any/Not, max depth 3, short-circuit evaluation, triggered on query | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-memory-fragment-001 | MemoryFragment SO 8 类别完整 Schema | ADR-0007 ✅ |
| TR-memory-fragment-002 | 6 种 ContentChange 类型定义 | ADR-0007 ✅ |
| TR-memory-fragment-003 | ConditionGroup: All/Any/Not + 6 leaf conditions, max depth 3, [SerializeReference] | ADR-0008 ✅ |
| TR-memory-fragment-004 | InteractiveObject: Hitbox, InteractionType enum (Touch/Drag/Hover/Examine), InteractCondition | ADR-0007 ✅ |
| TR-memory-fragment-005 | Cross-fragment ContentChange 同章验证; cross-chapter 通过 SetFlag + ConditionGroup | ADR-0007 ✅ |
| TR-memory-fragment-006 | Single fragment 5-10KB; 60-100 total <1MB; object limit 2-5 per fragment MVP | ADR-0007 ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | MemoryFragment SO 完整 Schema | Config/Data | Complete | ADR-0007 |
| 002 | ConditionGroup 条件求值引擎 | Logic | Complete | ADR-0008 |
| 003 | 编辑器验证 + 跨碎片约束 | Logic | Complete | ADR-0007 |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/memory-fragment-data-model.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Epic complete. All 3 stories implemented, reviewed, and closed.
