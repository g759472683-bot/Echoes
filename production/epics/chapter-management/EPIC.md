# Epic: 章节管理 (ChapterManager)

> **Layer**: Feature
> **GDD**: design/gdd/chapter-management.md
> **Architecture Module**: ChapterManager (#15)
> **Status**: Complete
> **Stories**: 4/4 Complete

## Overview

实现《回响》中游戏进度的骨架——一个轻量级的进度状态机和协调器。持有当前章节/碎片引用（CurrentChapterKey/CurrentFragmentId），3 状态状态机（IDLE/IN_CHAPTER/TRANSITIONING），ChapterDefinition ScriptableObject（ChapterKey/EntryFragmentId/Endings[]/CompletionRatio/AllowReplay），碎片导航协调（关联引擎驱动——非 SequenceIndex 线性），章节完成检测（双条件：全部碎片已访问 OR 访问占比≥CompletionRatio 且最佳候选分数<COMPLETION_ASSOCIATION_THRESHOLD），章节完成过渡流程（ResolveEnding → 更新进度 → auto_save → 过渡到下一章或 OnAllChaptersCompleted），预加载触发（未访问碎片 ≤3 → 预加载下一章，每章一次），章节重玩（保留 overlay/flags，重置 _chapterVisitedFragments/_recentHistory），线性章节解锁（完成 N → 解锁 N+1，并集语义），存档/读档集成（CollectSaveData + RestoreFromSave）。MVP 范围：2 章线性推进（Ch01 → Ch02）。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0007: SO 不可变 + Overlay | ChapterDefinition SO（ChapterKey/EntryFragmentId/Endings[]）——设计时创作，运行时只读 | MEDIUM |
| ADR-0010: 多结局判定算法 | ResolveEnding(chapterId) 在章节完成时调用，EndingDefinition[] 由 ChapterDefinition 拥有 | LOW |
| ADR-0011: 跨章状态追踪 | OnChapterReplayStarted 事件 → 触发 IsImmutable Flag 保护 + 非 Immutable Flag 重置为 DefaultValue | LOW |
| ADR-0001: 事件总线架构 | OnChapterStarted/OnChapterCompleted/OnFragmentChanged/OnAllChaptersCompleted 四个 static event | LOW |

⚠️ 本系统无专属 ADR。章节状态机、完成检测算法、过渡协调流程、预加载触发逻辑由 GDD `chapter-management.md` 第 3 节直接定义。

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-chapter-management-001 | ChapterDefinition SO + 3 状态状态机（IDLE/IN_CHAPTER/TRANSITIONING）+ ChapterManager 骨架 | ADR-0007 ✅ |
| TR-chapter-management-002 | 碎片导航协调——关联引擎驱动（非 SequenceIndex），TransitionToFragment → SceneManager → 更新追踪 | ⚠️ No dedicated ADR |
| TR-chapter-management-003 | 章节完成检测（双条件：全部访问 OR 占比+关联阈值）+ 过渡流程（ResolveEnding → 更新进度 → auto_save → 过渡/结束） | ADR-0010 ✅ |
| TR-chapter-management-004 | 预加载触发（未访问 ≤3 → PreloadChapterAsync，每章一次） | ADR-0002, ADR-0004 ✅ |
| TR-chapter-management-005 | 章节重玩——持久化保留（overlay/flags/_completedChapters/_unlockedChapters），重置（_chapterVisitedFragments/_recentHistory/_preloadNotYetTriggered） | ADR-0011 ✅ |
| TR-chapter-management-006 | 线性章节解锁（新游戏=OrderIndex=0，完成 N→解锁 N+1，并集语义）+ 存档/读档集成 | ⚠️ No dedicated ADR |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/chapter-management.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 章节状态机 + 碎片导航 | Integration | Complete | ADR-0001 |
| 002 | 章节完成检测 | Logic | Complete | ADR-0010 |
| 003 | 章节完成过渡流程 | Integration | Complete | ADR-0010, ADR-0001 |
| 004 | 章节重玩 + 线性解锁 | Logic | Complete | ADR-0011 |
