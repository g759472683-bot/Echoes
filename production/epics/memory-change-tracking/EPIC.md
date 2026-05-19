# Epic: 记忆变化追踪 (ChangeTracker)

> **Layer**: Feature
> **GDD**: design/gdd/memory-change-tracking.md
> **Architecture Module**: ChangeTracker (#12)
> **Status**: Complete
> **Stories**: 4 stories created (001–004)

## Overview

实现《回响》中"选择即重写"的记录引擎——一个运行时叠加层系统，维护 `_overlay` Dictionary（Key=(fragmentId, choiceId) → ContentOverrides），记录每一次玩家选择产生的 6 种内容变化（ToggleVisualLayer/SetObjectState/SetTextContent/ModifyTagWeight/UnlockAssociation/SetFlag）。提供 GetCurrentState 状态合并算法（base SO + 所有 overlay 条目按 OrderIndex 升序合并 → ResolvedFragmentState 不可变快照），全局 Flag 字典（_flags: Dictionary<string, bool>），已访问碎片/已完成章节集合（_visitedFragments/_completedChapters HashSet），仅追加变更日志（_changeLog），以及 ConditionGroup 运行时求值引擎（6 种叶子条件 + All/Any/Not 组合器，最大深度 3，短路径求值）。ApplyChanges 包含完整验证（FragmentId/LayerId/ObjectId/TagId/AssociationTargetId 存在性检查），并触发 OnOverlayChanged 事件通知微动画 (#9) 和 HUD (#17) 刷新。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0007: SO 不可变 + Overlay | Base SO (immutable) + ChangeTracker._overlay (mutable) 两层模型；6 种 ContentChange overlay 算法；GetCurrentState 合并策略 | MEDIUM |
| ADR-0008: ConditionGroup 引擎 | 6 leaf conditions + All/Any/Not，max depth 3，short-circuit evaluation，triggered on query | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-memory-change-tracking-001 | _overlay Dictionary + ContentOverrides 结构体（6 种变更类型分组字段）+ OverlayVersion 递增 | ADR-0007 ✅ |
| TR-memory-change-tracking-002 | ApplyChanges 算法（验证→转换→存储→日志→OverlayVersion++→OnOverlayChanged 事件） | ADR-0007 ✅ |
| TR-memory-change-tracking-003 | GetCurrentState 状态合并算法（base SO 复制 + 按 OrderIndex 升序应用 overlay 条目，6 种变更各自合并策略） | ADR-0007 ✅ |
| TR-memory-change-tracking-004 | Flag 系统（_flags Dictionary + SetFlag/GetFlag）+ _visitedFragments/_completedChapters 集合 | ADR-0007 ✅ |
| TR-memory-change-tracking-005 | ConditionGroup 运行时求值引擎（6 种叶子条件 + 3 种组合器，最大深度 3，短路径求值） | ADR-0008 ✅ |
| TR-memory-change-tracking-006 | _changeLog 仅追加日志 + ApplyChanges 验证（无效条目跳过 + LogWarning） + 跨碎片变更即时生效 | ADR-0007 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/memory-change-tracking.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | ChangeTracker 核心 + ApplyChanges | Logic | Complete | ADR-0007 |
| 002 | GetCurrentState 状态合并 | Logic | Complete | ADR-0007 |
| 003 | Flag 系统 + 条件求值集成 | Integration | Complete | ADR-0007, ADR-0008 |
| 004 | 存档序列化与恢复 | Integration | Complete | ADR-0007 |
