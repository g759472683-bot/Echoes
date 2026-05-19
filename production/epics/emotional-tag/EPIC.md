# Epic: 情感标签系统 (EmotionalTagSystem)

> **Layer**: Feature
> **GDD**: design/gdd/emotional-tag-system.md
> **Architecture Module**: EmotionalTagSystem (#10)
> **Status**: Complete
> **Stories**: 4/4 complete — ALL DONE

## Overview

实现《回响》中记忆碎片的"嗅觉系统"——一个有限的标签词汇表（MVP 15-20 个，8 个情感类别），为每个碎片分配一个或多个标签及权重，当网状关联引擎 (#13) 需要在碎片之间建立联想时提供情感相似度的原始词汇。包含 EmotionalTagCatalog ScriptableObject（全局唯一，设计时创作，运行时只读）、标签层级（最多 2 层，ParentTagId）、互斥规则（IncompatibleWith）、标签权重合并（BaseWeight + ChangeTracker overlay ModifyTagWeight）、以及 Editor 中的 Emotional Tag Browser 工具窗口。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0007: SO 不可变 + Overlay | BaseWeight 来自 SO（不可变），运行时权重 = BaseWeight × ModifyTagWeight overlay | MEDIUM |

⚠️ 本系统无专属 ADR。标签词汇表 Schema、层级规则、互斥规则、查询 API 由 GDD `emotional-tag-system.md` 第 3 节直接定义。标签相似度矩阵的计算和使用归 ADR-0009（关联引擎）。

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-emotional-tag-001 | EmotionalTagCatalog 全局唯一 SO，15-20 标签 × 8 Category，启动加载 <100ms | ADR-0007 ✅ |
| TR-emotional-tag-002 | 每个碎片 1-5 个标签分配（TagId + BaseWeight + IsPrimary），查询 API（GetTagsForFragment / GetPrimaryTag / QueryFragmentsByTag / GetTagCategory / GetRelatedTags） | ADR-0007 ✅ |
| TR-emotional-tag-003 | 标签层级最多 2 层，父标签查询自动包含子标签，互斥规则（IncompatibleWith 不可同时为 IsPrimary） | ⚠️ No dedicated ADR |
| TR-emotional-tag-004 | 运行时权重 = BaseWeight × ModifyTagWeight overlay merge（ModOp: Add/Multiply/Set），Clamp [0.0, 1.0] | ADR-0007 ✅ |
| TR-emotional-tag-005 | Editor Emotional Tag Browser 工具窗口（树形视图、引用计数、安全重命名/删除、孤立标签检测） | ⚠️ No dedicated ADR |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/emotional-tag-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | EmotionalTagCatalog SO 定义 + 启动加载 | Logic | Complete | ADR-0007 |
| 002 | 标签查询 API + 层级解析 | Logic | Complete | ADR-0007 |
| 003 | 运行时权重叠加 + 编辑器验证 | Integration | Complete | ADR-0007 |
| 004 | Emotional Tag Browser 编辑器窗口 | Config/Data | Complete | N/A |
