# Epic: 数据管理系统 (DataManager)

> **Layer**: Foundation
> **GDD**: design/gdd/data-management.md
> **Architecture Module**: DataManager (#2)
> **Status**: Ready
> **Stories**: 5 created — 3 Logic, 1 Integration, 1 Config/Data

## Overview

实现《回响》中所有游戏内容的数据加载、缓存和查询基础设施。DataManager 是 Foundation 层根模块——所有其他系统通过它获取 ChapterDefinition、MemoryFragment、Sprite 等游戏数据。采用 Unity Addressables 资产管理系统，以 ScriptableObject 两层结构（ChapterDefinition SO + MemoryFragment SO 数组）组织数据，提供 Task-based 三态异步就绪模型（Cached/Loading/NotRequested），支持并发请求去重和章节预加载。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0002: 数据管理策略 | Addressables 加载 + 三态异步就绪模型 + 并发去重 + 预加载触发 | MEDIUM |
| ADR-0001: 事件总线架构 | static event Action<T> 跨系统通信（DataManager 加载完成通知） | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-data-management-001 | SO 两层结构: ChapterDefinition SO + AssetReferenceT<MemoryFragment>[] | ADR-0002 ✅ |
| TR-data-management-002 | Task-based 三态异步 API (Cached/Loading/NotRequested) + 并发请求去重 | ADR-0002 ✅ |
| TR-data-management-003 | Addressables 10 组: Data_Ch01-04, Art_Ch01-04, Shared_UI, Shared_Audio | ADR-0002 ✅ |
| TR-data-management-004 | 预加载触发: 剩余 ≤3 fragments 时 DownloadDependenciesAsync 下一章插图 | ADR-0002 ✅ |
| TR-data-management-005 | 三层数据验证: Editor Inspector + Build Addressables 交叉检查 + Runtime 描述性异常 | ADR-0002 ✅ |
| TR-data-management-006 | 所有 Addressables 加载调用包裹 try/catch (Unity 6.2+ 抛异常) | ADR-0002 ✅ |
| TR-data-management-007 | 元数据加载 <2s; 碎片定义查询 <50ms (内存中) | ADR-0002 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/data-management.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | SO 数据结构 + Addressables 分组配置 | Config/Data | Ready | ADR-0002 |
| 002 | 异步加载引擎 + 状态机 | Logic | Ready | ADR-0002 |
| 003 | 章节预加载 + 内存管理 | Logic | Ready | ADR-0002 |
| 004 | 数据验证 + 异常安全 | Logic | Ready | ADR-0002 |
| 005 | JSON 序列化桥接 | Integration | Ready | ADR-0002, ADR-0003 |

## Next Step

Run `/story-readiness production/epics/data-management/story-001-so-structures-addressables-groups.md` to begin implementation.
