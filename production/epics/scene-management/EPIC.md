# Epic: 场景管理系统 (SceneManager)

> **Layer**: Foundation
> **GDD**: design/gdd/scene-management.md
> **Architecture Module**: SceneManager (#6)
> **Status**: Ready
> **Stories**: 5 created — 2 Logic, 2 Integration, 1 Visual/Feel

## Overview

实现《回响》中记忆画卷空间的加载、卸载与过渡管理。采用 3 场景架构（Boot → MainMenu → Game），Game 场景作为持久容器，所有记忆碎片内容通过 Addressables 注入。统一使用 SceneFader 全屏墨迹遮罩（UI Toolkit VisualElement opacity 过渡）覆盖所有转场——碎片间 (1.0s)、章节间 (fade+load+fade)、场景间。转场期间自动屏蔽输入（Action Map → Inactive），并在 OnFragmentTransitionStarted/Transitioned 事件中协调交互反馈的抑制与恢复。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: 场景管理 | 3 场景架构 + 转场状态机 + SceneFader 墨迹遮罩 + 预加载触发 | LOW |
| ADR-0001: 事件总线架构 | OnFragmentTransitionStarted/Transitioned static events (4-5 订阅者) | LOW |
| ADR-0002: 数据管理 | 依赖 DataManager 的 PreloadChapterAsync/GetFragmentAsync/GetIllustrationAsync | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-scene-management-001 | 3-scene 架构 (Boot/MainMenu/Game) + Addressables 内容注入; Game 场景加载一次 | ADR-0004 ✅ |
| TR-scene-management-002 | Transition state machine + SceneFader 全屏墨迹遮罩 + UI Toolkit opacity 过渡 | ADR-0004 ✅ |
| TR-scene-management-003 | OnFragmentTransitionStarted(chapterKey,fragmentId) 和 OnFragmentTransitioned 事件 | ADR-0004 ✅ |
| TR-scene-management-004 | 预加载触发 ≤3 fragments — Task.WhenAll 并行预加载插图+音频 | ADR-0004 ✅ |
| TR-scene-management-005 | 碎片转场 1.0s (0.5+0.5); 章节转场 1.0s fade + load + 1.0s fade | ADR-0004 ✅ |
| TR-scene-management-006 | 所有转场期间 Action Map → Inactive 防止误操作 | ADR-0004 ✅ |
| TR-scene-management-007 | 错误恢复: 碎片失败→返回章节开头; 章节失败→返回主菜单; 场景失败→退出 | ADR-0004 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/scene-management.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 3 场景架构 + Boot 初始化 | Integration | Ready | ADR-0004, ADR-0001, ADR-0002 |
| 002 | SceneFader 墨韵过渡效果 | Visual/Feel | Ready | ADR-0004 |
| 003 | 碎片过渡引擎 + 事件 | Logic | Ready | ADR-0004, ADR-0001 |
| 004 | 章节过渡 + 预加载协调 | Integration | Ready | ADR-0004, ADR-0002 |
| 005 | 错误恢复 | Logic | Ready | ADR-0004 |

## Next Step

Run `/story-readiness production/epics/scene-management/story-001-3-scene-architecture-boot-init.md` to begin implementation.
