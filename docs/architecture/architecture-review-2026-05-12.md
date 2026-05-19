# Architecture Review Report

**Date**: 2026-05-12
**Engine**: Unity 6.3 LTS
**GDDs Reviewed**: 19 (all MVP systems)
**ADRs Reviewed**: 15 (0001–0015)
**Review Mode**: full

---

## Traceability Summary

Total GDD requirements addressed in ADR tables: **92**
✅ Covered (explicit ADR reference): **86**
⚠️ Partial (implicit or multi-ADR coverage): **6**
❌ Gaps (no ADR exists for system): **0** (all 19 MVP systems have ADR coverage)

### Systems with Dedicated ADRs

| System | Dedicated ADR | Also Covered By |
|--------|--------------|-----------------|
| #1 Input System | ADR-0005 | ADR-0001 |
| #2 Data Management | ADR-0002 | — |
| #3 Audio System | ADR-0013 | ADR-0002 |
| #4 Localization | ADR-0015 | ADR-0001 |
| #5 UI Framework | ADR-0006 | ADR-0001, 0005 |
| #6 Scene Management | ADR-0004 | ADR-0001, 0002 |
| #7 Save/Load | ADR-0003 | ADR-0007 |
| #8 Memory Fragment Data Model | ADR-0007 | ADR-0008 |
| #9 Micro-Animation | ADR-0012 | — |
| #10 Emotional Tag System | — (piecemeal) | ADR-0009, 0010, 0012 |
| #11 Scroll Interaction | — (piecemeal) | ADR-0001, 0004, 0005, 0014 |
| #12 Memory Change Tracking | ADR-0007 | ADR-0001, 0003, 0008 |
| #13 Web Association Engine | ADR-0009 | ADR-0008 |
| #14 Multi-Ending System | ADR-0010 | ADR-0003, 0008 |
| #15 Chapter Management | — (piecemeal) | ADR-0001, 0007, 0010, 0011 |
| #16 Cross-Chapter State | ADR-0011 | ADR-0003 |
| #17 In-Game HUD | — (piecemeal) | ADR-0001, 0006, 0009, 0015 |
| #18 Interaction Feedback | ADR-0014 | ADR-0001, 0004, 0012, 0013 |
| #19 Main Menu | — (piecemeal) | ADR-0006, 0013, 0015 |

**Systems without dedicated ADRs** (5): #10 Emotional Tags, #11 Scroll Interaction, #15 Chapter Management, #17 In-Game HUD, #19 Main Menu. These are covered by cross-cutting ADRs but lack a single authoritative architectural decision document. This is acceptable for Presentation-layer systems (#17, #19) but **#10 (Emotional Tags) and #11 (Scroll Interaction) are Gameplay-layer systems** that would benefit from dedicated ADRs in Pre-Production.

---

## Coverage Gaps (from GDD Cross-Review)

These are GDD-level inconsistencies identified in the cross-review that affect architecture:

### Blocking (must resolve before implementation)

**❌ B1. InteractionManager missing public event definitions**
- `interaction-feedback.md` subscribes to 10 static events on `InteractionManager`
- `scroll-interaction-system.md` does not define these as public C# events
- **ADR resolution**: ADR-0001 declares 10 events, ADR-0014 subscribes to them. GDDs need updating.
- **Action**: Update `scroll-interaction-system.md` to document the 10 public static events.

**❌ B2. SceneManager.OnFragmentTransitionStarted event missing**
- `interaction-feedback.md` subscribes to `OnFragmentTransitionStarted` for feedback suppression
- `scene-management.md` TransitionToFragmentAsync only triggers `OnFragmentTransitioned`
- **ADR resolution**: ADR-0004 declares both events. GDD needs updating.
- **Action**: Update `scene-management.md` Rule 4 to add `OnFragmentTransitionStarted` before FadeOut.

### Warnings (should resolve, won't block)

**⚠️ W1. OnFragmentTransitioned parameter signature inconsistency**
- `scene-management.md`: `OnFragmentTransitioned(chapterKey, fragmentId)` — 2 params
- `memory-change-tracking.md`: `OnFragmentTransitioned(fragmentId)` — 1 param
- **Action**: Standardize to 2-parameter signature across all GDDs.

