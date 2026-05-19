# 主菜单与菜单系统 (Main Menu & Menus)

> **Status**: Designed (pending review)
> **Author**: 用户 + ui-programmer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 4 (画卷中有呼吸) — 菜单是画卷外的"扉页"，手感延续水墨美学

## Overview

主菜单与菜单系统是《回响》的"扉页"与"书签"——它是玩家进入画卷前看到的第一幅墨迹，也是游戏中途暂停时铺在画卷上的那层薄纸。它管理四个界面组：**标题画面**（新游戏/继续/加载/设置/退出）、**暂停菜单**（继续/保存/加载/设置/返回标题）、**设置面板**（音频音量 + 语言选择）、**存档管理 UI**（槽位列表 + 覆盖确认 + 加载确认）。所有界面通过 UI 框架 (#5) 的面板栈管理——主菜单系统不处理面板栈逻辑，只定义每个面板的 UXML 布局和按钮行为。

技术层面，它是一个 `MainMenuController` MonoBehaviour，挂载在 MainMenu 场景的 UIDocument 上。它消费存档系统 (#7) 的槽位元数据和章节管理 (#15) 的进度数据，将它们渲染为墨迹风格的菜单项。不产生游戏逻辑——只负责"展示选项，把玩家的选择传回系统"。

## Player Fantasy

菜单不是"UI 界面"——它是画册的封面、扉页、和插在书页间的书签。

标题画面不轰炸感官——它是一张墨色未干的宣纸。游戏标题是手写的四个字，墨迹从纸上微微洇开。菜单选项不是按钮——它们是画面底部渐渐浮现的墨笔字迹："新游戏"的墨最浓，"继续"在存档存在时才显现（像被记忆唤出的痕迹），"加载游戏"是淡墨——因为它在等待你告诉它该翻到哪一页。

暂停不是"冻结游戏"。它是一张薄纸被轻轻铺在画卷上——画卷透过纸依稀可见，但画面柔和了，周围的声音也轻了。你在这张纸上做三件事：存一个书签、翻到一个旧书签、或者合上画册回到封面。当你选择"继续"，纸被抽走——不是弹走，是像有人从桌面上轻轻揭起一张纸——画卷重新在你面前完整展开，墨迹继续流动。

设置面板不是"选项菜单"——它是你在调整这幅画的观看方式。音量滑块不是冰冷的横条——它们是墨色从淡到浓的渐变。语言切换不是下拉框——它是一行字从"中文"变成"English"时，墨迹在纸上重新排列的细微过程。

当菜单做对了，玩家在标题画面会停 10 秒——不是因为在加载，而是因为那幅作为背景的记忆画卷让他们想先看一会儿。

## Detailed Design

### Core Rules

**规则 1 — 菜单架构：UI Toolkit 面板 + 面板栈**

MainMenu 场景拥有自己的 UIDocument。所有菜单面板是此 UIDocument 内的 VisualElement 树：

```
MainMenu UIDocument
├── #title-screen                 // 标题画面根 (默认可见)
│   ├── #game-logo                // 手写游戏标题
│   ├── #menu-buttons             // 菜单按钮容器
│   │   ├── #btn-new-game         // 新游戏
│   │   ├── #btn-continue         // 继续 (条件可见)
│   │   ├── #btn-load-game        // 加载游戏
│   │   ├── #btn-settings         // 设置
│   │   └── #btn-quit             // 退出
│   └── #background-painting      // 背景水墨画 (静态或微动)
├── #pause-menu                   // 暂停菜单根 (默认隐藏)
│   ├── #btn-resume
│   ├── #btn-save-game
│   ├── #btn-load-game-pause
│   ├── #btn-settings-pause
│   └── #btn-return-to-title
├── #settings-panel               // 设置面板 (默认隐藏)
│   ├── #audio-section
│   │   ├── #slider-master
│   │   ├── #slider-sfx
│   │   ├── #slider-music
│   │   └── #slider-ambience
│   ├── #language-section
│   │   └── #dropdown-language
│   └── #btn-settings-back
├── #save-load-panel              // 存档管理面板 (默认隐藏)
│   ├── #slot-list                // 3 槽位容器
│   │   ├── #slot-01
│   │   ├── #slot-02
│   │   └── #slot-auto
│   └── #btn-save-load-back
└── #modal-dialog                 // 模态确认对话框 (默认隐藏)
    ├── #modal-message
    ├── #btn-modal-confirm
    └── #btn-modal-cancel
```

**面板栈管理** (委托 UI 框架 #5):

| 场景 | 面板栈 | 说明 |
|------|--------|------|
| 标题画面 | `[#title-screen]` | 单层面板 |
| 标题→设置 | `[#title-screen, #settings-panel]` | Settings push |
| 标题→加载 | `[#title-screen, #save-load-panel]` | Load push |
| 暂停 | `[HUD(隐藏), #pause-menu]` | Pause push，HUD 降暗 |
| 暂停→设置 | `[HUD(隐藏), #pause-menu, #settings-panel]` | Settings push |
| 暂停→保存 | `[HUD(隐藏), #pause-menu, #save-load-panel]` | Save push |
| 确认对话框 | `[..., #modal-dialog]` | Modal push (栈顶) |

- 标题画面使用 `PushPanel` / `PopPanel` 管理子面板
- 暂停菜单是独立的场景级面板——InGame 场景按下 Escape → SceneManager 不切换场景，而是 UI 框架在 Game 场景中 PushPanel
- 确认对话框始终 Push 到栈顶——关闭时 PopPanel

**规则 2 — 标题画面 (Title Screen)**

```
启动流程:
  1. 游戏启动 → 加载 MainMenu 场景
  2. MainMenuController.Awake():
     a. 检查 SaveManager.HasAnySave() → 决定 #btn-continue 可见性
     b. 加载语言设置 (PlayerPrefs) → 刷新所有文本
     c. 加载音量设置 (PlayerPrefs) → 应用到 AudioManager
  3. #title-screen 淡入 (UI 框架 .fade-in, 0.5s——标题画面可稍慢)
  4. 键盘焦点自动移到 #btn-new-game (或 #btn-continue 若存档存在)
```

按钮行为:

| 按钮 | 条件 | 行为 |
|------|------|------|
| 新游戏 | 始终可见 | 若有 auto_save → 弹出确认对话框"开始新游戏将覆盖当前进度" → 确认 → StartNewGame()。若无存档 → 直接 StartNewGame() |
| 继续 | auto_save 存在时可见 | LoadAndRestore("auto_save") → 直接进入游戏 |
| 加载游戏 | 始终可见 | PushPanel → #save-load-panel (Load 模式) |
| 设置 | 始终可见 | PushPanel → #settings-panel |
| 退出 | 始终可见 | Application.Quit() |

- "继续"按钮在无存档时隐藏——不显示为灰色禁用态
- 背景是一幅静态或微动的水墨画——MVP 阶段为静态插图

**规则 3 — 暂停菜单 (Pause Menu)**

```
暂停触发:
  - Gameplay 状态下按 Escape (UI Action Map 的 Cancel)
  - UI 框架: PushPanel(#pause-menu) → InputSystem.SwitchToUIMode() → 游戏暂停 (Time.timeScale = 0)
  - #pause-menu 以 .fade-in (0.3s) 出现
  - HUD (#17) 降暗 (opacity 降至 0.3)

暂停恢复:
  - 按 Escape (Cancel) 或点击"继续"
  - UI 框架: PopPanel() → 若栈空 → SwitchToGameplayMode() → Time.timeScale = 1
  - HUD 恢复全亮

暂停面板按钮:
  - 继续: PopPanel → 恢复游戏
  - 保存游戏: PushPanel → #save-load-panel (Save 模式)
  - 加载游戏: PushPanel → #save-load-panel (Load 模式)
  - 设置: PushPanel → #settings-panel
  - 返回标题画面: PushPanel → #modal-dialog (确认"未保存的进度将丢失") → 确认 → SaveManager.SaveAsync("auto_save") → SceneManager.LoadScene("MainMenu")
```

- `Time.timeScale = 0` 暂停所有 MonoBehaviour Update + 微动画 + 场景动画。音频系统 (#3) 将环境音和音乐音量降至 pausedLevel (默认原音量的 0.3)
- 暂停菜单打开时，画面被一层半透明墨色遮罩覆盖（#pause-overlay, 在 UIDocument 内）

**规则 4 — 设置面板 (Settings Panel)**

设置面板在标题画面和暂停菜单中均可打开——相同面板，不同入口：

```
Settings Panel 结构:
  Audio:
    Master Volume:  Slider 0.0–1.0, 默认 0.8
    SFX Volume:     Slider 0.0–1.0, 默认 0.7
    Music Volume:   Slider 0.0–1.0, 默认 0.6
    Ambience Volume: Slider 0.0–1.0, 默认 0.5
  
  Language:
    下拉列表, 默认 "中文"
    MVP 选项: 中文, English
```

音量滑块行为:
- 拖动滑块 → 实时调整 AudioManager.SetVolume(category, value) — 即时听到效果
- 值保存到 PlayerPrefs (持久化——不依赖存档)
- 存档保存/加载时也会恢复音量设置（存档系统规则 8）

语言切换行为:
- 选择新语言 → 触发 `LocaleSettings.SelectedLocale = newLocale`
- Unity Localization Package 自动刷新所有当前可见面板的 LocalizedString
- 语言选择保存到 PlayerPrefs + 当前存档（若游戏中）
- 切换语言后设置面板自身也刷新

关闭设置:
- 点击"返回" (或 Escape) → PopPanel — 回到标题画面或暂停菜单
- 设置是即时应用的——没有"保存设置"按钮

**规则 5 — 存档管理面板 (Save/Load Panel)**

两种模式：Save 模式和 Load 模式——同一面板，不同按钮行为：

```
Save 模式 (从暂停菜单进入):
  标题: "保存游戏"
  3 个槽位显示:
    - 空槽: "— 空 —" (淡墨)
    - 已有存档: 时间戳 + 章节名 + 进度
  点击槽位:
    - 空槽: 直接保存到该槽位 → 显示"保存完成" → PopPanel
    - 已有存档: PushPanel → #modal-dialog "覆盖此存档？" → 确认 → 保存 → PopPanel ×2 → 回到暂停菜单

Load 模式 (从标题画面或暂停菜单进入):
  标题: "加载游戏"
  3 个槽位显示:
    - 空槽: "— 空 —" (不可交互)
    - 已有存档: 时间戳 + 章节名 + 进度
  点击已有存档槽位:
    - 暂停菜单中: PushPanel → #modal-dialog "加载此存档？未保存的进度将丢失" → 确认 → LoadAsync
    - 标题画面中: 直接 LoadAsync (无"未保存进度"问题)
```

每个槽位显示的信息:

| 字段 | 数据来源 | 格式 |
|------|---------|------|
| 槽位标签 | 固定 | "存档 1", "存档 2", "自动存档" |
| 存档时间 | SaveData.Timestamp | "2026年5月12日 14:30" |
| 当前章节 | SaveData.CurrentChapterKey → ChapterDefinition.DisplayNameKey → 本地化 | "第一章 · 童年" |
| 游玩时长 | SaveData.PlayTimeSeconds | "1h 23m" |
| 空槽 | SaveManager.SlotExists(slotId) = false | "— 空 —" (淡墨色) |

槽位元数据通过 `SaveManager.GetSlotMetaData(slotId)` 获取——不加载完整存档数据，只读取头部字段：

```csharp
public struct SlotMetaData
{
    public string SlotId;
    public bool IsEmpty;
    public string Timestamp;          // ISO 8601
    public string ChapterNameKey;     // 本地化 Key
    public int VisitedFragmentCount;
    public int TotalFragments;
    public int PlayTimeSeconds;
}
```

**规则 6 — 模态确认对话框 (Modal Dialog)**

```
#modal-dialog 结构:
  #modal-message: "开始新游戏将覆盖当前进度。确定继续？"
  #btn-modal-confirm: "确定" (墨色实心)
  #btn-modal-cancel: "取消" (淡墨空心)

行为:
  - PushPanel 到栈顶 → 下层 UI 面板视觉降暗但不响应输入
  - Confirm → 执行待确认操作 → PopPanel ×2 (对话框 + 触发面板)
  - Cancel → PopPanel (仅关闭对话框)
  - 键盘: Enter = Confirm, Escape = Cancel
```

确认场景和消息:

| 触发操作 | 消息 Key | 默认消息 |
|---------|---------|---------|
| 新游戏 (有现有存档) | `menu.confirm.new_game` | "开始新游戏将覆盖当前进度。确定继续？" |
| 覆盖存档 | `menu.confirm.overwrite` | "覆盖此存档？此操作不可撤销。" |
| 加载存档 (游戏中) | `menu.confirm.load_in_game` | "加载此存档？当前未保存的进度将丢失。" |
| 返回标题 (游戏中) | `menu.confirm.return_to_title` | "返回标题画面？未保存的进度将丢失。" |
| 退出游戏 | `menu.confirm.quit` | "退出游戏？" |

**规则 7 — 新游戏流程**

```
StartNewGame():
  1. #title-screen 淡出 (0.3s)
  2. 调用 ChapterManager.StartNewGame():
     a. 初始化所有跨章节 Flag (CrossChapterTracker.InitializeAllFlags)
     b. 设置 CurrentChapterKey = 第一个章节
     c. State = TRANSITIONING
  3. SceneManager.LoadScene("InGame")
  4. SceneManager 负责过渡效果和执行
  5. ChapterManager.State = IN_CHAPTER, 触发 OnChapterStarted
```

**规则 8 — 继续 / 加载流程**

```
Continue():
  → LoadGame("auto_save")

LoadGame(slotId):
  1. #title-screen (或 #pause-menu) 淡出 (0.3s)
  2. await SaveManager.LoadAsync(slotId):
     a. 反序列化 SaveData
     b. 校验 Checksum
     c. 版本迁移 (若需要)
  3. await ChapterManager.LoadAndRestore(saveData):
     a. 恢复章节进度、碎片位置
     b. 恢复 ChangeOverlay, CrossChapterFlags
  4. SceneManager.LoadScene("InGame") + 过渡
  5. 若在暂停菜单中: Time.timeScale = 1
```

**规则 9 — 输入与键盘导航**

| 上下文 | 按键 | 行为 |
|--------|------|------|
| Gameplay | Escape | 打开暂停菜单 |
| 暂停菜单 | Escape | 关闭暂停菜单 (PopPanel) |
| 标题画面子面板 | Escape | PopPanel 回到标题画面 |
| 标题画面根 | Escape | 若子面板打开 → PopPanel。若仅标题→弹出退出确认对话框 |
| 任何确认对话框 | Enter | Confirm |
| 任何确认对话框 | Escape | Cancel |
| 所有菜单面板 | Arrow Keys / Tab | 焦点在可交互元素间移动 (UI 框架 FocusController) |
| 滑块聚焦 | Arrow Left/Right | 调整值 ±0.05 |
| 下拉框聚焦 | Arrow Up/Down | 切换选项 |

- 所有按钮支持 Enter 和鼠标点击
- 保存/加载槽位支持 Enter 和鼠标点击
- 面板打开时自动聚焦到第一个可交互元素

**规则 10 — 暂停状态下的特殊处理**

```
Time.timeScale = 0 时:
  ✓ UI Toolkit 事件系统正常运作 (不受 timeScale 影响)
  ✓ UI 动画 (USS transition) 正常运作
  ✓ 音频系统 (#3) 环境音/音乐降低到 pausedLevel
  ✗ 微动画 (#9) 暂停 (MonoBehaviour Update 受 timeScale 影响)
  ✗ 关联引擎 (#13) — 不需要暂停，暂停时不调用
  ✗ 场景管理 (#6) — 不触发过渡

MVP 策略:
  微动画系统 (#9) 的 MonoBehaviour Update 在 timeScale=0 时自然暂停
  → "画卷静止"——这是合理的暂停隐喻
  若 Vertical Slice 需要背景微动: 使用 Time.unscaledDeltaTime
```

**规则 11 — MVP 范围**

MVP 包含:
- 标题画面 (5 个按钮)
- 暂停菜单 (5 个按钮)
- 设置面板 (4 音量滑块 + 语言下拉)
- 存档管理面板 (3 槽位, Save/Load 双模式)
- 模态确认对话框
- 继续 (auto_save) 功能
- 新游戏/加载/退出完整流程

MVP 不包含:
- 输入绑定配置 (Full Vision)
- 图形设置 (分辨率/全屏 — PC 游戏默认使用 Unity 启动器设置)
- 制作人员名单 (Vertical Slice — 与结局呈现 #20 一起做)
- 存档槽位缩略图 (Vertical Slice — 仅显示文字信息)
- 章节选择界面 (#21, Vertical Slice — 独立系统)

### States and Transitions

主菜单自身的状态由当前打开的面板决定——无独立状态机。但整体菜单/Gameplay 切换有清晰的状态：

| 状态 | 描述 | 面板栈 |
|------|------|--------|
| **TitleScreen** | 标题画面显示中 | [#title-screen] |
| **TitleScreen_SubPanel** | 标题画面的子面板打开 (设置/加载) | [#title-screen, #sub-panel] |
| **TitleScreen_Modal** | 标题画面的确认对话框 | [#title-screen, ..., #modal-dialog] |
| **InGame** | 游戏中，无菜单 | [] (HUD 可见) |
| **Paused** | 暂停菜单打开 | [#pause-menu] |
| **Paused_SubPanel** | 暂停菜单的子面板打开 (设置/保存/加载) | [#pause-menu, #sub-panel] |
| **Paused_Modal** | 暂停菜单的确认对话框 | [#pause-menu, ..., #modal-dialog] |
| **Loading** | 加载/保存进行中 | 显示加载遮罩 |

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **UI 框架 (#5)** | PushPanel, PopPanel, ReplaceTop, 面板栈状态, Theme.uss | 所有面板的打开/关闭/层叠管理 |
| 上游 | **存档系统 (#7)** | GetSlotMetaData(slotId), HasAnySave(), SaveAsync(slotId), LoadAsync(slotId) | 存档列表渲染、保存/加载操作 |
| 上游 | **章节管理 (#15)** | StartNewGame(), LoadAndRestore(SaveData), GetCompletedChapters(), GetUnlockedChapters() | 新游戏/继续/加载的入口 |
| 上游 | **输入系统 (#1)** | UI Action Map (Navigate, Confirm, Cancel, TabNext, TabPrevious) | 键盘导航和 Escape 暂停 |
| 上游 | **本地化 (#4)** | LocalizedString, LocaleSettings.SelectedLocale | 所有菜单文本、语言切换 |
| 下游 | **音频系统 (#3)** | SetVolume(category, value), PauseAll/ResumeAll | 音量滑块实时调整、暂停时降低音频 |
| 下游 | **场景管理 (#6)** | LoadScene("InGame"), LoadScene("MainMenu") | 场景切换 |
| 下游 | **跨章节状态追踪 (#16)** | InitializeAllFlags() | 新游戏时初始化 Flag |

## Formulas

本系统不含自定义数学公式。音量值是线性的 0.0–1.0。时间戳格式化使用 C# 标准库。游玩时长格式化：`$"{hours}h {minutes}m"`（hours=0 时省略）。

## Edge Cases

- **玩家在暂停菜单中，系统触发自动存档**: 自动存档规则 (存档系统 #7 规则 6) 在 Time.timeScale=0 时不触发——自动存档只在 Gameplay 状态执行。此场景不会发生。

- **玩家在保存面板中保存到槽 1，但保存失败 (磁盘满)**: SaveAsync 返回错误 → 主菜单系统收到异常 → 显示错误提示"保存失败：磁盘空间不足"→ 不关闭面板，玩家可重试或返回。

- **玩家在标题画面按 Escape**: 若子面板打开 → PopPanel。若仅标题画面 → 弹出退出确认对话框。Escape 不应在标题画面直接退出——防止误触。

- **玩家加载了一个旧版本存档，迁移失败**: SaveManager.LoadAsync 抛出异常 → 主菜单捕获 → 显示"存档与新版本不兼容"→ 返回标题画面。不崩溃。

- **玩家在暂停菜单打开状态下，Windows 锁屏/睡眠**: Unity 的 OnApplicationPause → 自动触发 auto_save (存档系统规则 6)。恢复后游戏仍在暂停状态。

- **玩家快速来回 Push/Pop 面板 (如双击设置按钮)**: UI 框架 Transitioning 状态拦截重复 Push (规则 7 Edge Cases)。面板过渡 0.3s——双击在 0.05s 内，第二次 Push 被忽略。

- **PlayerPrefs 中的语言设置为 "ja" 但当前构建不包含日文**: LocaleSettings 降级到默认语言 (中文)。下拉列表不显示不可用的语言选项——只列 LocalizationSettings.AvailableLocales 中的语言。

- **存档槽位全部为空时打开 Load Game 面板**: 3 个槽位全部显示"— 空 —"且不可交互。面板仍可打开——玩家可以查看，但无法加载。按 Escape 或点击"返回"关闭。

- **Save 模式和 Load 模式的面板同一时间只可能处于一种模式**: 由调用方决定——Save 从暂停菜单进入，Load 从标题画面或暂停菜单进入。面板在 Push 前设置 `_saveLoadMode` 枚举——运行时不可切换。

- **游戏退出时 (Application.Quit) 在 Editor 中无效**: Editor 中 `Application.Quit()` 不工作——使用 `EditorApplication.ExitPlaymode()` 在 `#if UNITY_EDITOR` 块中。Release Build 中 Application.Quit() 正常工作。

## Dependencies

### 硬依赖

| 系统 | 性质 | 接口 |
|------|------|------|
| **UI 框架 (#5)** | 硬依赖 — 面板栈、主题、导航 | PushPanel, PopPanel, ReplaceTop, Theme.uss, FocusController |
| **存档系统 (#7)** | 硬依赖 — 存档 CRUD + 元数据 | GetSlotMetaData(slotId), HasAnySave(), SaveAsync(slotId), LoadAsync(slotId) |
| **章节管理 (#15)** | 硬依赖 — 游戏入口 | StartNewGame(), LoadAndRestore(SaveData) |
| **输入系统 (#1)** | 硬依赖 — 键盘/鼠标输入 | UI Action Map (Navigate, Confirm, Cancel, TabNext, TabPrevious) |
| **本地化 (#4)** | 硬依赖 — 所有菜单文本 | LocalizedString, LocaleSettings |

### 软依赖

| 系统 | 性质 | 接口 |
|------|------|------|
| **音频系统 (#3)** | 软依赖 — 音量控制 | SetVolume(category, value) |
| **场景管理 (#6)** | 软依赖 — 场景切换 | LoadScene(sceneName) |
| **跨章节状态追踪 (#16)** | 软依赖 — 新游戏初始化 | InitializeAllFlags() |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 标题画面淡入时长 | 0.5s | 0.3–1.0s | 比普通面板稍慢——标题画面的初次印象 |
| 暂停时环境音/音乐降幅 | 0.3× | 0.1–0.5× | 暂停时的音频缩放因子 |
| 设置面板滑块步长 | 0.05 | 0.01–0.10 | Arrow Key 调整滑块的步长 |
| 游玩时长格式化精度 | 分钟 | — | <1h 显示分钟，≥1h 显示小时+分钟 |
| Save Slot Count | 2 manual + 1 auto | 固定值 | 来自存档系统规则 1 |

## Visual/Audio Requirements

- **标题画面背景**: 一幅静态水墨画——MVP 阶段为单张插图。内容：散落的画卷碎片漂浮在淡墨色空间中，碎片上有隐约的笔触痕迹。Vertical Slice 可加入 L1 微动画（尘埃漂浮、墨色缓缓流动）
- **手写标题**: 游戏标题"回响"使用手写体中文字体，颜色为浓墨色（近乎黑色但保留墨的质感——RGB 20, 15, 10）。标题下方可有微小的墨滴痕迹
- **菜单文字**: 使用手写体（与 HUD 相同字体——Theme.uss 的 `--font-family`）。"新游戏"/"继续"等菜单选项颜色为半透明墨色（hover 时逐渐变为浓墨）
- **暂停遮罩**: 半透明墨色覆盖层（rgba(15, 10, 5, 0.5)），让画卷透过遮罩依稀可见
- **按钮悬停**: 悬停时文字从淡墨变为浓墨（opacity 0.6→1.0, transition 0.2s），微放大 scale(1.02)
- **音效**: 按钮点击声由交互反馈 (#18) 管理——菜单系统不产生音频。但提出需求：标题画面按钮点击应使用 `sfx_touch_generic`——与游戏内触摸同一声音，保持一致性

## UI Requirements

MainMenu 场景的 UIDocument 承载所有菜单面板。UXML 定义 VisualElement 层次结构（见规则 1）。USS 引用 Theme.uss（`@import url("theme.uss");`）获取全局样式变量。所有文本通过本地化系统 (#4) 的 LocalizedString 绑定。

- **键盘导航**: 菜单面板打开时自动聚焦第一个可交互元素。Tab 在焦点组间移动。Enter 触发聚焦元素。Escape = 返回/Cancel。方向键在滑块和下拉框内调整值。
- **鼠标**: 所有按钮支持点击。滑块支持拖动（Slider 控件的默认行为）。悬停反馈（规则 6 视觉要求）。
- **响应式布局**: 菜单按钮垂直排列，居中。面板宽度固定为 400px（PC 屏幕）。MVP 不需要多分辨率适配——目标 1920×1080。

## Acceptance Criteria

- **GIVEN** 游戏首次启动（无存档），**WHEN** 标题画面显示，**THEN** "新游戏"、"加载游戏"、"设置"、"退出"按钮可见。"继续"按钮不可见。键盘焦点在"新游戏"上。

- **GIVEN** auto_save 存在，**WHEN** 标题画面显示，**THEN** "继续"按钮可见——显示在"新游戏"下方。键盘焦点在"继续"上。

- **GIVEN** 玩家在标题画面，**WHEN** 点击"新游戏"，**IF** auto_save 存在 → 弹出确认对话框。确认 → ChapterManager.StartNewGame() 被调用 → 场景切换到 InGame → Ch01 开始。若无 auto_save → 直接 StartNewGame()。

- **GIVEN** 玩家在标题画面，**WHEN** 点击"继续"，**THEN** SaveManager.LoadAsync("auto_save") 被调用 → ChapterManager.LoadAndRestore(saveData) → 场景切换到 InGame → 恢复到存档位置。

- **GIVEN** 玩家在 Gameplay 状态，**WHEN** 按 Escape，**THEN** 暂停菜单 PushPanel 到栈顶。Time.timeScale = 0。HUD 降暗。音频降低到 pausedLevel。

- **GIVEN** 暂停菜单打开，**WHEN** 玩家点击"继续"或按 Escape，**THEN** PopPanel 关闭暂停。Time.timeScale = 1。HUD 恢复。音频恢复。

- **GIVEN** 暂停菜单打开，**WHEN** 玩家点击"保存游戏"→ 选择槽 1 → 槽 1 已显示"第一章 · 童年 / 1h 23m" → 确认覆盖 → SaveManager.SaveAsync("save_01") 执行 → 存档完成。

- **GIVEN** 标题画面中打开"加载游戏"，三个槽位显示各自的时间戳和章节信息，**WHEN** 玩家点击一个已有存档的槽位，**THEN** LoadAsync 执行 → 游戏从该存档恢复。

- **GIVEN** 设置面板打开，**WHEN** 玩家拖动"音乐音量"滑块到 0.5，**THEN** AudioManager.SetVolume("music", 0.5) 实时生效。值保存到 PlayerPrefs。

- **GIVEN** 设置面板打开，**WHEN** 玩家在语言下拉中选择"English"，**THEN** LocaleSettings.SelectedLocale 切换 → 所有可见面板的文本更新为英语。

- **GIVEN** 暂停菜单打开，**WHEN** 玩家点击"返回标题画面"→ 确认对话框 → 确认，**THEN** auto_save 保存 → SceneManager 加载 MainMenu 场景 → 标题画面显示。

- **GIVEN** 保存/加载面板中所有槽位为空，**WHEN** 面板在 Load 模式下打开，**THEN** 3 个槽位显示"— 空 —"且不可交互。"返回"按钮可用。

## Open Questions

- **标题画面背景动画**: MVP 使用静态插图还是 L1 微动画？静态插图是更低成本的默认选择，L1 微动画（尘埃漂浮）可留给 Vertical Slice。建议 MVP 静态——标题画面停留时间短（<5 秒）——但使用高质量插图。(Owner: art-director)

- **"继续"按钮的可见性逻辑**: 当前：auto_save 存在 → 可见。是否需要额外检查——如 auto_save 的版本是否兼容？如果版本不兼容，"继续"按钮是否应隐藏？建议 MVP 简单处理：显示"继续"但点击后若版本迁移失败再报错——减少标题画面的加载时间。(Owner: ui-programmer)

- **设置面板是否需要"重置为默认"按钮**: 当前设计中无——用户可手动将每个滑块调回默认。如果加入 → 一个"重置为默认"按钮一键恢复所有音量和语言为默认值。建议 MVP 不加——设置项少（5 个），手动调整负担小。(Owner: ui-programmer)
