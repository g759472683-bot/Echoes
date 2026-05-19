# Epic: 交互反馈系统 (InteractionFeedback)

> **Layer**: Feature
> **GDD**: design/gdd/interaction-feedback.md
> **Architecture Module**: InteractionFeedback (#18)
> **Status**: Complete
> **Stories**: 2/2 Complete

## Overview

实现《回响》中玩家操作的"应答层"——一个轻量的事件监听器：订阅交互系统 (#11) 的 10 个交互事件 + 场景管理 (#6) 的 2 个过渡事件，对每个事件调用微动画 (#9) 的触发方法和音频系统 (#3) 的音效播放。事件→反馈映射表（10 事件 → 视觉+音频响应对）、优先级系统（选择确认 > 拖拽完成 > 交互触发 > 悬停）、0.3s 防抖（按 objectId+eventName）、过渡期间抑制（_feedbackSuppressed flag）。无状态——纯事件驱动，无 Update()。MVP 需要 8 个短音效（<1s 每个）。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0014: 交互反馈映射表 | 10 事件→视觉+音频响应 + 优先级 + 防抖 + 转场抑制 + FeedbackMappings SO | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-interaction-feedback-001 | 订阅 InteractionManager 10 事件 + SceneManager 2 过渡事件 | ADR-0014 ✅ |
| TR-interaction-feedback-002 | 事件→反馈映射表（10 事件→视觉+音频响应） | ADR-0014 ✅ |
| TR-interaction-feedback-003 | 优先级（确认 > 拖拽 > 交互 > 悬停）+ 转场抑制 | ADR-0014 ✅ |
| TR-interaction-feedback-004 | 防抖 0.3s（objectId+eventName）；音频播放完成，视觉可中断 | ADR-0014 ✅ |
| TR-interaction-feedback-005 | _feedbackSuppressed flag + 纯事件驱动（无 Update） | ADR-0014 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/interaction-feedback.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 事件订阅 + 反馈映射 + 优先级 + 防抖 + 转场抑制 | Logic | Complete | ADR-0014 |
| 002 | 视觉+音频反馈协调（MicroAnimationManager + AudioManager） | Integration | Complete | ADR-0014, ADR-0012, ADR-0013 |
