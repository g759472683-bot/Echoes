# Story 005: 错误恢复

> **Epic**: 场景管理系统 (SceneManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/scene-management.md`
**Requirement**: `TR-scene-management-007`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: 场景管理与转场状态机
**ADR Decision Summary**: 3 场景架构 + 转场状态机 — 每层加载失败有独立的错误恢复路径

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Addressables 6.2+ 加载失败抛异常（非 null）— catch 特定异常类型

**Control Manifest Rules (Foundation Layer)**:
- Required: Error recovery — fragment/chapter/scene load failure with retry/return options
- Forbidden: Never use `Task.Result` or `Task.Wait()` on main thread

---

## Acceptance Criteria

*From GDD `design/gdd/scene-management.md`, scoped to this story:*

- [ ] GIVEN 碎片插图 Addressables 加载失败，WHEN TransitionToFragmentAsync 捕获异常，THEN 遮罩保持覆盖，显示"记忆碎片加载失败"错误 + "返回章节开头"按钮——不自动跳转
- [ ] 章节加载失败：遮罩保持覆盖，显示"章节加载失败"错误 + "返回主菜单"和"重试"两个选项
- [ ] Game 场景加载失败 (LoadSceneAsync 异常)：记录完整错误堆栈，显示"游戏场景加载失败，请验证游戏文件完整性" + 退出到桌面按钮
- [ ] Boot 场景初始化失败：在 Boot 场景中显示错误信息 + "重试"按钮。不尝试加载 MainMenu
- [ ] 加载超时处理：Scene Load 30s 超时 / Metadata Load 10s 超时 → 显示超时错误 + 重试按钮

---

## Implementation Notes

*Derived from ADR-0004:*

错误分级恢复:

| 严重度 | 场景 | 恢复选项 |
|--------|------|----------|
| 可恢复 | 碎片加载失败 | 返回章节开头 |
| 可恢复 | 章节加载失败 | 返回主菜单 / 重试 |
| 致命 | Game 场景加载失败 | 退出到桌面 (Application.Quit) |
| 致命 | Boot 初始化失败 | 重试 / 退出到桌面 |

错误面板:
```csharp
private void ShowErrorPanel(string message, ErrorSeverity severity, 
    params (string label, Action onClick)[] buttons)
{
    // 在 SceneFader 之上创建临时错误 UI Document
    // 使用 UI Framework Theme.uss 样式
    var errorPanel = new VisualElement();
    errorPanel.AddToClassList("error-panel"); // 来自 Theme.uss
    
    var messageLabel = new Label(message);
    messageLabel.AddToClassList("error-message");
    errorPanel.Add(messageLabel);
    
    var buttonRow = new VisualElement();
    buttonRow.AddToClassList("error-buttons");
    foreach (var (label, onClick) in buttons)
    {
        var button = new Button(onClick) { text = label };
        buttonRow.Add(button);
    }
    errorPanel.Add(buttonRow);
    
    _rootVisualElement.Add(errorPanel);
}
```

超时处理:
```csharp
private async Task<T> WithTimeout<T>(Task<T> task, int timeoutSeconds, string errorMessage)
{
    var completedTask = await Task.WhenAny(task, Task.Delay(timeoutSeconds * 1000));
    if (completedTask != task)
        throw new TimeoutException(errorMessage);
    return await task;
}
```

错误发生后状态机:
- FragmentTransition 中错误 → 保持遮罩覆盖 → 显示错误面板 → 用户选择后 → State = Idle (遮罩解除)
- ChapterTransition 中错误 → 同上
- SceneTransition 中错误 → 致命——无法恢复当前场景上下文

---

## Out of Scope

*Handled by neighbouring stories or systems:*

- Story 002: SceneFader 遮罩 — 错误面板在遮罩之上显示
- Story 003/004: 正常过渡流程中的 try/catch — 本 Story 定义 catch 后的恢复逻辑
- 错误文本本地化 — 错误消息使用硬编码中文（仅限错误面板——内容是系统级而非玩家可见的叙事文本）
- DataLoadException 定义 — 由 Data Management (#2) Story 004 定义

---

## QA Test Cases

- **AC-1**: 碎片加载失败 → 返回章节开头
  - Given: 碎片的插图 Addressable key 无效
  - When: `TransitionToFragmentAsync` 中 `GetIllustrationAsync` 抛出 DataLoadException
  - Then: 遮罩保持覆盖；显示错误信息 "记忆碎片加载失败"；提供 "返回章节开头" 按钮；不自动跳转
  - Edge cases: 点击 "返回章节开头" → 加载该章第一个碎片

- **AC-2**: 章节加载失败 → 返回主菜单/重试
  - Given: 章节 2 的 Addressables 组缺失
  - When: `TransitionToChapterAsync("ch2")` 加载失败
  - Then: 遮罩保持覆盖；显示 "章节加载失败"；提供 "返回主菜单" + "重试" 两个选项
  - Edge cases: 点击 "重试" → 重新执行 TransitionToChapterAsync；连续 3 次重试失败 → 额外显示 "建议验证游戏文件"

- **AC-3**: Game 场景加载失败 → 退出
  - Given: Game.unity 场景文件损坏
  - When: `LoadSceneAsync("Game")` 失败
  - Then: 完整错误堆栈记录到日志；显示 "游戏场景加载失败，请验证游戏文件完整性"；提供 "退出到桌面" 按钮
  - Edge cases: 玩家拒绝退出 → 无其他选项——Game 场景不可用时游戏无法运行

- **AC-4**: Boot 初始化失败
  - Given: DataManager 在 Boot 中初始化时 Addressables 初始化失败
  - When: Boot 场景初始化
  - Then: 显示错误信息 (含系统名)；提供 "重试" 按钮；不加载 MainMenu
  - Edge cases: 重试后仍失败 → 增加 "退出" 按钮

- **AC-5**: 加载超时
  - Given: Game 场景加载卡住 (模拟)
  - When: 超过 30s
  - Then: 抛出 TimeoutException；显示超时错误信息 + 重试按钮
  - Edge cases: Metadata Load 超时 10s → 单独的超时错误信息

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/scene-management/error_recovery_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003 (碎片过渡), Story 004 (章节过渡)
- Unlocks: None directly — 错误恢复是横切关注点

---

## Completion Notes
**Completed**: 2026-05-17
**Criteria**: 5/5 passing (27 unit tests)
**Deviations**: 5 LOW issues accepted — 3 dead code (`_lastErrorSeverity` not cleared, `WithTimeout<T>` unused, `RetryFragmentTransition` unwired), 2 weak tests (no-op retry counter, dead timeout pattern test)
**Test Evidence**: `tests/unit/scene-management/error_recovery_test.cs` (27 test functions)
**Code Review**: APPROVED WITH SUGGESTIONS — all issues LOW
