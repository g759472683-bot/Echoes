# 输入系统 (Input System)

> **Status**: In Design
> **Author**: 用户 + game-designer + gameplay-programmer
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Pillar 4 (画卷中有呼吸) — 间接支撑

## Overview

输入系统是《回响》中所有玩家操作的统一入口。它封装 Unity Input System Package 的底层 API，为上层系统（UI 框架、记忆画卷交互系统、无障碍系统）提供一致的输入接口——包括键盘/鼠标的按键检测与状态查询、鼠标位置追踪、以及运行时按键重绑定。输入系统不包含任何游戏逻辑，它的唯一职责是将硬件输入转化为标准化的输入事件，供游戏系统消费。没有输入系统，玩家无法与记忆画卷中的任何物件交互——"触碰记忆"这个核心动词在代码层面就无从开始。

## Player Fantasy

在《回响》中，鼠标是游魂的手。移动光标穿过一幅记忆画卷不是操作系统界面——是在一段已经活过的人生中漂流。每一次悬停都是一次注目：你在看这段记忆，这段记忆也在回应你的注视。每一次点击不是扣动扳机，而是用手指轻触旧相册中的一页——页面因为你在那里而有了响应。

输入系统不存在"快"。它塑造玩家行动的自然节奏——不急不迫、不催促。游魂在画卷中的漂移没有时间限制，没有需要快速反应的挑战。点击永远是一个被接受的邀请，不是一道必须完成的指令。

## Detailed Design

### Core Rules

**规则 1 — 输入架构**：使用 Unity Input System Package 的 Input Actions 资产 + Generate C# Class 模式。所有输入访问通过生成的 `PlayerControls` 类进行，不直接访问 `Keyboard.current` / `Mouse.current`。设置 `Active Input Handling` 为 `Input System Package (New)` 独占模式。

**规则 2 — 动作映射**：定义两个 Action Map：

| Action Map | 用途 | 激活条件 |
|------------|------|----------|
| `Gameplay` | 记忆画卷交互 | 正常游戏进行中 |
| `UI` | 菜单与界面操作 | 任意模态 UI 打开时 |

**规则 3 — Gameplay 动作列表**（9 个动作）：

| 动作名 | 类型 | 绑定 | 用途 |
|--------|------|------|------|
| Point | Value (Vector2), Pass-Through | Mouse Position | 鼠标位置——悬浮检测 |
| Click | Button | Mouse Left Button | 触碰记忆物件 / 做出选择 |
| Scroll | Value (Vector2) | Mouse Scroll Wheel | 画卷缩放或内容滚动 |
| Navigate | Value (Vector2) | Arrow Keys / D-Pad / Left Stick | 菜单选项导航 |
| Confirm | Button | Enter / Gamepad A | 确认选择 |
| Cancel | Button | Escape / Gamepad B | 取消 / 返回 |
| Pause | Button | Escape / Gamepad Start | 打开暂停菜单 |
| TabNext | Button | Tab | 跳到下一个 UI 焦点组 |
| TabPrevious | Button | Shift+Tab | 跳到上一个 UI 焦点组 |

**规则 4 — 输入优先级**（两层门控）：
- **第 1 层（最高）— 模态 UI**：暂停菜单、选择对话框、章节过渡等打开时，禁用 `Gameplay` Action Map，启用 `UI` Action Map。零游戏输入处理。
- **第 2 层 — 非模态 UI**：HUD 元素、交互反馈叠加层。通过 `EventSystem.current.IsPointerOverGameObject()` 检测——如果鼠标在 UI 元素上，游戏输入被抑制。
- **第 3 层 — Gameplay**：只有未被第 1、2 层消费的输入才传递给记忆画卷交互系统。

**规则 5 — 悬浮检测架构**：由一个集中的 `HoverDetector` 组件在 `Update()` 中轮询鼠标位置，执行单次 `Physics2D.OverlapPoint()`（non-alloc，限制在 `Interactable` 物理层），对比上一帧的悬浮对象，分发 `OnHoverEnter` / `OnHoverExit` 事件。交互对象不在各自 Update 中独立做射线检测。

**规则 6 — 手柄支持范围**：手柄仅用于菜单导航。`Gameplay` Action Map 中不启用手柄绑定。手柄的 D-Pad、Left Stick、A、B、Start 仅在 `UI` Action Map 中有效。如果未检测到已连接的手柄（`Gamepad.current == null`），手柄相关 UI 提示不显示。

**规则 7 — 按键重绑定**：支持运行时按键重绑定，使用 `PerformInteractiveRebinding()` API。重绑定数据通过 `SaveBindingOverridesAsJson()` / `LoadBindingOverridesFromJson()` 持久化到 `PlayerPrefs`。重绑定界面位于设置菜单中。仅键盘/鼠标按键可重绑定——手柄绑定保持默认。

**规则 8 — 设备热插拔**：在 `Update()` 中检测设备变化。如果检测到新手柄连接，UI 导航提示更新。Input System 自动追踪设备连接，无需手动刷新。

### States and Transitions

