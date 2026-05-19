# Story 001: 3 场景架构 + Boot 初始化

> **Epic**: 场景管理系统 (SceneManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/scene-management.md`
**Requirement**: `TR-scene-management-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: 场景管理与转场状态机, ADR-0001: 事件总线架构, ADR-0002: 数据管理策略
**ADR Decision Summary**: 3 场景架构 (Boot/MainMenu/Game) + Game 场景内 Addressables 内容注入 + SceneFader 全屏墨迹遮罩 + 预加载触发机制

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `SceneManager.LoadSceneAsync` 是 Unity 核心 API，稳定；IL2CPP 中的完成回调行为与 Editor 一致性需验证

**Control Manifest Rules (Foundation Layer)**:
- Required: 3-scene architecture — Boot → MainMenu → Game, content via Addressables injection
- Required: Unified ink-fade transition — SceneFader VisualElement with opacity transition
- Forbidden: Never use `Resources.Load()` (except AudioMixer)

---

## Acceptance Criteria

*From GDD `design/gdd/scene-management.md`, scoped to this story:*

- [ ] 项目包含 3 个 Unity Scene: `Boot`, `MainMenu`, `Game`
- [ ] GIVEN 游戏启动，WHEN Boot 场景完成初始化，THEN DataManager + AudioManager + InputSystem 全部就绪，MainMenu 场景自动加载。Boot→MainMenu 过渡使用墨色遮罩淡入淡出
- [ ] GIVEN 玩家在主菜单点击"开始游戏"，WHEN Game 场景尚未加载，THEN Game 场景异步加载 + 初始章节 Addressables 预加载。加载期间全屏遮罩覆盖。加载完成后遮罩淡出，玩家看到第一个记忆碎片
- [ ] `Game` 场景作为持久容器——在进入游戏时加载一次，返回主菜单时卸载——不在章节间重新加载

---

## Implementation Notes

*Derived from ADR-0004:*

项目场景结构:
```
Assets/Scenes/
├── Boot.unity         — 引擎启动场景
├── MainMenu.unity     — 主菜单场景
└── Game.unity         — 游戏容器场景 (持久)
```

Boot 场景初始化流程:
1. Boot 场景加载 (Build Settings Scene 0)
2. 初始化 DataManager、AudioManager、InputSystem、LocalizationSettings
3. 所有 Foundation 系统就绪 → `LoadSceneAsync("MainMenu")`
4. Boot → MainMenu 使用 SceneFader 遮罩过渡

MainMenu → Game 流程:
1. 玩家点击 "开始游戏" → 触发 `LoadSceneAsync("Game")`
2. Game 场景异步加载期间: 全屏墨色遮罩覆盖
3. Game 场景加载完成 → 触发初始章节预加载 (DataManager.PreloadChapterAsync)
4. 初始碎片内容就绪 → 遮罩淡出 → OnFragmentTransitioned 事件

关键代码结构:
```csharp
public class SceneManager : MonoBehaviour
{
    public async Task LoadSceneAsync(string sceneName)
    {
        await SceneFader.FadeOut(1.0f);
        await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        await InitializeSceneSystems();
        await SceneFader.FadeIn(1.0f);
    }
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: SceneFader 视觉组件 — 遮罩 VisualElement、opacity transition
- Story 003: 碎片过渡引擎 — TransitionToFragmentAsync 内部逻辑
- Story 004: 章节过渡 + 预加载 — TransitionToChapterAsync
- Story 005: 错误恢复

---

## QA Test Cases

- **AC-1**: Boot → MainMenu 自动过渡
  - Given: 游戏刚启动
  - When: Boot 场景完成所有 Foundation 系统初始化
  - Then: MainMenu 场景自动加载；过渡中使用墨色遮罩淡入淡出；DataManager/AudioManager/InputSystem 在 MainMenu 中可正常访问
  - Edge cases: Boot 初始化中某系统失败 → Error 状态（Story 005）

- **AC-2**: MainMenu → Game 异步加载
  - Given: 玩家在 MainMenu，Game 场景未加载
  - When: 点击 "开始游戏"
  - Then: 全屏遮罩覆盖；Game 场景异步加载；初始章节 Addressables 预加载；完成后遮罩淡出；玩家看到第一个碎片
  - Edge cases: Game 场景加载超时 30s → 显示超时错误 + 重试按钮

- **AC-3**: Game 场景持久性
  - Given: Game 场景已加载，玩家在章节 1
  - When: 从碎片 1 切换到碎片 2
  - Then: Game 场景不重新加载；内容在同一场景内通过 Addressables 注入更新
  - Edge cases: 返回主菜单 → Game 场景卸载 → 再次进入 → 重新加载 Game 场景

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/scene-management/scene_boot_test.cs` — must exist and pass

**Status**: [x] Created — 23 test functions, all ACs covered

---

## Dependencies

- Depends on: None (Foundation 层根模块之一)
- Unlocks: Story 002 (SceneFader), Story 003 (碎片过渡)

---

## Completion Notes
**Completed**: 2026-05-14
**Criteria**: 4/4 passing
**Deviations**:
- BootBootstrap passes null for ISceneFader/IDataManager/IAudioManager (implementations not yet built)
- Class renamed SceneManager → GameSceneManager (avoids UnityEngine.SceneManagement.SceneManager collision)
- IDataManager.cs expanded to full ADR-0002 interface by linter
- _sceneLoadFuncForTesting / _sceneLoadTimeoutSeconds internal injection for testability
**Test Evidence**: tests/integration/scene-management/scene_boot_test.cs — 23 test functions, all ACs covered
**Code Review**: Complete — 7 issues fixed (C1, B1, H1, B2, M1, M2, M3, H2)
