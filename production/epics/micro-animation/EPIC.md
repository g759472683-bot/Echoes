# Epic: 微动画系统 (MicroAnimationSystem)

> **Layer**: Feature
> **GDD**: design/gdd/micro-animation-system.md
> **Architecture Module**: MicroAnimationManager (#9)
> **Status**: Complete
> **Stories**: 4/4 Complete

## Overview

实现《回响》中 Pillar 4（画卷中有呼吸）的物理引擎——一个轻量动画调度器，管理三种微动画：循环环境动画（Ambient）、一次性触发动画（Triggered）、交互反馈动画（Feedback）。GPU 端通过 Shader Graph + MaterialPropertyBlock 驱动（顶点位移、UV 滚动、颜色调制），CPU 端通过自定义 MicroTween 值类型 struct（~250 行，零 GC）运行 Transform 补间。三级性能降级（High→Medium→Low→Minimal），每碎片动画预算 <2ms。OnFragmentTransitioned 集成——碎片切换时自动启动/停止动画。L1/L2/L3 朱砂墨点发光系统——静态墨点 → 呼吸脉动 → 内光暖色偏移。EmotionPreset 按情感标签映射动画参数。MVP 包含自定义 MicroTween + 三类动画 + 性能降级 + 墨点系统。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0012: 微动画系统 | URP 2D Shader Graph + MaterialPropertyBlock + MicroTween 值类型 + 三级性能降级 + 两类 Verified Fallback | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-micro-animation-001 | 三种动画类别（Ambient/Triggered/Feedback）| ADR-0012 ✅ |
| TR-micro-animation-002 | 自定义 MicroTween struct（零 GC）+ 5 种缓动函数 | ADR-0012 ✅ |
| TR-micro-animation-003 | GPU 驱动动画（Shader Graph + MaterialPropertyBlock）+ CPU 仅 tick | ADR-0012 ✅ |
| TR-micro-animation-004 | 三级性能降级（High/Medium/Low/Minimal） | ADR-0012 ✅ |
| TR-micro-animation-005 | L1/L2/L3 朱砂墨点发光系统 | ADR-0012 ✅ |
| TR-micro-animation-006 | 性能预算 <2ms/fragment/frame | ADR-0012 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/micro-animation-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | MicroAnimationCatalog + Manager + MicroTween | Logic | Complete | ADR-0012 |
| 002 | GPU Shader 动画 + MaterialPropertyBlock | Integration | Complete | ADR-0012 |
| 003 | 性能降级 + OnFragmentTransitioned 集成 | Integration | Complete | ADR-0012 |
| 004 | L1/L2/L3 墨点发光 + EmotionPreset | Visual/Feel | Complete | ADR-0012 |