| 状态 | 激活的 Action Map | 触发条件 |
|------|-------------------|----------|
| **Gameplay** | Gameplay | 游戏启动默认；从任意模态 UI 关闭后切回 |
| **Menu** | UI | 玩家按 Pause（暂停菜单）、触发选择对话框、进入章节过渡 |
| **Rebinding** | 仅被重绑定的单个 Action | 玩家在设置中点击"修改按键"，进入交互式重绑定流程 |
| **Inactive** | 无 | 加载画面、过场动画、引擎启动/关闭期间 |

**状态转换表**：

| 从 → 到 | 触发 | 行为 |
|----------|------|------|
| Gameplay → Menu | `Pause` 动作 performed | 禁用 Gameplay map，启用 UI map |
| Menu → Gameplay | `Cancel` 动作 performed（且无子菜单打开） | 禁用 UI map，启用 Gameplay map |
| Menu → Rebinding | 设置界面中点击"修改按键" | 仅激活目标 Action 的重绑定，其余输入暂停 |
| Rebinding → Menu | 重绑定完成、超时（30秒）、或用户取消 | 恢复 UI map，保存/丢弃重绑定结果 |
| Any → Inactive | 场景加载开始 / 过场开始 | 禁用所有 Action Map |
| Inactive → Gameplay | 场景加载完成 / 过场结束 | 根据上下文恢复 Gameplay 或 UI map |

### Interactions with Other Systems

