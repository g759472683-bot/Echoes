# 回响 (Echoes) — Master Architecture

## Document Status
- **Version**: 1
- **Last Updated**: 2026-05-12
- **Engine**: Unity 6.3 LTS
- **GDDs Covered**: #1–19 (all MVP systems)
- **ADRs Referenced**: ADR-0001~0011 (全部 P0/P1/P2, 共 11 个)
- **Technical Director Sign-Off**: 2026-05-12 — CONCERNS (架构质量合格; P0 ADR 缺失需在 Pre-Production 前解决)
- **Lead Programmer Feasibility**: Skipped (Lean mode)

## Engine Knowledge Gap Summary

| Risk | Domain | Key Change | Systems Affected |
|------|--------|-----------|-----------------|
| HIGH | Input System | Legacy Input deprecated; Input System package mandatory | #1 — correctly targets new API |
| HIGH | UI Toolkit | Runtime UI production-ready in Unity 6 | #5, #17, #19 — correctly use UI Toolkit |
| HIGH | Rendering | RenderGraph API; Shader Graph for URP 2D | #9 — Shader Graph + MaterialPropertyBlock |
| MEDIUM | Addressables | 6.2+ throws exceptions on load failure | #2 — already wraps try/catch |
| MEDIUM | Scripting | C# 9, `[SerializeReference]` polymorphic | #8 — ContentChange/Condition polymorphic types |
| MEDIUM | Audio | Audio Mixer improvements | #3 — standard usage, low exposure |
| LOW | Physics 2D | Solver iteration defaults changed | #11 — simple OverlapPoint, unaffected |

**Verdict**: All HIGH/MEDIUM risk domains correctly addressed in GDDs. No blockers.

---

## System Layer Map

```
┌─────────────────────────────────────────────────────────────┐
│  PRESENTATION LAYER                                         │
│  #9 微动画  #17 HUD  #18 交互反馈  #19 主菜单  #3 音频      │
├─────────────────────────────────────────────────────────────┤
│  FEATURE LAYER                                              │
│  #10 情感标签  #11 画卷交互  #12 变化追踪                    │
│  #13 网状关联引擎  #14 多结局  #15 章节管理  #16 跨章状态    │
├─────────────────────────────────────────────────────────────┤
│  CORE LAYER                                                 │
│  #1 输入系统  #8 记忆碎片数据模型                             │
│  #5 UI框架（桥接：核心↔表现）                                 │
├─────────────────────────────────────────────────────────────┤
│  FOUNDATION LAYER                                           │
│  #2 数据管理  #6 场景管理  #7 存档  #4 本地化                 │
├─────────────────────────────────────────────────────────────┤
│  PLATFORM LAYER                                             │
│  Unity 6.3 LTS + URP 2D + Addressables + Input System        │
└─────────────────────────────────────────────────────────────┘
```

### Layer Definitions

| Layer | Responsibility | What it does NOT do |
|-------|---------------|---------------------|
| **Foundation** | Data loading, scene transitions, save/load, string localization. All systems depend on these. | No gameplay logic. No player-visible output. |
| **Core** | Input routing, data model definitions, UI panel contract. Defines *what* data and input look like. | No gameplay mechanics. No visual rendering. |
| **Feature** | All gameplay mechanics and progression — association engine, change tracking, chapter flow, endings. | No direct rendering. No asset loading. |
| **Presentation** | Player-visible output — animations, HUD, feedback effects, menus, audio playback. | No gameplay state ownership. No data persistence. |

### Key Cross-Layer Notes

