# Epic: 网状关联引擎 (WebAssociationEngine)

> **Layer**: Feature
> **GDD**: design/gdd/web-association-engine.md
> **Architecture Module**: WebAssociationEngine (#13)
> **Status**: Complete
> **Stories**: 4/4 Complete

## Overview

实现《回响》中"关联的网络"的计算心脏——一个多因子关联评分引擎，回答"接下来，哪些记忆与此刻相关？"。纯 C# 类（非 MonoBehaviour，完全可单元测试），通过构造函数注入依赖（TagSimilarityMatrix SO + EmotionalTagSystem + ChangeTracker + 数据模型）。唯一公开方法 ComputeAssociations(currentFragmentId, chapterKey, recentHistory, visitedFragmentIds) 是纯函数——无状态，相同输入产生相同输出。四因子公式：Score = (A × 0.6 + B × 0.4) × C × D，其中 A=余弦标签相似度（使用预计算 TagSimilarityMatrix N×N），B=显式关联权重（含双向加成 0.15，B=-1.0 设计师排除），C=情感节奏惩罚（滑动窗口 K=4，连续同类别重复惩罚，Peace 类别 ×1.30 调色板清洁剂），D=发现偏向（未访问 ×1.30，有内容变化重访 ×0.70）。候选池过滤（同章、已解锁、条件满足、排除自身），Top-5 返回，Strength 分级（Strong ≥0.60 / Medium ≥0.30 / Faint ≥0.10 / Trace <0.10），DominantFactor 标记。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0009: 网状关联引擎 | 四因子加权公式 Score = (A×0.6 + B×0.4) × C × D + 候选池过滤 + Visual Grading 4 档 | LOW |
| ADR-0008: ConditionGroup 引擎 | 候选池 ConditionGroup 过滤——已解锁碎片的条件评估 | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-web-association-001 | 纯 C# 关联引擎（非 MonoBehaviour，构造函数依赖注入），ComputeAssociations 纯函数 | ADR-0009 ✅ |
| TR-web-association-002 | 候选池构建——同章碎片，排除自身/Locked/ConditionGroup 不可达 | ADR-0009 ✅ |
| TR-web-association-003 | 因子 A — 余弦标签相似度（TagSimilarityMatrix N×N 预计算 SO，默认规则 + 逐对覆盖） | ADR-0009 ✅ |
| TR-web-association-004 | 因子 B — 显式关联权重（双向加成 +0.15，B=-1.0 设计师排除） | ADR-0009 ✅ |
| TR-web-association-005 | 因子 C — 情感节奏惩罚（K=4 滑动窗口, penaltyForPosition 阶梯: 0.70/0.55/0.40/0.25, Peace ×1.30, 候选池≤5 自适应减半）+ 因子 D — 发现偏向（未访问 ×1.30, 重访衰减 -0.30/次, 有 pending changes 保底 0.70） | ADR-0009 ✅ |
| TR-web-association-006 | Top-5 排序 + Strength 4 级分级（Strong ≥0.60/Medium ≥0.30/Faint ≥0.10/Trace）+ DominantFactor 标记 | ADR-0009 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/web-association-engine.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 引擎架构 + 候选池构建 | Logic | Complete | ADR-0009 |
| 002 | 因子 A — 余弦标签相似度 | Logic | Complete | ADR-0009 |
| 003 | 因子 B + C + D | Logic | Complete | ADR-0009 |
| 004 | 综合评分 + 排名 + Strength 分级 | Logic | Complete | ADR-0009 |
