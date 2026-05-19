# Epic: 本地化系统 (LocalizationManager)

> **Layer**: Foundation
> **GDD**: design/gdd/localization.md
> **Architecture Module**: LocalizationManager (#4)
> **Status**: Ready
> **Stories**: 4 created — 2 Logic, 1 Integration, 1 Config/Data

## Overview

实现《回响》中所有玩家可见文本的语言管理系统。基于 Unity Localization Package (`com.unity.localization`) 构建，以简体中文 (zh-Hans) 为开发基准语言，MVP 阶段额外支持英语 (en)。采用 1+N StringTable 设计（1 个全局 UI_Shared 表 + 按章节分割的 Narrative_Ch[N] 表），利用包原生 Fallback 链 (en → zh-Hans) 确保缺失翻译自动回退到中文原文。Runtime locale 切换通过 LocalizationSettings.SelectedLocale 触发所有 LocalizedString 组件当帧刷新。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0015: 本地化策略 | Unity Localization 包 + 双字符串表 (UI_Shared + Narrative) + en→zh-Hans fallback | MEDIUM |
| ADR-0001: 事件总线架构 | OnLocaleChanged static event 通知所有 UI 刷新 | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-localization-001 | Unity Localization Package, zh-Hans (default) + en locales, en→zh-Hans fallback | ADR-0015 ✅ |
| TR-localization-002 | 1+N StringTable: UI_Shared (persistent) + Narrative_Ch01-04 (per-chapter) | ADR-0015 ✅ |
| TR-localization-003 | Runtime locale 切换: LocalizationSettings.SelectedLocale 自动刷新所有 LocalizedString | ADR-0015 ✅ |
| TR-localization-004 | MemoryFragment SO 存储 TableReference + TableEntryReference (非原始字符串) | ADR-0015 ✅ |
| TR-localization-005 | MissingTranslationEvent 回调日志警告；禁止向玩家显示原始 Key 名 | ADR-0015 ✅ |
| TR-localization-006 | OnLocaleChanged event 通知 UI 刷新 (遵循 ADR-0001 static event 模式) | ADR-0015 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/localization.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | Unity LP 配置 + StringTable 结构 | Config/Data | Complete | ADR-0015 |
| 002 | 运行时 Locale 切换引擎 | Logic | Complete | ADR-0015, ADR-0001 |
| 003 | Fallback 链 + 缺失翻译处理 | Logic | Complete | ADR-0015 |
| 004 | Locale 持久化集成 | Integration | Complete | ADR-0015, ADR-0003 |

## Next Step

Run `/story-readiness production/epics/localization/story-001-unity-lp-setup-stringtable-config.md` to begin implementation.
