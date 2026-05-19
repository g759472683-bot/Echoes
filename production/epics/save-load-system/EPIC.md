# Epic: 存档系统 (SaveManager)

> **Layer**: Foundation
> **GDD**: design/gdd/save-load-system.md
> **Architecture Module**: SaveManager (#7)
> **Status**: Ready
> **Stories**: 4 created — 3 Logic, 1 Integration

## Overview

实现《回响》中玩家进度的持久化与恢复管理器。采用三槽位设计（2 手动 + 1 自动），使用 System.Text.Json 序列化 SaveData 结构体，SHA-256 校验和保证完整性，原子写入 (.tmp → .sav) 防止写入中断导致的存档损坏。SaveManager 是聚合层——保存时从 6 个系统（ChapterManager、ChangeTracker、CrossChapterTracker、MultiEndingSystem、LocalizationManager、AudioManager）收集状态，加载时通过 ChapterManager.LoadAndRestore 分发恢复。支持版本迁移链以兼容未来存档格式变更。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0003: 存档序列化 | JSON + SHA-256 校验 + 原子写入 + 版本迁移链 | LOW |
| ADR-0001: 事件总线架构 | SaveManager 遵循架构原则 (不声明事件, 薄序列化层) | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-save-load-system-001 | 3 槽位 (save_01/save_02/auto_save) 在 Application.persistentDataPath/Saves/ | ADR-0003 ✅ |
| TR-save-load-system-002 | SHA-256 校验和覆盖所有字段 (Checksum 自身除外) — 保存计算, 加载验证 | ADR-0003 ✅ |
| TR-save-load-system-003 | 原子文件写入: .sav.tmp 序列化 → File.Move(tmp, final, overwrite: true) | ADR-0003 ✅ |
| TR-save-load-system-004 | 版本迁移链 (Migrate_V1_to_V2 等) — SaveData.Version < currentVersion 触发 | ADR-0003 ✅ |
| TR-save-load-system-005 | SaveData 聚合: chapter/fragment progress, ChangeOverlay, CrossChapterFlags, volume, locale, endings | ADR-0003 ✅ |
| TR-save-load-system-006 | 自动存档触发: chapter start, critical choice (30s debounce), chapter complete, OnApplicationQuit | ADR-0003 ✅ |
| TR-save-load-system-007 | 保存 <200ms (PC); 防止并发保存/加载操作 | ADR-0003 ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | SaveData 结构 + SHA-256 校验和 | Logic | Complete | ADR-0003 |
| 002 | 原子文件 I/O + 3 槽位管理 | Logic | Complete | ADR-0003 |
| 003 | 收集/恢复编排 + 版本迁移 | Integration | Complete | ADR-0003 |
| 004 | 自动存档引擎 | Logic | Complete | ADR-0003 |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/save-load-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/story-readiness production/epics/save-load-system/story-001-save-data-structure-checksum.md` to start implementation.