**⚠️ W2. Audio system downstream reference error**
- `audio-system.md` lists UI Framework (#5) as PlaySFX consumer
- `ui-framework.md` states audio is handled by Interaction Feedback (#18), not UI Framework
- **Action**: Remove #5 from audio-system.md downstream table.

**⚠️ W3. Scene management downstream list incomplete**
- Missing #12 (Memory Change Tracking) as `OnFragmentTransitioned` consumer
- **Action**: Add #12 to scene-management.md downstream table.

**⚠️ W4. Save loading bypasses ChapterManager state initialization**
- `save-load-system.md` Rule 4 calls SceneManager directly
- `chapter-management.md` Rule 10 requires `EnterChapterAtFragment()` for state init
- **ADR resolution**: ADR-0003 specifies ChapterManager as save consumer. GDD needs updating.
- **Action**: Update save-load-system.md to use `ChapterManager.LoadAndRestore(saveData)`.

---

## Cross-ADR Conflict Detection

### Dependency Graph (Topological Sort)

```
Foundation (no dependencies):
  1. ADR-0001 (Event Bus)
  2. ADR-0005 (Input System)
  3. ADR-0012 (Micro-Animation)
  4. ADR-0015 (Localization)

Foundation (light dependencies):
  5. ADR-0002 (Data Management) — requires ADR-0001
  6. ADR-0006 (UI Framework) — requires ADR-0001, ADR-0005

Core:
  7. ADR-0007 (SO Immutable Overlay) — requires ADR-0002
  8. ADR-0013 (Audio Architecture) — requires ADR-0002

Feature (depends on Core):
  9. ADR-0008 (Condition Group) — requires ADR-0007
 10. ADR-0003 (Save System) — requires ADR-0007
 11. ADR-0011 (Cross-Chapter State) — requires ADR-0007, ADR-0003

Feature (depends on Feature):
 12. ADR-0010 (Multi-Ending) — requires ADR-0008, ADR-0011
 13. ADR-0009 (Web Association) — requires ADR-0002, ADR-0008, ADR-0010
 14. ADR-0004 (Scene Management) — requires ADR-0001, ADR-0002

Presentation:
 15. ADR-0014 (Interaction Feedback) — requires ADR-0001, ADR-0012, ADR-0013
```

### Conflict Detection Results

| Check | Result |
|-------|--------|
| Data ownership conflicts | ✅ None — clear chain of custody (ChangeTracker owns overlay, CrossChapterTracker owns flags, SaveManager aggregates) |
| Integration contract conflicts | ⚠️ See GDD warnings W1-W4 above. ADRs are consistent; GDDs lag. |
| Performance budget conflicts | ✅ None — all systems fit within 16.6ms frame budget (Input ~0.1ms + Interaction ~0.2ms + MicroAnim ~1ms + WebAssoc <1ms + Ending <1ms = ~3.3ms total) |
| Dependency cycles | ✅ None detected — all dependency chains are directed acyclic |
| Architecture pattern conflicts | ✅ None — all systems follow ADR-0001's `static event Action<T>` pattern |
| State management conflicts | ✅ None — ADR-0007 owns overlay, ADR-0011 owns flags via ChangeTracker, ADR-0003 aggregates |

### Unresolved Dependencies

- ADR-0009 depends on ADR-0010 (labeled "情感标签系统"). ADR-0010 is the Multi-Ending algorithm, not the emotional tag system. The emotional tag system (#10) has no dedicated ADR. **This is a dependency description error** — ADR-0009 actually depends on emotional tag data model, which is partially defined in ADR-0010 (tag weight contribution to endings) and ADR-0012 (emotion presets for shaders). Clarify or create dedicated emotional tag ADR.

### Recommended Implementation Order

1. **Foundation first**: ADR-0001, 0005, 0015, 0012 → ADR-0002, 0006
2. **Core second**: ADR-0007, 0013 → ADR-0003, 0008
3. **Feature third**: ADR-0011, 0004 → ADR-0010 → ADR-0009
4. **Presentation last**: ADR-0014

---

## GDD Revision Flags (Architecture → Design Feedback)

These GDD assumptions conflict with verified ADR decisions or engine behaviour. GDDs should be revised before their systems enter implementation.

| GDD | Assumption | Reality (from ADR) | Action |
|-----|-----------|---------------------|--------|
| `scroll-interaction-system.md` | InteractionManager has no public static events | ADR-0001 declares 10 public static events on InteractionManager | **Revise GDD** — add event documentation |
| `scene-management.md` | Only `OnFragmentTransitioned` event exists | ADR-0004 declares both `OnFragmentTransitionStarted` + `OnFragmentTransitioned` | **Revise GDD** — add Started event |
| `memory-change-tracking.md` | `OnFragmentTransitioned(fragmentId)` single param | ADR-0004 uses `OnFragmentTransitioned(chapterKey, fragmentId)` | **Revise GDD** — update parameter signature |
| `audio-system.md` | UI Framework (#5) consumes PlaySFX | UI Framework delegates audio to Interaction Feedback (#18) | **Revise GDD** — correct downstream table |
| `scene-management.md` | Downstream consumers listed: #9, #11 | Missing #12 (Memory Change Tracking) | **Revise GDD** — add #12 |
| `save-load-system.md` | Load path calls SceneManager directly | Must call ChapterManager.LoadAndRestore for state initialization | **Revise GDD** — update Rule 4 |

---

## Engine Compatibility Issues

### Engine Audit Results

| Metric | Value |
|--------|-------|
| ADRs with Engine Compatibility section | 15 / 15 (100%) |
| Version consistency | ✅ All reference Unity 6.3 LTS |
| Deprecated API references | ✅ None detected |
| Stale version references | ✅ None |

### Knowledge Risk Distribution

| Risk Level | ADRs | Systems |
|-----------|------|---------|
| **HIGH** | ADR-0005 (Input System), ADR-0006 (UI Toolkit), ADR-0012 (Shader Graph) | Input, UI Framework, Micro-Animation |
| **MEDIUM** | ADR-0002 (Addressables), ADR-0007 (SerializeReference), ADR-0013 (Audio Mixer), ADR-0015 (Localization) | Data Mgmt, Memory Fragment, Audio, Localization |
| **LOW** | ADR-0001, 0003, 0004, 0008, 0009, 0010, 0011, 0014 | Event Bus, Save, Scene, Conditions, Web Assoc, Multi-Ending, Cross-Chapter, Feedback |

### Post-Cutoff APIs Used (by ADR)

| ADR | APIs | Verification Required |
|-----|------|----------------------|
| ADR-0002 | `Addressables.LoadAssetAsync<T>()`, `Release()`, `DownloadDependenciesAsync()` | IL2CPP exception type consistency; download timeout behavior |
| ADR-0005 | `InputActionAsset`, `PlayerInput`, `PerformInteractiveRebinding()`, `SaveBindingOverridesAsJson()` | `PerformInteractiveRebinding` in IL2CPP |
| ADR-0006 | `UIDocument`, `VisualElement`, `INotifyBindablePropertyChanged`, USS `var()` | MVVM binding stability; UI Toolkit runtime maturity |
| ADR-0007 | `[SerializeReference]` | Polymorphic serialization in IL2CPP; AOT code stripping |
| ADR-0012 | `Shader Graph` (URP 2D), `MaterialPropertyBlock.SetFloat()` | URP 2D SpriteLit/Unlit vertex displacement; SpriteRenderer compatibility |
| ADR-0013 | `AudioMixer`, `AudioMixerSnapshot`, `Addressables.LoadAssetAsync<AudioClip>()` | Exposed Parameters in IL2CPP; Snapshot transition at timeScale=0 |
| ADR-0015 | `com.unity.localization` — `LocalizationSettings`, `Locale`, `StringTable`, `LocalizedString` | Package version compatibility with 6.3 LTS; UI Toolkit binding |

### Engine Specialist Findings

**Consultant**: unity-specialist (claude-opus-4-6)
**Date**: 2026-05-12
**Scope**: All 15 ADRs + 4 engine reference modules

---

#### Finding 1: ADR-0005 Input System — Audit contains factual error

**Verdict: AUGMENTED**

The Phase 5 audit states ADR-0005 "Uses `PlayerInput`." This is incorrect. ADR-0005 explicitly **rejected** `PlayerInput` (Section: Alternative 2): "`PlayerInput` uses `SendMessage` or `UnityEvent` (has GC allocation); manual Action Map control is cleaner." The ADR uses manual `InputActionAsset` with generated C# wrapper class and manual `Enable()`/`Disable()` of Action Maps.

`InputActionAsset`, `PerformInteractiveRebinding()`, and `SaveBindingOverridesAsJson()` are all stable in Unity 6.3 LTS at Input System package version 1.11. `PerformInteractiveRebinding` IL2CPP issues were resolved in Input System 1.4+ (2023). The HIGH risk rating is appropriate only because the LLM cutoff predates the API — the API itself is mature.

**Missed concern**: The ADR mentions loading the `InputActionAsset` from "Resources/ or Addressables." The `Resources/` option contradicts ADR-0002. Should be resolved to Addressables only.

---

#### Finding 2: ADR-0006 UI Toolkit — MVVM throttling + CSS transition limits

**Verdict: CONFIRMED + AUGMENTED**

UI Toolkit runtime is production-ready in Unity 6.3 LTS. `UIDocument`, `VisualElement`, and USS `var()` APIs are stable.

Two concerns the audit missed:

1. **HUD MVVM update frequency**: `INotifyBindablePropertyChanged` triggers full element re-measure/layout on every property change. For HUD elements updating frequently, this can exceed the 0.1ms CPU budget. The ADR does not specify a throttling strategy (e.g., only notify on dirty, batch multiple property changes, or use `schedule.Execute()` to coalesce updates).

2. **USS `transition` property limits**: In Unity 6 UI Toolkit, the `transition` USS property supports `opacity`, `translate`, `scale`, and `rotate` — but NOT all properties. The ADR's assumption that CSS transitions are GPU-accelerated is only true for `opacity` and `transform`; other properties (color, width) animate on the CPU via layout recalculations.

---

#### Finding 3: ADR-0012 Shader Graph 2D — Risk more severe than audit suggests

**Verdict: AUGMENTED — Upgrade risk from HIGH to CRITICAL for vertex displacement**

The URP 2D Shader Graph concerns require significant nuance:

**Vertex displacement is PROBABLY NOT SUPPORTED**. URP 2D's SpriteLit/SpriteUnlit targets use a dedicated 2D vertex stage optimized for sprite rendering. The vertex position in a 2D sprite shader is typically a fixed quad — vertex displacement nodes (Position, Normal, Tangent) are designed for 3D mesh targets and may not compile or produce no visual effect when targeting the 2D renderer. The ADR's planned `_ParallaxOffset` for vertex displacement is at high risk of failure.

**UV scrolling should work**. `_FragmentTime` for UV animation operates in the fragment stage, which is standard across all Shader Graph targets.

**MaterialPropertyBlock with URP 2D SpriteRenderer is QUESTIONABLE**. The SRP Batcher (enabled by default in URP 6.x) can override `MaterialPropertyBlock` values because it batches by shader variant, not by material instance. In Unity 6.3, `MaterialPropertyBlock` with SpriteRenderer in URP 2D requires either: (a) disabling SRP Batcher for those materials, or (b) using `Material.SetFloat()` directly on a material instance (which creates copies and defeats batching). **Recommendation**: Add an explicit fallback of using instanced materials with `Material.SetFloat()` and accept the memory trade-off (~5KB per fragment, acceptable for 2D).

---

#### Finding 4: ADR-0002 Addressables Exceptions

**Verdict: CONFIRMED — ADR handles it correctly**

The `breaking-changes.md` confirms: "Addressables — Asset Loading Returns: Asset loading failures now throw exceptions by default instead of returning null." The ADR correctly wraps `Addressables.LoadAssetAsync<T>()` in try/catch. In IL2CPP builds, `InvalidKeyException` may surface as a generic `Exception` due to type stripping — the ADR's catch of `Exception` (not a specific type) is actually the correct defensive approach for IL2CPP compatibility.

---

#### Finding 5: ADR-0007 [SerializeReference] in IL2CPP

**Verdict: CONFIRMED — Standard mitigation, missing link.xml specifics**

`[SerializeReference]` with polymorphic types in IL2CPP is a documented risk. The ADR's mitigation (link.xml to retain types) is standard. **Missed**: The ADR shows `ContentChange[]` with 6 sub-types. If any sub-type is ONLY referenced through `[SerializeReference]` (not directly instantiated in code), the IL2CPP linker WILL strip it unless link.xml explicitly preserves it. The ADR should enumerate which specific types need link.xml preservation entries.

---

#### Finding 6: ADR-0013 Audio Mixer — Incorrect risk + defensible deviation

**Verdict: AUGMENTED**

**Exposed Parameters in IL2CPP**: These work correctly. Parameter names are strings in the Audio Mixer asset (a native asset), not subject to managed code stripping. `AudioMixer.SetFloat()`/`GetFloat()` interface with the native audio DSP, unaffected by IL2CPP.

**Snapshot transitions at Time.timeScale = 0**: This concern in the ADR's risk table is **INCORRECT**. The audio mixer DSP runs on the audio thread using real time (unscaled), completely independent of `Time.timeScale`. `AudioMixerSnapshot.TransitionTo()` will complete correctly even when `Time.timeScale = 0`. The ADR's mitigation (use `AudioListener.pause`) is good practice for pause menus for OTHER reasons, but unnecessary for audio snapshot transitions specifically.

**Missed concern**: The ADR loads the Audio Mixer from `Resources/` rather than Addressables. This is a ~1KB boot-critical asset, so the deviation is defensible, but the ADR should explicitly document this as an intentional exception to ADR-0002.

---

#### Finding 7: ADR-0015 Localization Package

**Verdict: CONFIRMED — Manual refresh fallback is robust**

`com.unity.localization` 1.4+ is compatible with Unity 6.x. The ADR's pragmatic fallback — using `OnLocaleChanged` event to manually refresh all UI text — is actually more robust than relying on automatic UI Toolkit binding for a project of this scale. Good architectural decision.

---

#### Finding 8: Static Event Pattern — Missing two concerns

**Verdict: AUGMENTED**

The `static event Action<T>` pattern (ADR-0001) is valid C# and works correctly in Unity. The audit missed:

1. **Static state survival across scene loads**: If a producing system's scene is unloaded, its static events survive (static members are GC roots). New scene subscribers see the old event with potentially stale producer state. The ADR does not address whether producers should null their static events on scene unload.

2. **Testing leakage**: Static events persist between test runs. If tests don't reset static state in `[TearDown]`, subscriptions from Test A leak into Test B. This should be documented for test authors.

---

#### Finding 9: 3-Scene Architecture

**Verdict: CONFIRMED — Pattern sound, minor hardening note**

The three-scene architecture (Boot → MainMenu → Game) with Addressables content injection is correct. One minor note: the ADR uses `SceneManager.LoadSceneAsync("Game")` with string scene names. A `SceneReference` wrapper would provide compile-time safety — string names fail at runtime if mistyped.

---

#### Finding 10: Deprecated API Check

**Verdict: CONFIRMED — No deprecated APIs, one defensible exception**

All ADRs correctly avoid deprecated Unity 6.3 APIs. The one exception: ADR-0013 loads the Audio Mixer from `Resources/` rather than Addressables (~1KB boot-critical asset). This is an acceptable exception but should be explicitly justified as a boot dependency.

---

#### Finding 11 (Specialist-Only): ADR-0007 uses `.Result` on Task — POTENTIAL DEADLOCK

**Verdict: BLOCKING anti-pattern in code sample**

The `GetCurrentState` code sample in ADR-0007 shows:
```csharp
var base = DataManager.GetFragmentAsync(chapterKey, fragmentId).Result;
```
This is a **blocking call on the main thread**. In Unity, `Task.Result` on the main thread can deadlock if the task requires the Unity synchronization context to complete. The ADR assumes the asset is "already in memory," but this is an implementation detail that should not be baked into the API. An async design should propagate `await` up the call chain.

---

#### Finding 12 (Specialist-Only): ADR-0010 LINQ GC estimates optimistic

**Verdict: ADVISORY**

The `ResolveEnding` algorithm uses multiple LINQ chains (`.Where()`, `.Select()`, `.OrderByDescending()`, `.ThenBy()`) with lambda closures. The ADR's performance table says "~100B GC Allocation" — a single LINQ chain with closures typically allocates 200-500B+. For a function called once per chapter end (not per frame), this is acceptable. The estimate should be revised to ~500B for accuracy.

---

#### Finding 13 (Specialist-Only): ADR-0013 SFX pool reinvents Unity ObjectPool

**Verdict: ADVISORY**

ADR-0013 implements a manual array-based SFX pool with priority system. Unity 6 includes `UnityEngine.Pool.ObjectPool<T>` which provides object lifecycle management, diagnostics, and leak detection. While the priority logic needs custom code, the underlying AudioSource management could use `ObjectPool<AudioSource>` for consistency with Unity patterns and built-in diagnostics.

---

#### Finding 14 (Specialist-Only): Lambda closures in hot-path event subscriptions

**Verdict: ADVISORY**

ADR-0001 specifies "zero GC allocation" for event dispatch, but this only applies to invocation. If a consumer subscribes with a lambda that captures variables (`+= id => Handle(id, someLocal)`), that allocation happens at subscription time. The ADR should explicitly warn against lambda subscriptions for hot-path events and require method group subscription (`+= HandleChoiceSelected` without lambda).

---

#### Finding 15 (Specialist-Only): No use of Unity 6 `Awaitable` API

**Verdict: OPTIMIZATION OPPORTUNITY**

All ADRs use `Task<T>` which is valid and portable, but Unity 6 introduced `Awaitable` as a lightweight alternative designed for Unity's player loop. `Awaitable` avoids `Task` allocation overhead and integrates with Unity's frame-aware scheduling. This is an optimization opportunity for Pre-Production, not a concern for Technical Setup.

---

#### Specialist Findings Summary

| # | Finding | Severity | Action Required |
|---|---------|----------|-----------------|
| 1 | ADR-0005: `PlayerInput` not actually used (audit error) | Info | Correct audit record |
| 2 | ADR-0006: MVVM throttling + CSS transition limits | Warning | Add throttling strategy to ADR-0006 |
| 3 | ADR-0012: Vertex displacement likely unsupported; MPB+URP 2D needs fallback | **CRITICAL** | Add explicit fallback to ADR-0012 |
| 4 | ADR-0002: Addressables exceptions correctly handled | Info | — |
| 5 | ADR-0007: Missing explicit link.xml per sub-type | Warning | Enumerate types in ADR-0007 |
| 6 | ADR-0013: TimeScale+Snapshot risk is incorrect; Resources.Load deviation | Warning | Document Resources.Load exception |
| 7 | ADR-0015: Manual refresh fallback is robust | Info | — |
| 8 | ADR-0001: Static event survival across scene unload + test leakage | Warning | Document in ADR-0001 |
| 9 | 3-Scene: String scene names lack compile-time safety | Advisory | Consider SceneReference |
| 10 | No deprecated APIs; one defensible Resources.Load | Info | — |
| 11 | ADR-0007: `.Result` blocking on main thread | **BLOCKING** | Fix code sample in ADR-0007 |
| 12 | ADR-0010: LINQ GC estimate optimistic (~500B, not 100B) | Advisory | Revise estimate |
| 13 | ADR-0013: Manual SFX pool vs Unity ObjectPool | Advisory | Consider ObjectPool |
| 14 | ADR-0001: Lambda closure warning missing | Advisory | Add guidance to ADR-0001 |
| 15 | Task<T> vs Unity 6 Awaitable | Optimization | Consider in Pre-Production |

**New blocking issue**: Finding 11 (`.Result` deadlock in ADR-0007 code sample) must be resolved. This is a code sample anti-pattern that could propagate to implementation.

**New critical issue**: Finding 3 (Shader Graph 2D vertex displacement likely unsupported) requires an explicit fallback plan in ADR-0012 before micro-animation implementation begins.

---

## Architecture Document Coverage

**Source**: `docs/architecture/architecture.md`

| Check | Result |
|-------|--------|
| All 19 MVP systems in module ownership | ✅ Yes — every system has a Module Ownership entry |
| Data flow covers cross-system communication | ✅ Yes — frame update path, event/signal path, and save/load path documented |
| API boundaries support GDD integration requirements | ✅ Yes — IDataManager, IInputManager, ISceneManager, ISaveManager, IUIPanelStack, IChangeTracker all defined |
| Orphaned architecture (systems in arch doc without GDD) | ✅ None |
| Missing from architecture (GDD systems not in arch doc) | ✅ None |
| Layer organization difference | ⚠️ Minor — Audio (#3) is Foundation in systems-index but Presentation in architecture.md; Input (#1) is Foundation in systems-index but Core in architecture.md. These are organizational, not functional conflicts. |

---

## Verdict: CONCERNS

**The architecture is fundamentally sound for Pre-Production entry.** No dependency cycles exist. All 19 MVP systems have ADR coverage. Engine compatibility is documented with appropriate risk ratings. Performance budgets are within targets. Data ownership is clear with no conflicts.

### Rationale for CONCERNS (not PASS)

1. **6 GDD revision flags** — GDDs have integration contract drift from ADR decisions (B1, B2, W1-W4). These are low-effort fixes but should be resolved before implementation stories are written.

2. **5 systems lack dedicated ADRs** — Emotional Tags (#10), Scroll Interaction (#11), Chapter Management (#15), In-Game HUD (#17), and Main Menu (#19) are covered piecemeal by cross-cutting ADRs. For Gameplay-layer systems (#10, #11, #15), dedicated ADRs are recommended before implementation.

3. **3 HIGH risk engine items verified** — Input System (ADR-0005), UI Toolkit (ADR-0006), and Shader Graph (ADR-0012) reviewed by unity-specialist. One CRITICAL finding: Shader Graph 2D vertex displacement likely unsupported (Finding 3).

4. **ADR-0009 dependency description error** — References ADR-0010 as "情感标签系统" but ADR-0010 is Multi-Ending. The emotional tag system dependency chain needs clarification.

5. **NEW BLOCKING: ADR-0007 `.Result` deadlock** — Code sample in ADR-0007 uses `Task.Result` on main thread, which can deadlock in Unity (Specialist Finding 11). Must be fixed before implementation.

### NOT FAIL because

- No blocking ADR-level conflicts (data ownership, dependency cycles, pattern conflicts)
- No deprecated API usage
- All performance budgets fit within frame target
- Cross-review blocking issues (B1, B2) are GDD-level, not ADR-level — resolved in ADRs, just need GDD updates
- P3 ADRs (0012-0015) are now created, filling previous gaps
- Specialist confirmed all 15 ADRs avoid deprecated APIs; one defensible Resources.Load exception (ADR-0013)

---

## Immediate Actions (before Pre-Production)

1. **Fix 6 GDD revision flags** — Update the 6 flagged GDDs per the table above (estimated: 1 session)
2. **Resolve ADR-0009 dependency description** — Clarify whether it depends on emotional tag system or multi-ending system
3. **Fix ADR-0007 `.Result` deadlock (BLOCKING)** — Replace `Task.Result` blocking call with `await` in `GetCurrentState` code sample (Specialist Finding 11)
4. **Add Shader Graph 2D fallback to ADR-0012 (CRITICAL)** — Document explicit fallback for vertex displacement (likely unsupported) and MaterialPropertyBlock + SRP Batcher compatibility (Specialist Finding 3)
5. **Complete remaining engine verifications** — Per Specialist Findings table above, address the 5 Warnings (Findings 2, 5, 6, 8) and 4 Advisory items (Findings 9, 12, 13, 14)
6. **Consider dedicated ADRs** for #10 (Emotional Tags) and #11 (Scroll Interaction) — both are Gameplay-layer systems that would benefit from dedicated architectural decisions
7. **Update TR Registry** — Already populated with 92 TR-IDs; verify against specialist extraction data (452 raw TR-REQs from 19 GDDs)

### Gate Guidance

- **To transition to Pre-Production**: Resolve items 1-5 above. Items 6-7 can be done during Pre-Production.
- **Rerun `/architecture-review`**: After GDD revisions are applied and engine verifications complete.
- **Stage update**: After this review's findings are addressed, update `production/stage.txt` to "Pre-Production".

---

## Appendix: ADR Status Summary

| ADR | Title | Status | Layer | Risk |
|-----|-------|--------|-------|------|
| ADR-0001 | Event Bus Architecture | Accepted | Foundation | LOW |
| ADR-0002 | Data Management Strategy | Accepted | Foundation | MEDIUM |
| ADR-0003 | Save Serialization Strategy | Accepted | Foundation | LOW |
| ADR-0004 | Scene Management | Accepted | Foundation | LOW |
| ADR-0005 | Input System Architecture | Accepted | Foundation | HIGH |
| ADR-0006 | UI Framework | Accepted | Foundation | HIGH |
| ADR-0007 | SO Immutable Overlay Pattern | Accepted | Core | MEDIUM |
| ADR-0008 | Condition Group Engine | Accepted | Feature | LOW |
| ADR-0009 | Web Association Engine | Accepted | Feature | LOW |
| ADR-0010 | Multi-Ending Algorithm | Accepted | Feature | LOW |
| ADR-0011 | Cross-Chapter State | Accepted | Feature | LOW |
| ADR-0012 | Micro-Animation System | Accepted | Presentation | HIGH |
| ADR-0013 | Audio Architecture | Accepted | Presentation | MEDIUM |
| ADR-0014 | Interaction Feedback Mapping | Accepted | Presentation | LOW |
| ADR-0015 | Localization Strategy | Accepted | Foundation | MEDIUM |
