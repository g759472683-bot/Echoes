# Story 004: 章节过渡 + 预加载协调

> **Epic**: 场景管理系统 (SceneManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/scene-management.md`
**Requirement**: `TR-scene-management-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: 场景管理与转场状态机, ADR-0002: 数据管理策略
**ADR Decision Summary**: 章节预加载在剩余 ≤3 碎片时触发 — `Task.WhenAll` 并行预加载插图 + 音频；章节过渡时 await 预加载 Task 确保就绪

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `DownloadDependenciesAsync` 在 IL2CPP 中的超时行为需验证；`Addressables.Release` 在 Fast Mode 下行为与构建不同

**Control Manifest Rules (Foundation Layer)**:
- Required: Chapter preload trigger at ≤3 fragments remaining — preload next chapter assets in background
- Required: Preload trigger when remaining fragments ≤3 — parallel preload via Task.WhenAll for illustrations + audio
- Guardrail: Chapter preload trigger: 3 fragments remaining, background Task

---

## Acceptance Criteria

*From GDD `design/gdd/scene-management.md`, scoped to this story:*

- [ ] GIVEN 章节 1 剩余 3 个碎片，WHEN 玩家进入倒数第 3 个碎片，THEN 后台触发章节 2 的插图 + 音频预加载。到达章节边界时 ChapterTransition 立即或近立即执行
- [ ] GIVEN 玩家到达章节 1 结尾，WHEN ChapterTransition 触发，THEN 音乐交叉淡出 → 卸载 Ch01 Addressables → 加载 Ch02 Addressables → 新章音乐淡入 → 遮罩淡出展示 Ch02 第一个碎片
- [ ] 预加载并行: `Task.WhenAll(DataManager.PreloadChapterAsync, AudioManager.PreloadChapterAudioAsync)`
- [ ] 玩家快速跳过最后 3 个碎片时（<30 秒），预加载可能未完成——`TransitionToChapterAsync` await 预加载 Task，遮罩保持覆盖
- [ ] `PreloadNextFragmentAsync` 预加载下一碎片内容（fire-and-forget）——使碎片切换零延迟

---

## Implementation Notes

*Derived from ADR-0004:*

预加载触发逻辑:
```csharp
private void CheckPreloadTrigger(string chapterKey, string currentFragmentId)
{
    var chapter = _dataManager.GetChapter(chapterKey);
    var fragments = chapter.Fragments;
    int currentIndex = fragments.IndexOf(f => f.Id == currentFragmentId);
    int remaining = fragments.Count - currentIndex - 1;
    
    if (remaining <= _preloadThreshold && !_preloadStarted)
    {
        _preloadStarted = true;
        _ = PreloadNextChapterAsync(chapter.NextChapterKey);
    }
}
```

TransitionToChapterAsync:
```csharp
public async Task TransitionToChapterAsync(string chapterKey)
{
    _currentState = TransitionState.ChapterTransition;
    
    // Step 1: 触发音乐交叉淡出
    _audioManager.StopMusic(fadeTime: 1.0f);
    
    // Step 2: SceneFader 遮罩淡出
    await _sceneFader.FadeOut(1.0f);
    _currentState = TransitionState.Loading;
    
    // Step 3: 卸载旧章节 Addressables
    _dataManager.UnloadChapter(_currentChapterKey);
    _audioManager.UnloadChapterAudio(_currentChapterKey);
    
    // Step 4: await 预加载 Task (如果已完成则立即返回)
    if (_preloadTask != null)
        await _preloadTask;
    
    // Step 5: 加载新章 (如果预加载未执行或失败，主路径加载)
    await _dataManager.PreloadChapterAsync(chapterKey);
    await _audioManager.PreloadChapterAudioAsync(chapterKey);
    
    // Step 6: 加载新章第一个碎片
    var firstFragment = _dataManager.GetChapter(chapterKey).EntryFragmentId;
    await LoadFragmentContent(chapterKey, firstFragment);
    
    // Step 7: 新章音乐淡入
    _audioManager.PlayMusic(chapterKey, fadeTime: 1.0f);
    
    // Step 8: 遮罩淡入
    await _sceneFader.FadeIn(1.0f);
    
    _currentChapterKey = chapterKey;
    _currentState = TransitionState.Idle;
    OnFragmentTransitioned?.Invoke(chapterKey, firstFragment);
}
```

并行预加载:
```csharp
private async Task PreloadNextChapterAsync(string chapterKey)
{
    try
    {
        await Task.WhenAll(
            _dataManager.PreloadChapterAsync(chapterKey),
            _audioManager.PreloadChapterAudioAsync(chapterKey)
        );
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"Preload failed for {chapterKey}: {ex.Message}");
        // 不抛异常 — 主加载路径处理
    }
}
```

---

## Out of Scope

*Handled by neighbouring stories or systems:*

- Story 003: 碎片过渡引擎 — TransitionToFragmentAsync
- 音乐管理 — 由 Audio System (#3) 负责
- Addressables 加载逻辑 — 由 Data Management (#2) 负责
- 章节完成判定 — 由 Chapter Management (#15) 负责

---

## QA Test Cases

- **AC-1**: 预加载触发
  - Given: 章节 1 有 10 个碎片，PreloadThreshold = 3
  - When: 玩家进入第 8 个碎片 (index 7, 剩余 3)
  - Then: 后台启动 `PreloadNextChapterAsync("ch2")`；插图 + 音频并行预加载
  - Edge cases: 第 7 个碎片时 (剩余 4) → 不触发；第 9 个碎片时 (剩余 2) → 已触发，不重复

- **AC-2**: 章节过渡完整流程
  - Given: 玩家在章节 1 最后一个碎片，章节 2 预加载已完成
  - When: 触发章节完成 → `TransitionToChapterAsync("ch2")`
  - Then: 音乐淡出 (1s) → 遮罩覆盖 → 卸载 Ch01 → 加载 Ch02 (预加载已完成，立即返回) → 新章音乐淡入 → 遮罩淡出 → 显示 Ch02 第一个碎片
  - Edge cases: 预加载未完成 → await 等待直到完成 → 遮罩保持覆盖

- **AC-3**: Task.WhenAll 并行预加载
  - Given: 预加载触发
  - When: `PreloadNextChapterAsync` 执行
  - Then: DataManager.PreloadChapterAsync 和 AudioManager.PreloadChapterAudioAsync 同时运行 (非先后)
  - Edge cases: 其中一个失败 → 另一个继续 → 日志记录哪个失败

- **AC-4**: 快速跳过 > 等待加载
  - Given: 预加载刚触发 1 秒 (尚未完成)
  - When: 玩家快速跳过最后 3 个碎片到达章节边界
  - Then: `TransitionToChapterAsync` await 未完成的预加载 Task；遮罩保持黑色覆盖直到加载完成
  - Edge cases: 预加载超时 30s → 主加载路径代替 → 日志 warning

- **AC-5**: PreloadNextFragmentAsync fire-and-forget
  - Given: 当前碎片加载完成
  - When: 调用 `PreloadNextFragmentAsync`
  - Then: Task 在后台运行；不阻塞当前碎片交互；下一碎片切换时数据已缓存
  - Edge cases: 预加载中玩家触发其他碎片 → 预加载结果仍可用于该碎片

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/scene-management/chapter_transition_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003 (碎片过渡引擎), Data Management (#2) 的 PreloadChapterAsync, Audio System (#3) 的 PreloadChapterAudioAsync
- Unlocks: Chapter Management (#15) 的章节完成流程

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 5/5 passing (all auto-verified by 12 integration tests)
**Deviations**:
- ADVISORY: Duplicate `GetNextChapterKey` with incompatible parsing between GameSceneManager and DataManager — extract to shared utility in follow-up
- ADVISORY: Fire-and-forget `_ =` discard at PreloadNextFragmentAsync without exception observation — wrap in try/catch matching PreloadNextChapterAsync pattern
- ADVISORY: TransitionToChapterAsync re-calls PreloadChapterAsync after successful background preload — DataManager has idempotency guard but redundant
- BLOCKING (resolved): PreloadFragmentAsync missing in DataManager → implemented at DataManager.cs:428-444
- BLOCKING (resolved): OnChapterTransitioned fired (new, new) instead of (old, new) → captured `oldChapterKey` at GameSceneManager.cs:631
**Test Evidence**: `tests/integration/scene-management/chapter_transition_test.cs` — 12 test functions covering AC-1 through AC-5 + 2 edge cases
**Code Review**: Complete — APPROVED WITH SUGGESTIONS (unity-specialist + qa-tester), 2 BLOCKING fixed, 4 MEDIUM deferred