| 方向 | 系统 | 数据流向 | 接口 |
|------|------|----------|------|
| 下游 | **UI 框架** (#5) | 输入系统 → UI框架 | `Navigate` (Vector2), `Confirm` (event), `Cancel` (event), `TabNext` (event), `TabPrevious` (event), `Point` (Vector2, 用于鼠标悬浮检测) |
| 下游 | **记忆画卷交互系统** (#11) | 输入系统 → 交互系统 | `Point` (Vector2, 持续), `Click` (event), `Scroll` (Vector2)。通过 `HoverDetector` 组件接收 `OnHoverEnter` / `OnHoverExit` 事件 |
| 下游 | **无障碍系统** (#22, VS) | 扩展输入 | 未来在 Gameplay map 中添加键盘/手柄画卷导航动作，由无障碍系统配置 |

## Formulas

输入系统不包含数学公式。它是一个事件转发层——将硬件输入转化为标准化的输入事件或连续值，供上层系统消费。所有输入值保持原始精度（鼠标位置为屏幕像素坐标，滚轮为归一化 Vector2，按钮为布尔状态）。任何需要将输入值转化为游戏世界坐标或交互参数的转换（如 `ScreenToWorldPoint`）由消费系统负责，不属于输入系统的职责范围。

## Edge Cases

- **如果没有鼠标连接**：游戏在 Gameplay 状态下暂停，显示"请连接鼠标以继续"提示。Menu 状态下键盘导航仍然可用。游戏启动时检测——如果无鼠标，主菜单仍然可以通过键盘操作。
- **如果手柄在 Menu 状态下断开连接**：焦点保持在当前 UI 元素上，手柄提示消失，键盘提示出现。导航继续通过键盘工作。
- **如果玩家将两个动作绑定到同一个按键**：后绑定的胜出，之前的绑定被清除。系统不会阻止玩家创建有问题的绑定——由玩家自行管理。
- **如果玩家重绑定了一个关键按键（如将 Pause 重绑定到原本是 Confirm 的键）**：Confirm 的绑定被清除，Confirm 变为未绑定状态。下次打开菜单时，未绑定的动作在设置中显示警告图标。
- **如果同一帧内同时触发 Click 和 Pause（同为 Escape 键在不同上下文）**：由于输入优先级规则——模态 UI 开关通过 Action Map 切换控制——Gameplay 状态下 Pause 事件先处理（切换到 Menu map），同一帧内 Click 事件被抑制（Gameplay map 已禁用）。
- **在滚轮最大值处**：滚轮值由硬件限制，不存在"溢出"问题。Input System 返回的 Vector2 值直接传递，不做 clamp。
- **如果 Input System 初始化失败（极罕见）**：降级为最小化的直接设备轮询循环，并在屏幕上显示警告文本"输入系统异常，请重启游戏"。
- **如果玩家在重绑定过程中断开正在重绑定的设备**：重绑定操作超时（30秒），恢复到重绑定前的绑定，显示"设备已断开，重绑定取消"消息。
- **如果 Actions Asset 文件缺失或损坏**：游戏在启动画面中显示错误信息并阻止进入主菜单。不尝试以无输入配置运行。

## Dependencies

**硬依赖（系统无法运行）**：无。输入系统是 Foundation 层第一个系统，无上游依赖。

**下游系统（依赖本系统）**：

| 系统 | 依赖性质 | 接口 |
|------|----------|------|
| UI 框架 (#5) | 硬依赖 | Navigate, Confirm, Cancel, TabNext, TabPrevious, Point |
| 记忆画卷交互系统 (#11) | 硬依赖 | Point, Click, Scroll, HoverDetector 事件 |
| 无障碍系统 (#22, VS) | 软依赖 | 未来扩展键盘/手柄画卷导航 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| Gamepad Stick Dead Zone | 0.15 | 0.05–0.30 | 低于此值的手柄摇杆输入被忽略。过高会使菜单导航迟钝，过低会使漂移手柄产生误触 |
| Rebind Timeout | 30s | 15–60s | 重绑定等待输入的最长时间。太短给玩家压力，太长让其他输入冻结过久 |
| Scroll Sensitivity | 1.0 | 0.1–3.0 | 滚轮输入的倍率。由记忆画卷交互系统读取和应用 |
| Hover Poll Rate | Every Frame | Every Frame / Every 2nd Frame / Every 3rd Frame | 悬浮检测的轮询频率。默认每帧。如果性能预算紧张，可降至隔帧但不低于每3帧一次（在 60fps 下即 20Hz——仍然平滑） |

## Visual/Audio Requirements

输入系统本身不产生视觉或音频输出。交互反馈的视觉表现（悬浮高亮、点击波纹）和音频表现（触碰音效）由交互反馈系统 (#18) 负责。

## UI Requirements

输入系统本身没有独立的 UI。其唯一需要 UI 的功能是**按键重绑定界面**——该界面属于设置菜单，由主菜单与菜单系统 (#19) 实现。输入系统通过 `PerformInteractiveRebinding()` API 提供重绑定能力，菜单系统调用它。

## Acceptance Criteria

- **GIVEN** 游戏首次启动，**WHEN** 引擎完成初始化，**THEN** `PlayerControls` 实例被创建，`Gameplay` Action Map 被启用，`UI` Action Map 被禁用。
- **GIVEN** 玩家在记忆画卷中移动鼠标，**WHEN** 鼠标悬浮在一个带有 `Collider2D`（Interactable 层）的物件上方，**THEN** `HoverDetector` 检测到碰撞并触发 `OnHoverEnter` 事件，携带物件引用和屏幕坐标。
- **GIVEN** 鼠标悬浮在一个可交互物件上，**WHEN** 玩家按下鼠标左键，**THEN** `Click` 事件被触发，携带点击位置的屏幕坐标。
- **GIVEN** 玩家按下 Escape 键（Gameplay 状态），**WHEN** 当前无模态 UI 打开，**THEN** `Pause` 动作触发，`Gameplay` map 被禁用，`UI` map 被启用，暂停菜单显示。
- **GIVEN** 暂停菜单打开（UI 状态），**WHEN** 玩家移动鼠标到菜单按钮并点击，**THEN** `EventSystem.current.IsPointerOverGameObject()` 返回 `true`，`HoverDetector` 和 `Click` 事件不传递给记忆画卷交互系统。
- **GIVEN** 暂停菜单打开，**WHEN** 玩家按下 Escape 或 Gamepad B 按钮，**THEN** `Cancel` 动作触发，暂停菜单关闭，`UI` map 被禁用，`Gameplay` map 被启用。
- **GIVEN** 无手柄连接，**WHEN** 游戏在任意状态下运行，**THEN** UI 中不显示手柄按钮提示（如 "Press A to confirm"），仅显示键盘提示。
- **GIVEN** 游戏运行中，**WHEN** 手柄被插入，**THEN** `Gamepad.current` 变为非 null，UI 导航提示更新为同时显示键盘和手柄提示。
- **GIVEN** 玩家在设置界面点击"修改按键"，**WHEN** 选择了 `Confirm` 动作进行重绑定，**THEN** 系统进入 Rebinding 状态——仅监听下一个按键输入，30秒超时后自动恢复原绑定。
- **GIVEN** 玩家完成了 `Confirm` 动作的重绑定（按下了新按键），**WHEN** 重绑定操作完成，**THEN** 新绑定被保存到 `PlayerPrefs`，旧绑定被覆盖，重绑定界面显示新按键名称。
- **GIVEN** 输入系统的 Actions Asset 文件缺失，**WHEN** 游戏启动并尝试加载该资产，**THEN** 游戏显示错误信息并阻止进入主菜单（不崩溃也不以无配置状态运行）。
- **GIVEN** 场景加载开始，**WHEN** 加载画面显示，**THEN** 所有 Action Map 被禁用（Inactive 状态），加载期间不处理任何输入。

## Open Questions

- **滚轮的最终用途**：当前 Gameplay 下的 `Scroll` 动作已被定义，但其具体行为（画卷缩放 vs 碎片间滚动 vs 内容平移）由记忆画卷交互系统决定。交互系统设计时需要明确指定滚轮的消费方式。
- **选择对话框中的滚轮行为**：当选择对话框（模态 UI）打开时，滚轮是被 UI map 消费用于滚动选项，还是仍然传递到 Gameplay？建议模态 UI 打开时所有输入归 UI——但需在设计 UI 框架时确认。
- **手柄画卷导航方案**（留给无障碍系统 #22）：当 VS 阶段加入键盘/手柄的画卷导航时，需要定义是光标模拟方案（键盘移动一个虚拟光标）、吸附方案（按方向键跳到最近的可交互物件）、还是自由移动方案（键盘控制游魂在画卷中平移）。输入系统需要提前知道要添加什么类型的动作（Button vs Value）。
