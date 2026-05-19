# Epic: UI框架 (UIPanelStack)

> **Layer**: Core
> **GDD**: design/gdd/ui-framework.md
> **Architecture Module**: UIPanelStack (#5)
> **Status**: Complete
> **Stories**: 4 created — 2 Logic, 1 Config/Data, 1 Visual/Feel (4/4 Complete)

## Overview

实现《回响》中所有用户界面的基础架构。基于 Unity UI Toolkit (`com.unity.ui`) 构建——封装 LIFO 面板栈管理器（PushPanel/PopPanel/ReplaceTop，最大深度 10），提供面板互斥输入门控（栈非空 → UI Action Map，栈空 → Gameplay Action Map），定义全局 Theme.uss CSS 变量系统（6 类别：颜色/字体/间距/过渡/面板/按钮），以及键盘导航焦点管理（FocusController + UI Action Map 绑定）。HUD 作为持久底层存在于面板栈之外。UIPanelStack 不包含任何面板的具体内容——每个菜单和 HUD 的布局与逻辑由各自的 UI 系统负责。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0006: UI 框架 | UI Toolkit + LIFO 面板栈 (max 10) + MVVM 数据绑定 + Theme.uss 全局变量 | HIGH |
| ADR-0001: 事件总线架构 | UI 面板切换通过 static event 通知其他系统 | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-ui-framework-001 | Unity UI Toolkit 独占 (UGUI 弃用); 所有面板 UXML + USS | ADR-0006 ✅ |
| TR-ui-framework-002 | LIFO UIPanelStack (max depth 10) + PushPanel/PopPanel/ReplaceTop; HUD 为持久层 | ADR-0006 ✅ |
| TR-ui-framework-003 | FocusController + UI Action Map 键盘导航 | ADR-0006 ✅ |
| TR-ui-framework-004 | Theme.uss CSS 变量 6 类别全局样式定义 | ADR-0006 ✅ |
| TR-ui-framework-005 | 面板转场: opacity fade-in/fade-out USS transition (0.3s/0.2s) | ADR-0006 ✅ |
| TR-ui-framework-006 | 面板打开自动聚焦; PopPanel 恢复上次焦点位置 | ADR-0006 ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | UIPanelStack 核心引擎 + 输入门控 | Logic | Complete | ADR-0006 |
| 002 | Theme.uss 全局样式系统 | Config/Data | Complete | ADR-0006 |
| 003 | 面板过渡动画 | Visual/Feel | Complete | ADR-0006 |
| 004 | 键盘导航 + 焦点管理 | Logic | Complete | ADR-0006 |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/ui-framework.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

All stories complete. Run `/code-review` on modified files, then proceed to Feature layer epics.
