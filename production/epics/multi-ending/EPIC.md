# Epic: 多结局系统 (MultiEndingSystem)

> **Layer**: Feature
> **GDD**: design/gdd/multi-ending-system.md
> **Architecture Module**: MultiEndingSystem (#14)
> **Status**: Complete
> **Stories**: 4/4 Complete

## Overview

实现《回响》中"选择的重量"的最终体现——一个条件评估与权重累加引擎，在每章完成时收集该章所有碎片中定义的 EndingTrigger，通过变化追踪系统 (#12) 评估每个触发条件的满足情况，累加 ContributionWeight，按章节结局定义的 MinimumScore 阈值判定哪个结局被触发。三阶段判定算法：收集触发器（遍历 ChapterDefinition.Endings[] → 所有碎片中的 EndingTrigger[] → 按 EndingId 分组）→ IsEssential 必要门（任何 IsEssential 触发器未满足 → 结局取消资格）→ 累加分数 + 阈值检查 + Tie-breaking（必要条件数 DESC → 新颖性偏向 → 定义顺序 ASC）。支持跨章节隐藏结局（通过 FlagSet + ChapterCompleted 条件），关联路径加成 Hook（EmotionalAffinity 匹配 dominantPathEmotion，MVP 默认 weight=0.0），结局可重判（不缓存，每次 ResolveEnding 重新评估），UnlockedEndingIds 并集语义（新结局添加，旧结局保留）。纯 C# 逻辑，完全可单元测试。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0010: 多结局判定算法 | 三阶段判定——收集触发器 → IsEssential 门控 → ContributionWeight 累加 + EmotionalAffinity 路径加分 → 阈值检查 → Tie-breaking | LOW |
| ADR-0008: ConditionGroup 引擎 | EndingTrigger.TriggerCondition 评估——ConditionGroup 求值 | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-multi-ending-001 | EndingDefinition 数据结构（EndingId/EndingType/MinimumScore/IsDefault/EmotionalAffinity）+ 章节配置拥有 | ADR-0010 ✅ |
| TR-multi-ending-002 | 三阶段判定算法——收集触发器 → IsEssential 必要门（任一未满足→取消资格）→ ContributionWeight 累加 + 阈值检查 | ADR-0010 ✅ |
| TR-multi-ending-003 | Tie-breaking 三级优先级（必要条件数 DESC → 新颖性偏向 → 定义顺序 ASC），确定性结果 | ADR-0010 ✅ |
| TR-multi-ending-004 | 默认结局兜底——每章恰好一个 IsDefault=true, MinimumScore=0.0 的结局，永远合格 | ADR-0010 ✅ |
| TR-multi-ending-005 | 隐藏结局跨章节机制——通过 FlagSet(flagId, value) + ChapterCompleted(chapterId) 条件，无需特殊判定路径 | ADR-0010 ✅ |
| TR-multi-ending-006 | UnlockedEndingIds 并集持久化 + 结局可重判（每次 ResolveEnding 重新评估，不缓存） | ADR-0010 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/multi-ending-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | EndingDefinition 数据结构 + 触发器收集 | Logic | Complete | ADR-0010 |
| 002 | 三阶段判定算法 | Logic | Complete | ADR-0010 |
| 003 | Tie-Breaking + 结局持久化 + 重判 | Logic | Complete | ADR-0010 |
| 004 | 隐藏结局跨章节支持 + Path Bonus Hook | Integration | Complete | ADR-0010 |
