# Epic: 主菜单与菜单系统 (MainMenu)

> **Layer**: Feature
> **GDD**: design/gdd/main-menu.md
> **Architecture Module**: MainMenuController (#19)
> **Status**: Complete
> **Stories**: 4/4 Complete

## Overview

实现《回响》的"扉页"与"书签"——主菜单与菜单系统。管理四个界面组：标题画面（新游戏/继续/加载/设置/退出）、暂停菜单（继续/保存/加载/设置/返回标题）、设置面板（4 音量滑块 + 语言下拉）、存档管理 UI（3 槽位 + Save/Load 双模式 + 覆盖确认 + 加载确认）。所有面板通过 UI 框架 (#5) 的面板栈管理——MainMenuController 只定义 UXML 布局和按钮行为。MainMenu 场景拥有独立 UIDocument。暂停时 Time.timeScale=0 冻结 MonoBehaviour Update，UI Toolkit 事件系统保持响应。模态确认对话框覆盖 5 种场景。完整键盘导航——Arrow Keys/Tab 移动焦点、Enter 确认、Escape 返回/PopPanel。MVP 包含所有 5 个面板 + 继续 (auto_save) + 新游戏/加载/退出完整流程。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0006: UI 框架 | PanelStack PushPanel/PopPanel, Theme.uss, FocusController 键盘导航 | LOW |
| ADR-0001: 事件总线 | 暂停/恢复/切换场景经由 static events 通信 | LOW |
| ADR-0003: 存档 | Save/Load 面板消费 GetSlotMetaData, HasAnySave, SaveAsync, LoadAsync | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-main-menu-001 | 5 个 VisualElement 树（#title-screen, #pause-menu, #settings-panel, #save-load-panel, #modal-dialog） | ADR-0006 ✅ |
| TR-main-menu-002 | 面板栈管理委托 UI Framework PushPanel/PopPanel | ADR-0006 ✅ |
| TR-main-menu-003 | 暂停 Time.timeScale=0 + UI Toolkit 事件保持响应 | ADR-0006 ✅ |
| TR-main-menu-004 | "继续"按钮仅 auto_save 存在时可见 | ADR-0003 ✅ |
| TR-main-menu-005 | Save/Load 面板双模式（_saveLoadMode enum） | ADR-0003 ✅ |
| TR-main-menu-006 | 5 种确认对话框场景（本地化消息） | ⚠️ No dedicated ADR |
| TR-main-menu-007 | 完整键盘导航（Arrow/Tab/Enter/Escape） | ADR-0006 ✅ |

Note: ⚠️ items are covered by GDD directly.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/main-menu.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 标题画面 + 暂停菜单 | UI | Complete | ADR-0006 |
| 002 | 设置面板 + 模态确认对话框 | UI | Complete | ADR-0006 |
| 003 | 存档管理面板（Save/Load 双模式） | Integration | Complete | ADR-0006, ADR-0003 |
| 004 | 新游戏/继续/加载/退出流程 | Integration | Complete | ADR-0006, ADR-0003, ADR-0011 |
