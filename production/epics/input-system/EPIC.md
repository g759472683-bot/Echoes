# Epic: 输入系统 (InputManager)

> **Layer**: Core
> **GDD**: design/gdd/input-system.md
> **Architecture Module**: InputManager (#1)
> **Status**: Complete
> **Stories**: 4 created — 3 Logic, 1 Integration — ALL COMPLETE

## Overview

实现《回响》中所有玩家操作的统一入口。封装 Unity Input System Package 底层 API，通过生成的 `PlayerControls` C# 类提供类型安全的输入访问。定义两个互斥 Action Map（Gameplay 用于画卷交互、UI 用于菜单导航），由 InputManager 集中管理切换。HoverDetector 组件每帧执行单次 `Physics2D.OverlapPoint` (non-alloc) 检测悬停物件并分发 OnHoverEnter/OnHoverExit 事件。支持运行时按键重绑定（PerformInteractiveRebinding + PlayerPrefs 持久化）和设备热插拔检测。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0005: 输入系统架构 | Input System 包 + PlayerControls C# 生成 + 两个 Action Map 互斥门控 + PlayerPrefs 重绑定 | HIGH |
| ADR-0001: 事件总线架构 | OnGamepadConnectionChanged static event; HoverDetector 事件基于 ADR-0001 模式 | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-input-system-001 | Unity Input System + 两个 Action Map (Gameplay/UI) 互斥切换 | ADR-0005 ✅ |
| TR-input-system-002 | HoverDetector + 单次 Physics2D.OverlapPoint/frame (Interactable layer) | ADR-0005 ✅ |
| TR-input-system-003 | PerformInteractiveRebinding() + PlayerPrefs 持久化 | ADR-0005 ✅ |
| TR-input-system-004 | 设备热插拔检测 + OnGamepadConnectionChanged event 通知 | ADR-0005 ✅ |
| TR-input-system-005 | 四个输入状态: Gameplay/Menu/Rebinding/Inactive + 场景加载触发转换 | ADR-0005 ✅ |
| TR-input-system-006 | 手柄仅菜单导航; Gamepad.current==null 隐藏手柄提示 | ADR-0005 ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | PlayerControls 设置 + Action Map 状态机 | Integration | Complete | ADR-0005 |
| 002 | HoverDetector 悬浮检测引擎 | Logic | Complete | ADR-0005 |
| 003 | 运行时按键重绑定 | Logic | Complete | ADR-0005 |
| 004 | 设备热插拔 + 手柄菜单支持 | Logic | Complete | ADR-0005 |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/input-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/story-readiness production/epics/input-system/story-001-action-map-state-machine.md` to start implementation.