1. **UI Framework (#5) straddles Core↔Presentation** — defines the panel contract (Core) but renders visuals (Presentation)
2. **Micro-Animation (#9) is pure Presentation** — only visual output, no gameplay logic
3. **Change Tracking (#12) is Feature** — owns the overlay state, the game's core mutable data
4. **Audio System (#3) is listed in both Foundation (loading/mixing) and Presentation (playback)** — the AudioManager's data-loading side is Foundation; its PlaySFX/PlayMusic surface is Presentation

---

## Module Ownership

### Foundation Layer

#### #2 DataManager (Data Management)
| Aspect | Detail |
|--------|--------|
| **Owns** | ChapterDefinition[] SO refs, MemoryFragment[] SO refs (metadata cache); three-state async readiness model (Cached/Loading/NotRequested); concurrent request dedup (shared Task refs); Addressables group lifecycle (load/unload/preload) |
| **Exposes** | `GetChapterAsync(key) → Task<ChapterDefinition>`, `GetFragmentAsync(chapterKey, fragmentId) → Task<MemoryFragment>`, `GetIllustrationAsync(assetKey) → Task<Sprite>`, `PreloadChapterAsync(key) → Task`, `IsReady(assetKey) → bool`, `GetFragmentsByChapter(chapterKey) → List<MemoryFragment>` |
| **Consumes** | None (Foundation root) |
| **Engine APIs** | `Addressables.LoadAssetAsync<T>()`, `Addressables.Release()`, `Addressables.DownloadDependenciesAsync()`, `ScriptableObject`, `System.Text.Json` |
| **Risk** | ⚠️ MEDIUM — Addressables 6.2+ throws exceptions (verified: GDD wraps try/catch) |

#### #4 LocalizationManager (Localization)
| Aspect | Detail |
|--------|--------|
| **Owns** | LocalizationSettings asset config; StringTable load states (UI_Shared persistent + Narrative_Ch per-chapter); current SelectedLocale; fallback chain (en → zh-Hans) |
| **Exposes** | `GetLocalizedString(tableRef, entryRef) → string`, `SetLocale(localeCode)`, `GetCurrentLocale() → Locale`, `OnLocaleChanged` event |
| **Consumes** | Unity Localization Package built-in Addressables loading (no longer depends on #2 LoadStringTable) |
| **Engine APIs** | `com.unity.localization`: `LocalizationSettings`, `Locale`, `StringTable`, `LocalizedString`, `TableReference`, `TableEntryReference` |
| **Risk** | ⚠️ MEDIUM — package version must be verified for 6.3 LTS |

#### #6 SceneManager (Scene Management)
| Aspect | Detail |
|--------|--------|
| **Owns** | 3 Unity Scenes (Boot/MainMenu/Game) load/unload; SceneFader full-screen ink-mask VisualElement; transition state machine (FragmentTransition/ChapterTransition/SceneTransition); preload trigger timing |
| **Exposes** | `LoadSceneAsync(sceneName) → Task`, `TransitionToFragmentAsync(chapterKey, fragmentId) → Task`, `TransitionToChapterAsync(chapterKey) → Task`, `PreloadChapterAsync(chapterKey) → Task`, `OnFragmentTransitionStarted(chapterKey, fragmentId)`, `OnFragmentTransitioned(chapterKey, fragmentId)` events |
| **Consumes** | #2 DataManager (GetFragmentAsync/GetIllustrationAsync/PreloadChapterAsync), #3 AudioManager (PlayMusic/StopMusic/PreloadChapterAudioAsync), #1 InputSystem (Action Map → Inactive) |
| **Engine APIs** | `UnityEngine.SceneManagement.SceneManager.LoadSceneAsync()`, `UIDocument`/`VisualElement` (SceneFader), UI Toolkit `transition` property |
| **Risk** | ✅ LOW — standard SceneManager API |

#### #7 SaveManager (Save/Load)
| Aspect | Detail |
|--------|--------|
| **Owns** | 3-slot .sav files (save_01/save_02/auto_save); full SaveData struct (version, timestamp, progress, ChangeOverlay, CrossChapterFlags, volume settings); SHA-256 checksum; atomic write (.tmp → .sav); version migration chain |
| **Exposes** | `SaveAsync(slotId) → Task`, `LoadAsync(slotId) → Task<SaveData>`, `HasAnySave() → bool`, `GetSlotMetaData(slotId) → SlotMetaData`, `CollectSaveData() → SaveData`, `RestoreSaveData(SaveData)` |
| **Consumes** | #15 ChapterManager (progress state), #12 ChangeTracker (_overlay + _flags + _visitedFragments), #16 CrossChapterTracker (CrossChapterFlags), #4 LocalizationManager (LocaleCode), #3 AudioManager (volumes), #14 MultiEndingSystem (UnlockedEndingIds) |
| **Engine APIs** | `Application.persistentDataPath`, `System.Text.Json`, `System.Security.Cryptography.SHA256`, `File.ReadAllTextAsync/WriteAllTextAsync`, `File.Move` (atomic replace) |
| **Risk** | ✅ LOW — pure C# I/O |

### Core Layer

#### #1 InputManager (Input System)
| Aspect | Detail |
|--------|--------|
| **Owns** | PlayerControls generated C# class (Input Actions asset); two Action Maps (Gameplay + UI) with exclusive toggle; HoverDetector component (single Physics2D.OverlapPoint/frame); device hot-plug detection; key rebinding (PlayerPrefs persistence) |
| **Exposes** | Gameplay Map: Point (Vector2), Click (Button), Scroll (Vector2); UI Map: Navigate, Confirm, Cancel, TabNext, TabPrevious; `SwitchToGameplayMode()` / `SwitchToUIMode()` / `SwitchToInactive()`; `OnGamepadConnectionChanged` event; HoverDetector events: OnHoverEnter/OnHoverExit |
| **Consumes** | None (Foundation root) |
| **Engine APIs** | `com.unity.inputsystem`: `InputActionAsset`, `PlayerInput`, `Keyboard.current`, `Mouse.current`, `Gamepad.current`, `PerformInteractiveRebinding()`, `SaveBindingOverridesAsJson()` |
| **Risk** | ⚠️ HIGH — entire API surface is post-cutoff |

#### #8 MemoryFragment (Data Model)
| Aspect | Detail |
|--------|--------|
| **Owns** | MemoryFragment ScriptableObject full schema (8 categories): Core Identity (FragmentId/ChapterId/SequenceIndex), Visual Layers (BaseIllustration + VisualLayers[]), Interactive Objects (InteractiveObjects[]), Emotional Tags (EmotionalTags[]), Choice Branches (ChoiceGroups[]), Content Changes (ContentChange[] 6 types), Explicit Associations (ExplicitAssociations[]), Ending Triggers (EndingTriggers[]); ConditionGroup system (6 leaf types + All/Any/Not combinators, max depth 3) |
| **Exposes** | Pure data definition — no runtime methods. Fields queried via DataManager, consumed by other systems. ContentChange and ConditionGroup are shared type definitions. |
| **Consumes** | #4 LocalizationManager (TableReference type references) |
| **Engine APIs** | `ScriptableObject`, `AssetReferenceSprite`, `TableReference`, `[SerializeReference]` (polymorphic ContentChange/Condition), custom Editor Inspector |
| **Risk** | ⚠️ MEDIUM — `[SerializeReference]` in IL2CPP builds needs verification |

#### #5 UIPanelStack (UI Framework)
| Aspect | Detail |
|--------|--------|
| **Owns** | UIPanelStack LIFO stack (max depth 10); panel-exclusive input gating (stack non-empty → UI Action Map, stack empty → Gameplay Action Map); Theme.uss global style variables (`--color-*`/`--font-*`/`--spacing-*`/`--transition-*`); two predefined CSS transition classes (.fade-in/.fade-out); UI Toolkit FocusController keyboard navigation |
| **Exposes** | `PushPanel(panelId)`, `PopPanel()`, `ReplaceTop(panelId)`; panel transition animations (auto-trigger .fade-in/.fade-out) |
| **Consumes** | #1 InputSystem (Navigate/Confirm/Cancel/TabNext/TabPrevious + Action Map switching); #4 LocalizationManager (LocalizedString binding) |
| **Engine APIs** | `com.unity.ui` (UI Toolkit): `UIDocument`, `VisualElement`, USS `var()` system, `FocusController`, `Focusable` |
| **Risk** | ⚠️ HIGH — runtime UI Toolkit is Unity 6 new feature |

### Feature Layer

#### #10 EmotionalTagSystem (Emotional Tags)
| Aspect | Detail |
|--------|--------|
| **Owns** | EmotionalTagCatalog ScriptableObject (15-20 tags × 8 categories); tag hierarchy (max 2 levels, ParentTagId); runtime weight query (base + overlay merge); vocabulary uniqueness |
| **Exposes** | `GetTagsForFragment(fragmentId) → List<EmotionalTag>`, `GetPrimaryTag(fragmentId) → EmotionalTag?`, `QueryFragmentsByTag(tagId, minWeight) → List<string>`, `GetTagCategory(tagId) → Category`, `GetRelatedTags(tagId) → List<string>` |
| **Consumes** | #8 MemoryFragment (EmotionalTags[] fields); #12 ChangeTracker (ModifyTagWeight overlay merge) |
| **Engine APIs** | `ScriptableObject`, custom Editor tag browser |
| **Risk** | ✅ LOW — ScriptableObject + pure C# logic |

#### #11 InteractionManager (Scroll Interaction)
| Aspect | Detail |
|--------|--------|
| **Owns** | InteractionManager MonoBehaviour (Game scene persistent); per-frame Physics2D.OverlapPoint detection; hover/exit/click/drag four interaction events; 10 public static C# events; four interaction type handlers (Touch/Drag/Hover/Examine); interaction result dispatch (PlayAnimation/ShowText/PresentChoice/TransitionToFragment/RevealObject); drag threshold (5px trigger / 30px complete); object DefaultState (Active/Hidden/Disabled) |
| **Exposes** | 10 static events: OnHoverEnter, OnHoverExit, OnInteract, OnDragStart, OnDragComplete, OnDragCancel, OnChoiceSelected, OnChoiceHover, OnRevealObject, OnShowText |
| **Consumes** | #1 InputSystem (Point/Click raw input + HoverDetector events); #6 SceneManager (OnFragmentTransitioned → rebuild colliders); #8 MemoryFragment (InteractiveObjects[]/ChoiceGroups[]/InteractionResult) |
| **Engine APIs** | `Physics2D.OverlapPoint()` (non-alloc), `BoxCollider2D`/`CircleCollider2D`, `MonoBehaviour.Update()` |
| **Risk** | ✅ LOW — standard 2D physics |

#### #12 ChangeTracker (Memory Change Tracking)
| Aspect | Detail |
|--------|--------|
| **Owns** | `_overlay` Dictionary (key=(fragmentId, choiceId) → ContentOverrides); `_flags` Dictionary (string → bool); `_visitedFragments` HashSet; `_completedChapters` HashSet; `_changeLog` List (debug/gallery only, not serialized); OverlayVersion counter; 6 ContentChange overlay algorithms; ConditionGroup runtime evaluation engine (shared service) |
| **Exposes** | `ApplyChanges(ContentChange[]) → void`, `GetCurrentState(fragmentId) → ResolvedFragmentState`, `SetFlag(flagId, value)`, `SetFlagRaw(flagId, value)` (for cross-chapter tracker), `GetFlag(flagId) → bool`, `GetAllFlags() → Dictionary`, `EvaluateCondition(ConditionGroup) → bool`, `OnOverlayChanged(targetFragmentId)` event |
| **Consumes** | #8 MemoryFragment (base SO read); #2 DataManager (GetFragmentAsync); #6 SceneManager (OnFragmentTransitioned → update _visitedFragments); #15 ChapterManager (chapter complete → update _completedChapters) |
| **Engine APIs** | `System.Collections.Generic.Dictionary/HashSet`, `[Serializable]` struct |
| **Risk** | ✅ LOW — pure C# data structures |

#### #13 WebAssociationEngine (Web Association Engine)
| Aspect | Detail |
|--------|--------|
| **Owns** | Pure C# class (no Unity dependency, fully unit-testable); four-factor scoring: (A=TagSimilarity×0.6 + B=ExplicitWeight×0.4) × C=RhythmPenalty × D=DiscoveryBoost; TagSimilarityMatrix ScriptableObject (N×N precomputed); candidate pool construction (same-chapter, unlocked, conditions met, exclude self) |
| **Exposes** | `ComputeAssociations(currentFragmentId, chapterKey, recentHistory, visitedFragmentIds) → List<AssociationCandidate>` (pure function, stateless) |
| **Consumes** | #10 EmotionalTagSystem (GetTagsForFragment/GetPrimaryTag/QueryFragmentsByTag); #12 ChangeTracker (GetCurrentState → condition eval); #8 MemoryFragment (ExplicitAssociations[] fields) |
| **Engine APIs** | None (pure C# math) — only `ScriptableObject` for TagSimilarityMatrix |
| **Risk** | ✅ LOW — no Unity API dependency |

#### #14 MultiEndingSystem (Multi-Ending)
| Aspect | Detail |
|--------|--------|
| **Owns** | UnlockedEndingIds HashSet (permanent union semantics); ending judgment algorithm: collect triggers → IsEssential gate → accumulate ContributionWeight → EmotionalAffinity path bonus → threshold check → tie-breaking; ResolvedEnding output struct |
| **Exposes** | `ResolveEnding(chapterId) → ResolvedEnding`, `OnChapterStart(chapterId)`, `GetUnlockedEndingIds() → HashSet<string>` |
| **Consumes** | #15 ChapterManager (ChapterDefinition.Endings[] defs); #8 MemoryFragment (EndingTriggers[]); #12 ChangeTracker (ConditionGroup eval + _flags + _visitedFragments + _completedChapters) |
| **Engine APIs** | Pure C# logic — `System.Collections.Generic.HashSet` |
| **Risk** | ✅ LOW |

#### #15 ChapterManager (Chapter Management)
| Aspect | Detail |
|--------|--------|
| **Owns** | ChapterState state machine (IDLE/IN_CHAPTER/TRANSITIONING); _chapterVisitedFragments/_sessionVisitedFragments/_recentHistory (K=4); _completedChapters/_unlockedChapters; chapter completion detection (dual condition: all-visited OR Ratio+threshold); preload trigger (remaining ≤3 fragments, once per chapter); chapter replay state split (reset visit records, keep overlay/flags) |
| **Exposes** | `StartNewGame()`, `LoadAndRestore(SaveData)`, `ReplayChapter(chapterKey)`, `TransitionToFragment(targetFragmentId)`, `ReturnToMainMenu()`; properties: CurrentState/CurrentChapterKey/CurrentFragmentId; queries: GetCompletedChapters()/GetUnlockedChapters()/IsChapterCompleted(key)/IsChapterUnlocked(key)/GetVisitedFragmentCount(key); events: OnChapterStarted/OnChapterCompleted/OnFragmentChanged/OnAllChaptersCompleted/OnChapterReplayStarted |
| **Consumes** | #6 SceneManager (TransitionToFragmentAsync/TransitionToChapterAsync/PreloadChapterAsync/LoadSceneAsync); #13 WebAssociationEngine (ComputeAssociations); #14 MultiEndingSystem (ResolveEnding/OnChapterStart); #2 DataManager (GetFragmentsByChapter); #7 SaveManager (CollectSaveData/RestoreSaveData) |
| **Engine APIs** | `MonoBehaviour`, `async Task`, C# `event Action<string>` |
| **Risk** | ✅ LOW |

#### #16 CrossChapterTracker (Cross-Chapter State)
| Aspect | Detail |
|--------|--------|
| **Owns** | CrossChapterFlagRegistry ScriptableObject (FlagId/SetInChapter/SetInFragmentId/SetByChoiceId/IsImmutable/DefaultValue/ConsumedBy); new-game flag initialization; IsImmutable protection (once true, cannot revert false); chapter replay immutable flag protection |
| **Exposes** | `InitializeAllFlags()`, `GetPersistableFlags() → Dictionary`, `RestoreFlags(Dictionary)` |
| **Consumes** | #12 ChangeTracker (SetFlagRaw/GetAllFlags/GetFlag — direct _flags read/write); #15 ChapterManager (OnChapterReplayStarted event → activate IsImmutable protection) |
| **Engine APIs** | `ScriptableObject` (FlagRegistry), `System.Collections.Generic.Dictionary` |
| **Risk** | ✅ LOW |

### Presentation Layer

#### #3 AudioManager (Audio System)
| Aspect | Detail |
|--------|--------|
| **Owns** | 4-level Audio Mixer routing (Master→SFX/Music/Ambience); dual-source music crossfade (Music_A/Music_B + two Snapshots); SFX 10-source object pool (priority stealing); volume control (4 Exposed Parameters → PlayerPrefs); singleton AudioListener |
| **Exposes** | `PlaySFX(clipKey, worldPosition?)`, `PlayMusic(clipKey, fadeTime)`, `StopMusic(fadeTime)`, `PlayAmbience(clipKey)`, `SetVolume(category, linearValue)`, `GetVolume(category) → float`, `PreloadChapterAudioAsync(chapterKey) → Task`, `UnloadChapterAudio(chapterKey)`, `OnAudioError` event |
| **Consumes** | #2 DataManager (three-state load model reused — but AudioManager tracks internally); #6 SceneManager (chapter transition triggers music switch); #18 InteractionFeedback (PlaySFX calls); #19 MainMenu (SetVolume/GetVolume) |
| **Engine APIs** | `AudioMixer`, `AudioMixerGroup`, `AudioMixerSnapshot`, `AudioSource` (PlayOneShot/Play/Stop/LoadAudioData), `AudioListener`, `AudioClip`, `Addressables.LoadAssetAsync<AudioClip>()` |
| **Risk** | ✅ LOW — standard Audio APIs |

#### #9 MicroAnimationManager (Micro-Animation)
| Aspect | Detail |
|--------|--------|
| **Owns** | MicroAnimationCatalog ScriptableObject (AmbientAnimDef[]/TriggeredAnimDef[]/FeedbackAnimDef[]); custom MicroTween value-type struct (~250 lines, zero GC); three animation categories (Ambient loop/Triggered one-shot/Feedback immediate); three-level L1/L2/L3 vermilion ink dot glow; per-frame MaterialPropertyBlock updates; performance degradation strategy (High→Medium→Low→Minimal); EmotionPreset parameter modulation |
| **Exposes** | `PlayTriggered(animationId)`, `PlayFeedback(animationId)`, L2/L3 glow control (for #18); OnFragmentTransitioned handler (stop old fragment animations + start new fragment AmbientAnimInstances) |
| **Consumes** | #6 SceneManager (OnFragmentTransitioned → animation switch); #8 MemoryFragment (AmbientAnimInstances[]); #10 EmotionalTagSystem (GetPrimaryTag → EmotionPreset params); #18 InteractionFeedback (triggered animation requests) |
| **Engine APIs** | `Shader Graph` (vertex displacement/UV scroll/material property pulse), `MaterialPropertyBlock` (`SetFloat("_FragmentTime", time)`), `SpriteRenderer`, `MonoBehaviour.Update()` |
| **Risk** | ⚠️ HIGH — URP 2D Shader Graph compatibility must be verified |

#### #17 InGameHUD
| Aspect | Detail |
|--------|--------|
| **Owns** | Game scene UIDocument VisualElement tree (#fragment-text-overlay/#choice-panel/#association-paths/#chapter-progress/#interaction-hint); MVVM data sources (AssociationPathsDataSource/ChapterProgressDataSource — implement INotifyBindablePropertyChanged); association path visual grading (Strong/Medium/Faint/Trace 4 ink styles); chapter progress ink dot row |
| **Exposes** | `ShowChoicePanel(ChoiceGroup)`, `ShowFragmentText(TextContent)`, `HideChoicePanel()`, `UpdateChapterProgress(chapterKey, visitedCount, total)`, `UpdateAssociationPaths(AssociationCandidate[])` |
| **Consumes** | #5 UI Framework (UIDocument + Theme.uss + panel stack bottom layer); #11 InteractionManager (PresentChoice → ShowChoicePanel/ShowText → ShowFragmentText); #13 WebAssociationEngine (ComputeAssociations result → UpdateAssociationPaths); #15 ChapterManager (OnFragmentChanged → progress update); #12 ChangeTracker (option click → ApplyChanges); #1 InputSystem (UI Action Map switch/keyboard nav); #4 LocalizationManager (LocalizedString) |
| **Engine APIs** | `com.unity.ui` (UI Toolkit): `VisualElement`, `Label`, `Button`, `INotifyBindablePropertyChanged`, USS variables/transitions; `TextMeshPro` (handwritten Chinese font) |
| **Risk** | ⚠️ MEDIUM — MVVM binding in UI Toolkit is relatively new |

#### #18 InteractionFeedback
| Aspect | Detail |
|--------|--------|
| **Owns** | Event→Feedback mapping table (10 interaction events → visual+audio response); feedback priority (choice confirm > drag complete > interact > hover); debounce (same object 0.3s); transition suppression flag (_feedbackSuppressed: bool) |
| **Exposes** | No public methods (pure event listener — driven by other systems' events, exposes no API) |
| **Consumes** | #11 InteractionManager (10 static events); #6 SceneManager (OnFragmentTransitionStarted → suppress / OnFragmentTransitioned → restore); #9 MicroAnimationManager (PlayTriggered/L2/L3 glow control); #3 AudioManager (PlaySFX) |
| **Engine APIs** | `MonoBehaviour` (OnEnable event subscription only); pure event-driven, no Update() |
| **Risk** | ✅ LOW |

#### #19 MainMenuController
| Aspect | Detail |
|--------|--------|
| **Owns** | MainMenu scene UIDocument with 5 VisualElement trees (#title-screen/#pause-menu/#settings-panel/#save-load-panel/#modal-dialog); SlotMetaData struct; pause state (Time.timeScale = 0); settings panel 4 volume sliders + language dropdown; 5 confirmation dialog scenarios |
| **Exposes** | Button event handlers (new game/continue/load/settings/quit/save/return to title); volume/language changes → AudioManager/LocaleSettings |
| **Consumes** | #5 UI Framework (PushPanel/PopPanel/ReplaceTop); #7 SaveManager (HasAnySave/GetSlotMetaData/SaveAsync/LoadAsync); #15 ChapterManager (StartNewGame/LoadAndRestore); #6 SceneManager (LoadSceneAsync); #3 AudioManager (SetVolume/GetVolume); #4 LocalizationManager (SetLocale/LocalizedString); #16 CrossChapterTracker (InitializeAllFlags — indirect via ChapterManager.StartNewGame) |
| **Engine APIs** | `com.unity.ui` (UI Toolkit): `UIDocument`, `VisualElement`, `Button`, `Label`, `Slider`, `DropdownField`, USS transitions; `Application.Quit()`/`EditorApplication.ExitPlaymode()`; `PlayerPrefs`; `Time.timeScale` |
| **Risk** | ✅ LOW |

---

## Data Flow

### 3.1 Frame Update Path

每帧仅 3 个系统需要 `MonoBehaviour.Update()`，其余全部事件驱动：

```
16.6ms 帧预算
├─ InputManager.Update()           ~0.1ms  读取 InputActionAsset 状态
│  └─ 产出: Vector2 Point, Button Click/Scroll 原始值
│
├─ InteractionManager.Update()     ~0.2ms  Physics2D.OverlapPoint (单点, 非分配)
│  └─ 消费: InputManager.Point
│  └─ 产出: 触发 static event (OnHoverEnter/OnHoverExit/OnInteract/OnDrag*)
│
└─ MicroAnimationManager.Update()  ~1.0ms  MaterialPropertyBlock.SetFloat 逐 SpriteRenderer
   └─ 产出: GPU 参数写入 (无 CPU→GPU 回读)
```

| 数据 | 生产者 | 消费者 | 方式 | 跨线程 |
|------|--------|--------|------|--------|
| 输入状态 (Point/Click/Scroll) | #1 InputManager | #11 InteractionManager, #5 UIPanelStack | 同步属性读取 | 否 |
| 悬停/点击命中结果 | #11 InteractionManager | #18 InteractionFeedback, #17 HUD | static event | 否 |
| 动画参数 (_FragmentTime 等) | #9 MicroAnimationManager | GPU (Shader Graph) | MaterialPropertyBlock | 否 (GPU 端) |

### 3.2 Event/Signal Path

核心场景：**玩家点击画卷碎片上的选项 → 连锁系统响应**

```
玩家 Click 选项 A
  │
  ▼
#11 InteractionManager.OnChoiceSelected(choiceId)
  │
  ├──▶ #12 ChangeTracker.ApplyChanges(ContentChange[])
  │      └─ 更新 _overlay / _flags / _visitedFragments
  │      └─ 触发 #12 OnOverlayChanged(targetFragmentId)
  │           └─▶ #17 InGameHUD 刷新文本/选项显示
  │
  ├──▶ #15 ChapterManager (检查是否为章节结束碎片)
  │      └─ 若完成 → #15 OnChapterCompleted(chapterKey)
  │           ├─▶ #6 SceneManager.TransitionToChapterAsync()  (同步调用)
  │           │    └─ 触发 #6 OnFragmentTransitionStarted
  │           │         ├─▶ #18 InteractionFeedback._feedbackSuppressed = true
  │           │         └─▶ #11 InteractionManager 暂停检测
  │           │    └─ 触发 #6 OnFragmentTransitioned
  │           │         ├─▶ #11 InteractionManager 重建碰撞体
  │           │         ├─▶ #9 MicroAnimationManager 切换环境动画
  │           │         ├─▶ #12 ChangeTracker 更新 _visitedFragments
  │           │         ├─▶ #13 WebAssociationEngine.ComputeAssociations()
  │           │         │    └─▶ #17 InGameHUD.UpdateAssociationPaths()
  │           │         └─▶ #18 InteractionFeedback._feedbackSuppressed = false
  │           └─▶ #14 MultiEndingSystem.ResolveEnding(chapterKey)
  │                └─ 若结局触发 → ResolvedEnding → #17 HUD 展示
  │
  └──▶ #18 InteractionFeedback (事件→反馈映射表)
         ├─▶ #9 MicroAnimationManager.PlayFeedback(animationId)
         └─▶ #3 AudioManager.PlaySFX(clipKey)
```

| 数据 | 生产者 | 消费者 | 方式 | 备注 |
|------|--------|--------|------|------|
| 选项选择 (choiceId) | #11 InteractionManager | #12 ChangeTracker, #15 ChapterManager, #18 InteractionFeedback | static event `OnChoiceSelected` | 1:N 广播 |
| Overlay 变更 | #12 ChangeTracker | #17 InGameHUD | static event `OnOverlayChanged` | 仅通知目标碎片 ID |
| 章节完成 | #15 ChapterManager | #6 SceneManager, #14 MultiEndingSystem | 同步方法调用 + event | 同步调用保证原子性 |
| 碎片转场开始/完成 | #6 SceneManager | #18 IFeedback, #11 IManager, #9 AnimManager, #12 CTracker | static event ×2 | 每组 4-5 个订阅者 |
| 关联路径更新 | #13 WebAssociationEngine | #17 InGameHUD | 同步返回值 (#15 中转) | 纯函数, 无状态 |

**事件订阅关系总表：**

| 事件 | 声明者 | 订阅者 |
|------|--------|--------|
| `OnChoiceSelected` | #11 InteractionManager | #12 ChangeTracker, #15 ChapterManager, #18 InteractionFeedback |
| `OnHoverEnter` / `OnHoverExit` | #11 InteractionManager | #18 InteractionFeedback |
| `OnInteract` | #11 InteractionManager | #18 InteractionFeedback |
| `OnDragStart` / `OnDragComplete` / `OnDragCancel` | #11 InteractionManager | #18 InteractionFeedback |
| `OnRevealObject` / `OnShowText` | #11 InteractionManager | #18 InteractionFeedback, #17 InGameHUD |
| `OnChoiceHover` | #11 InteractionManager | #18 InteractionFeedback |
| `OnOverlayChanged` | #12 ChangeTracker | #17 InGameHUD |
| `OnFragmentTransitionStarted` | #6 SceneManager | #18 InteractionFeedback, #11 InteractionManager |
| `OnFragmentTransitioned` | #6 SceneManager | #11 InteractionManager, #9 MicroAnimationManager, #12 ChangeTracker, #18 InteractionFeedback |
| `OnChapterStarted` | #15 ChapterManager | #14 MultiEndingSystem |
| `OnChapterCompleted` | #15 ChapterManager | #6 SceneManager, #14 MultiEndingSystem |
| `OnFragmentChanged` | #15 ChapterManager | #17 InGameHUD |
| `OnChapterReplayStarted` | #15 ChapterManager | #16 CrossChapterTracker |
| `OnLocaleChanged` | #4 LocalizationManager | #17 InGameHUD, #19 MainMenuController |
| `OnAudioError` | #3 AudioManager | #17 InGameHUD (显示错误提示) |

### 3.3 Save/Load Path

#### 存档 (Save)

```
用户触发 (暂停菜单 → 保存)
  │
  ▼
#19 MainMenuController
  │ 调用 SaveManager.SaveAsync(slotId)
  ▼
#7 SaveManager.CollectSaveData()
  │
  ├─▶ #15 ChapterManager
  │     └─ 产出: ChapterProgress (currentChapterKey, currentFragmentId, chapterVisitedFragments)
  │
  ├─▶ #12 ChangeTracker
  │     └─ 产出: _overlay, _flags, _visitedFragments (完整), _completedChapters
  │
  ├─▶ #16 CrossChapterTracker
  │     └─ 产出: CrossChapterFlags (Dictionary<string, bool>)
  │
  ├─▶ #14 MultiEndingSystem
  │     └─ 产出: UnlockedEndingIds (HashSet<string>)
  │
  ├─▶ #4 LocalizationManager
  │     └─ 产出: SelectedLocale (string)
  │
  └─▶ #3 AudioManager
        └─ 产出: VolumeSettings (MasterVol, SFXVol, MusicVol, AmbienceVol)

  ▼
SaveData 结构体组装完成
  │
  ▼
JSON 序列化 → SHA-256 校验 → 原子写入 (.tmp → .sav)
```

#### 读档 (Load)

```
用户触发 (标题画面 → 继续 / 读档)
  │
  ▼
#7 SaveManager.LoadAsync(slotId)
  │
  ▼
SaveData 反序列化 + SHA-256 校验
  │
  ▼
#15 ChapterManager.LoadAndRestore(SaveData)
  │
  ├─▶ #4 LocalizationManager.SetLocale(SaveData.LocaleCode)
  ├─▶ #3 AudioManager 恢复音量 (遍历 VolumeSettings)
  ├─▶ #12 ChangeTracker.Restore(_overlay, _flags, _visitedFragments, _completedChapters)
  ├─▶ #16 CrossChapterTracker.RestoreFlags(CrossChapterFlags)
  ├─▶ #14 MultiEndingSystem.Restore(UnlockedEndingIds)
  │
  └─▶ #6 SceneManager.TransitionToFragmentAsync(chapterKey, fragmentId)
       └─ (触发完整碎片转场流程 → 见 3.2)
```

| 数据 | 所有权 | 序列化格式 | 备注 |
|------|--------|-----------|------|
| ChapterProgress | #15 ChapterManager | JSON object | currentChapterKey, currentFragmentId |
| ContentOverlay | #12 ChangeTracker | JSON Dictionary<string, ContentOverrides> | 玩家所有选择累积 |
| Flags | #12 ChangeTracker | JSON Dictionary<string, bool> | 含跨章标记 |
| CrossChapterFlags | #16 CrossChapterTracker | JSON Dictionary<string, bool> | IsImmutable 不序列化 |
| UnlockedEndingIds | #14 MultiEndingSystem | JSON string[] | 永久并集 |
| LocaleCode | #4 LocalizationManager | JSON string | "zh-Hans" / "en" |
| VolumeSettings | #3 AudioManager | JSON object | 4 个 linear 值 |
| SlotMetaData | #7 SaveManager | JSON object | 独立文件, 不随 SaveData |

### 3.4 Initialization Order

三个阶段, 必须在游戏可玩之前完成:

```
═══════════════════════════════════════════
    Phase 1: 基础根系统 (并行, 无依赖)
═══════════════════════════════════════════

#2  DataManager          InitAsync()      ← 初始化 Addressables, 预热目录
#4  LocalizationManager  InitAsync()      ← 设置 Locale, 加载持久字符串表
#1  InputManager         Init()           ← 创建 InputActionAsset, 启用默认 Action Map
#7  SaveManager          Init()           ← 扫描存档槽位, 验证 .sav 完整性

    全部完成 → Phase 2

═══════════════════════════════════════════
    Phase 2: 基础服务 (依赖 Phase 1)
═══════════════════════════════════════════

#3  AudioManager         Init()           ← 创建 AudioMixer, 初始化 10-SFX 池
                           (解耦: Init 时不加载 AudioClip, 首次播放时按需加载)
#6  SceneManager         Init()           ← 注册 3 个 Scene 引用, 创建 SceneFader
                           (依赖: #2 DataManager, #3 AudioManager, #1 InputManager)
#5  UIPanelStack         Init()           ← 加载 Theme.uss, 初始化空栈
                           (依赖: #1 InputManager, #4 LocalizationManager)

    全部完成 → Phase 3

═══════════════════════════════════════════
    Phase 3: 游戏系统 (依赖 Phase 2, 场景加载后)
═══════════════════════════════════════════

#8  MemoryFragment       —                ← 纯 SO 类型定义, 无需运行时初始化

[进入 MainMenu 场景]
#19 MainMenuController   Init()           ← 构建 5 个 VisualElement 树

[用户点击"新游戏" / "继续"]
#10 EmotionalTagSystem   Init()           ← 加载 EmotionalTagCatalog SO
#12 ChangeTracker        Init()           ← 初始化空 _overlay, _flags, _visitedFragments
#13 WebAssociationEngine Init()           ← 加载 TagSimilarityMatrix SO
#14 MultiEndingSystem    Init()           ← 初始化 UnlockedEndingIds
#15 ChapterManager       Init()           ← 初始化状态机 (IDLE)
#16 CrossChapterTracker  Init()           ← InitializeAllFlags()

[场景切换到 Game 场景]
#11 InteractionManager   Init()           ← 注册到场景, 等待 OnFragmentTransitioned 构建碰撞体
#9  MicroAnimationManager Init()          ← 加载 MicroAnimationCatalog SO
#17 InGameHUD            Init()           ← 构建 VisualElement 树, 绑定 MVVM 数据源
#18 InteractionFeedback  Init()           ← 订阅 10 个 static events (OnEnable)

═══════════════════════════════════════════
              就绪, 可开始游戏
═══════════════════════════════════════════
```

**关键时序约束：**

| 约束 | 说明 |
|------|------|
| Phase 1 四系统并行 | #1, #2, #4, #7 之间无依赖, 可同时初始化 |
| AudioManager 延迟加载 | Init 时不加载 AudioClip, 避免 Addressables 阻塞 Phase 2 |
| Feature 层 < 50ms | #10-#16 全部是纯 C# 逻辑 + SO 引用, 无 I/O |
| InteractionManager 延迟 | 必须等待首次 `OnFragmentTransitioned` 后才能构建碰撞体 (需要碎片数据) |
| 无循环初始化依赖 | 所有依赖为有向无环图, 验证通过 |

---

## API Boundaries

### 4.1 Foundation Layer

#### IDataManager (#2) — 数据加载边界

```csharp
// 三个异步就绪状态: Cached | Loading | NotRequested
// 并发去重: 对同一 key 的并发 GetAsync 返回同一个 Task 引用

public interface IDataManager
{
    // -- 按需加载 --
    Task<ChapterDefinition> GetChapterAsync(string chapterKey);
    Task<MemoryFragment> GetFragmentAsync(string chapterKey, string fragmentId);
    Task<Sprite> GetIllustrationAsync(string assetKey);

    // -- 预加载 --
    Task PreloadChapterAsync(string chapterKey);
    bool IsReady(string assetKey);

    // -- 查询 --
    List<MemoryFragment> GetFragmentsByChapter(string chapterKey);

    // -- 生命周期 --
    void UnloadChapter(string chapterKey);
}
```

**调用方约定：**
- 必须 `await`，不得访问 `.Result`（防止死锁）
- `GetFragmentAsync` 调用前不需要 `IsReady` 检查 — 内部自动处理三种状态
- `PreloadChapterAsync` 是 fire-and-forget，失败不抛异常（仅日志警告）

**模块保证：**
- 同一 key 的并发请求返回同一 `Task` 引用（去重）
- Addressables 异常包装为 `DataLoadException`，包含 assetKey
- 线程安全：所有公开方法可从任意线程调用

---

#### ISceneManager (#6) — 场景转换边界

```csharp
public interface ISceneManager
{
    // -- 转换 --
    Task LoadSceneAsync(string sceneName);          // "Boot" → "MainMenu" → "Game"
    Task TransitionToFragmentAsync(string chapterKey, string fragmentId);
    Task TransitionToChapterAsync(string chapterKey);

    // -- 预加载 --
    Task PreloadChapterAsync(string chapterKey);

    // -- 事件 (static) --
    // OnFragmentTransitionStarted(string chapterKey, string fragmentId)
    // OnFragmentTransitioned(string chapterKey, string fragmentId)
}
```

**调用方约定：**
- 转场期间（`OnFragmentTransitionStarted` → `OnFragmentTransitioned`），交互检测暂停
- 不得在转场进行中再次调用 `TransitionToFragmentAsync` — 调用方自行防重入
- `LoadSceneAsync("Game")` 前确保 ChapterManager 已完成初始化

**模块保证：**
- 转场原子性：SceneFader 动画覆盖整个加载过程
- `OnFragmentTransitioned` 保证在场景完全就绪后触发
- 失败抛出 `SceneTransitionException`，包含当前状态和目标场景

---

#### ISaveManager (#7) — 存档边界

```csharp
public interface ISaveManager
{
    // -- 持久化 --
    Task SaveAsync(int slotId);                    // 自动调用 CollectSaveData()
    Task<SaveData> LoadAsync(int slotId);

    // -- 元数据 --
    bool HasAnySave();
    SlotMetaData GetSlotMetaData(int slotId);      // 不反序列化完整数据

    // -- 状态收集/恢复 (由 ChapterManager 调用) --
    SaveData CollectSaveData();                    // 聚合 6 个系统状态
    void RestoreSaveData(SaveData data);           // 分发到 6 个系统
}

public struct SaveData
{
    public int Version;
    public DateTime Timestamp;
    public string CurrentChapterKey;
    public string CurrentFragmentId;
    public Dictionary<string, bool> Flags;
    public Dictionary<string, ContentOverrides> Overlay;
    public HashSet<string> VisitedFragments;
    public HashSet<string> CompletedChapters;
    public Dictionary<string, bool> CrossChapterFlags;
    public HashSet<string> UnlockedEndingIds;
    public string LocaleCode;
    public float MasterVolume, SFXVolume, MusicVolume, AmbienceVolume;
}

public struct SlotMetaData
{
    public DateTime Timestamp;
    public string ChapterName;
    public int VisitedCount;
    public TimeSpan PlayTime;
}
```

**调用方约定：**
- `SaveAsync` 调用 `CollectSaveData()`，被收集的 6 个系统需处于一致状态
- 不得在碎片转场中保存（此时 overlay 可能不一致）
- `LoadAsync` 返回后，调用方自行调用 `ChapterManager.LoadAndRestore(SaveData)`

**模块保证：**
- 原子写入：先写 `.tmp`，成功后 `File.Move` 到 `.sav`
- SHA-256 校验：`LoadAsync` 自动验证，失败抛 `SaveCorruptionException`
- 版本迁移：旧 SaveData 自动迁移到当前版本 (migration chain)

---

### 4.2 Core Layer

#### IInputManager (#1) — 输入路由边界

```csharp
// 两个 Action Map (Gameplay / UI)，互斥切换
// Key rebinding 通过 PlayerPrefs 持久化

public interface IInputManager
{
    // -- Action Map 切换 (互斥) --
    void SwitchToGameplayMode();
    void SwitchToUIMode();
    void SwitchToInactive();

    // -- 输入状态 (由 InteractionManager/HUD 每帧读取) --
    Vector2 Point { get; }
    bool ClickPressed { get; }
    bool ClickReleased { get; }
    Vector2 ScrollDelta { get; }

    // -- UI 导航 (由 UIPanelStack 直接绑定 InputActionAsset) --
    // Navigate, Confirm, Cancel, TabNext, TabPrevious

    // -- 设备事件 --
    // OnGamepadConnectionChanged(bool connected)
}
```

**调用方约定：**
- 只在有焦点的系统调用模式切换：栈非空 → UI, 栈空 + Game场景 → Gameplay
- 转场中切换为 Inactive
- `Point` 是屏幕坐标，消费者自行转换到本地坐标

**模块保证：**
- 模式切换是原子操作：先禁用当前 Map，再启用目标 Map
- 每帧最多一次 `OverlapPoint`（HoverDetector 内）
- 键位重绑定自动保存到 `PlayerPrefs`，下次启动自动加载

---

#### IUIPanelStack (#5) — UI 面板边界

```csharp
public interface IUIPanelStack
{
    // -- 面板生命周期 --
    void PushPanel(string panelId);     // LIFO, max depth 10
    void PopPanel();
    void ReplaceTop(string panelId);

    // -- 状态查询 --
    string TopPanelId { get; }
    int StackDepth { get; }

    // -- 输入门控 (自动) --
    // 栈非空 → UI Action Map
    // 栈空   → Gameplay Action Map
}
```

**调用方约定：**
- `PushPanel` 前确保 panelId 已注册
- 面板的 VisualElement 树由各自 Controller 管理，UIPanelStack 只管栈结构
- 不得在 `PopPanel()` 后立即 `PushPanel()`（等待 CSS transition 完成）

**模块保证：**
- 面板转场动画自动触发：`.fade-in` 入栈, `.fade-out` 出栈
- 键盘焦点自动迁移到栈顶面板第一个 `Focusable` 元素
- 栈深超过 10 时抛出异常

---

#### IChangeTracker (#12) — 状态变更边界

```csharp
public interface IChangeTracker
{
    // -- 变更应用 --
    void ApplyChanges(ContentChange[] changes);

    // -- 状态查询 --
    ResolvedFragmentState GetCurrentState(string fragmentId);
    bool EvaluateCondition(ConditionGroup condition);

    // -- Flag 操作 --
    void SetFlag(string flagId, bool value);
    bool GetFlag(string flagId);
    Dictionary<string, bool> GetAllFlags();

    // -- 事件 --
    // OnOverlayChanged(string targetFragmentId)  → #17 HUD 订阅
}
```

**调用方约定：**
- `ApplyChanges` 应在玩家确认选项后调用，不在转场中调用
- `EvaluateCondition` 是纯函数，可任意次调用无副作用
- Flag 命名必须与 GDD 一致

**模块保证：**
- `ApplyChanges` 是同步操作 — overlay 在方法返回时已生效
- `GetCurrentState` 合并 base SO + overlay，返回不可变快照
- `OnOverlayChanged` 事件在 `ApplyChanges` 完成后触发

---

### 4.3 跨层依赖总图

```
┌─────────────────────────────────────────────────────┐
│ Feature 层 (#10-#16)                                  │
│  依赖: IDataManager, IChangeTracker, ISceneManager,   │
│         IInputManager                                 │
├─────────────────────────────────────────────────────┤
│ Presentation 层 (#3, #9, #17, #18, #19)              │
│  依赖: IUIPanelStack, IInputManager, IChangeTracker,  │
│         ISceneManager (events), ILocalizationManager,  │
│         IAudioManager                                 │
├─────────────────────────────────────────────────────┤
│ Foundation 层内部依赖                                  │
│  IDataManager ← 根 (无依赖)                            │
│  ISaveManager ← IChangeTracker + 5 个状态源            │
│  ISceneManager ← IDataManager, IAudioManager           │
│  ILocalizationManager ← 根 (无依赖)                    │
│  IInputManager ← 根 (无依赖)                           │
│  IAudioManager ← 根 (延迟加载 AudioClip)               │
└─────────────────────────────────────────────────────┘
```

### 4.4 引擎类型校验

| 类型 | Unity 6.3 状态 | 风险 |
|------|---------------|------|
| `Task<T>` | C# 标准, 完全支持 | ✅ |
| `ScriptableObject` | Unity 核心, 无签名变更 | ✅ |
| `MonoBehaviour.Update()` | Unity 核心, 无变更 | ✅ |
| `UIDocument` / `VisualElement` | UI Toolkit, Unity 6 新特性 | ⚠️ 需验证 API 稳定性 |
| `InputActionAsset` / `PlayerInput` | Input System 包, 后 cutoff | ⚠️ 已验证模块文档 |
| `Addressables.LoadAssetAsync<T>()` | Addressables, 后 cutoff | ⚠️ 异常行为变更已验证 |
| `Physics2D.OverlapPoint()` | Unity 核心, 无变更 | ✅ |

---

## ADR Audit

**现有 ADR：0 个。** `docs/architecture/` 中无任何 Architecture Decision Record。

### Traceability Coverage

| 指标 | 值 |
|------|-----|
| Technical Requirements 总数 | ~440 (来自 19 个 GDD) |
| 已有 ADR 覆盖 | 0 |
| 覆盖率 | 0% |
| 缺口 | ~440 (100%) |

所有架构决策目前仅存在于本文档中，需通过 `/architecture-decision` 逐一记录为正式 ADR。

---

## Required ADRs

### P0 — 必须在任何编码前创建 (Foundation 层)

| # | ADR 标题 | 覆盖 TR 域 | 依赖 |
|---|----------|-----------|------|
| 1 | **事件总线架构：static event vs. 中央 EventBus** | TR-data-*, TR-scene-*, TR-interaction-*, TR-feedback-* | 无 |
| 2 | **数据管理策略：Addressables + 三态异步就绪模型** | TR-data-001~025 | 无 |
| 3 | **存档序列化格式与版本迁移策略** | TR-save-*, TR-crosschapter-*, TR-ending-* | 无 |
| 4 | **场景管理与转场状态机** | TR-scene-*, TR-chapter-* | ADR-2 |

### P1 — 必须在任何编码前创建 (Core 层)

| # | ADR 标题 | 覆盖 TR 域 | 依赖 |
|---|----------|-----------|------|
| 5 | **输入系统架构：Input System 包 + Action Map 互斥门控** | TR-input-*, TR-ui-*, TR-interaction-* | 无 |
| 6 | **UI 框架：UI Toolkit 面板栈 + MVVM 数据绑定** | TR-ui-*, TR-hud-*, TR-menu-* | ADR-5 |
| 7 | **ScriptableObject 不可变配置 + ChangeTracker 可变 overlay 模式** | TR-changetracker-*, TR-fragment-*, TR-emotion-* | ADR-2 |

### P2 — 应在对应系统构建前创建 (Feature 层)

| # | ADR 标题 | 覆盖 TR 域 | 依赖 |
|---|----------|-----------|------|
| 8 | **ConditionGroup 求值引擎设计** | TR-fragment-*, TR-changetracker-*, TR-ending-* | ADR-7 |
| 9 | **网状关联引擎：四因子加权算法与候选池构建** | TR-webassociation-* | ADR-2 |
| 10 | **多结局判定算法：触发器 + 权重 + 情感亲和** | TR-ending-*, TR-chapter-* | ADR-8 |
| 11 | **跨章节状态追踪：Immutable Flag 与跨周目保护** | TR-crosschapter-* | ADR-3 |

### P3 — 可推迟到实现阶段 (Presentation + Polish)

| # | ADR 标题 | 覆盖 TR 域 | 依赖 |
|---|----------|-----------|------|
| 12 | **微动画系统：URP 2D Shader Graph + MaterialPropertyBlock** | TR-animation-* | 无 (Open Question 需先验证) |
| 13 | **音频架构：4 层 Mixer + 双轨交叉淡入淡出** | TR-audio-* | ADR-2 |
| 14 | **交互反馈映射表：10 事件 → 视觉+音频响应** | TR-feedback-* | ADR-1 |
| 15 | **本地化策略：Unity Localization 包 + 双字符串表** | TR-localization-* | 无 |

**创建顺序：** P0 (1-4) → P1 (5-7) → P2 (8-11) → P3 (12-15, 按需)

---

## Architecture Principles

1. **事件驱动，不轮询** — 系统间通信通过 C# 事件（`static event Action<T>`），MonoBehaviour.Update() 仅用于输入轮询和动画 Tick。无每帧状态检查。

2. **不可变配置 + 可变 Overlay** — ScriptableObject 运行时只读。所有玩家选择产生的变化写入 ChangeTracker._overlay Dictionary。查询时合并 base SO + overlay。存档只序列化 overlay。

3. **纯函数核心，薄 MonoBehaviour 壳** — 有复杂逻辑的系统（#13 关联引擎、#14 多结局）实现为纯 C# 类，构造函数注入依赖。MonoBehaviour 仅负责场景生命周期和事件订阅。

4. **Task-based 异步，带去重** — 所有 Addressables 加载返回 `Task<T>`。对同一未加载资源的并发请求返回同一个 Task 引用。调用方 await，不自旋等待。

5. **单向依赖，无循环** — 依赖方向：Presentation → Feature → Core → Foundation。所有 19 个系统依赖链经核查为有向无环图。

---

## Open Questions

- **`[SerializeReference]` 在 IL2CPP 构建中的稳定性**：MemoryFragment 的多态 ContentChange/ConditionGroup 依赖此特性。需在 Pre-Production 阶段用 IL2CPP 构建验证。（Owner: lead-programmer）
- **URP 2D Shader Graph 兼容性**：MicroAnimationManager 的 GPU 端动画（顶点位移、UV 滚动）需要确认 URP 2D SpriteLit/Unlit 管线支持。（Owner: unity-shader-specialist）
- **UI Toolkit MVVM 绑定性能**：HUD 的 INotifyBindablePropertyChanged 数据绑定在频繁更新（关联路径候选列表）时是否引入帧率影响。需用 60fps 目标验证。（Owner: unity-ui-specialist）
