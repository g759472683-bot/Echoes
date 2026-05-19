# Control Manifest

> **Engine**: Unity 6.3 LTS
> **Last Updated**: 2026-05-19
> **Manifest Version**: 2026-05-19
> **ADRs Covered**: ADR-0001 ~ ADR-0018
> **Status**: Active — regenerate with `/create-control-manifest update` when ADRs change

This manifest is a programmer's quick-reference extracted from all Accepted ADRs,
technical preferences, and engine reference docs. For the reasoning behind each
rule, see the referenced ADR.

---

## Foundation Layer Rules

*Applies to: event architecture, data management, save/load, scene management, input, UI framework, localization*

### Required Patterns

- **`static event Action<T>` for all cross-system communication** — events declared in producer, subscribed in consumer OnEnable/OnDisable — source: ADR-0001
- **Method group subscription for hot-path events** — `+= HandleChoiceSelected` not `+= (id) => Handle(id, someLocal)` (lambda closures alloc GC) — source: ADR-0001
- **Producer OnDestroy nullification** — any MonoBehaviour declaring static events must set them to null in OnDestroy() to prevent stale delegate chains across scene loads — source: ADR-0001
- **Test TearDown static event reset** — all subscribed static events must be nulled in `[TearDown]` to prevent cross-test leakage — source: ADR-0001
- **Addressables for all asset loading** — `LoadAssetAsync<T>()`, never Resources.Load (except ~1KB boot-critical AudioMixer asset — source: ADR-0013) — source: ADR-0002
- **Three-state async readiness model** — Cached / Loading / NotRequested for all loaded assets — source: ADR-0002
- **Concurrent request deduplication** — same-key loads return shared Task reference — source: ADR-0002
- **Chapter preload trigger at ≤3 fragments remaining** — preload next chapter assets in background — source: ADR-0002, ADR-0004
- **JSON + SHA-256 checksum for save files** — System.Text.Json, atomic write (.tmp → .sav), version migration chain — source: ADR-0003
- **3-scene architecture** — Boot → MainMenu → Game, content via Addressables injection — source: ADR-0004
- **Unified ink-fade transition** — SceneFader VisualElement with opacity transition for all transitions — source: ADR-0004
- **OnFragmentTransitionStarted BEFORE fade-out** — suppress interaction feedback before transition begins — source: ADR-0004
- **OnFragmentTransitioned AFTER fade-in** — resume interaction after transition completes — source: ADR-0004
- **Unity New Input System only** — InputActionAsset with Gameplay/UI Action Maps, exclusive gating — source: ADR-0005
- **Single Physics2D.OverlapPoint per frame** — no EventSystem/Raycaster for interactable detection — source: ADR-0005
- **UI Toolkit (UIDocument + VisualElement) for all runtime UI** — UXML structure + USS styling — source: ADR-0006
- **LIFO panel stack (max depth 10)** — auto input gating (stack non-empty → UI Map, empty → Gameplay Map) — source: ADR-0006
- **MVVM data binding with throttling** — INotifyBindablePropertyChanged, batch updates with dirty flag, max 10Hz HUD refresh — source: ADR-0006
- **Theme.uss global CSS variables for visual consistency** — all color/spacing/font/transition values defined in :root — source: ADR-0006
- **USS transition only for opacity/transform** — GPU-accelerated properties only; color/size changes use code-driven tweens — source: ADR-0006
- **Unity Localization package with dual StringTable** — UI_Shared (persistent) + Narrative_Ch[N] (per-chapter) — source: ADR-0015
- **Fallback chain en → zh-Hans** — missing English falls back to Chinese, never blank text — source: ADR-0015
- **OnLocaleChanged static event for all UI refresh** — per ADR-0001 pattern — source: ADR-0015
- **Archive LocaleCode persisted to SaveData** — language selection survives restart — source: ADR-0015

### Forbidden Approaches

- **Never use `Task.Result` or `Task.Wait()` on main thread** — causes deadlock in Unity SynchronizationContext; always `await` — source: ADR-0007
- **Never use string-key EventBus** — no compile-time type safety, GC allocation per emit — source: ADR-0001
- **Never use `UnityEvent<T>`** — ~40B GC per invoke, Inspector configuration unscalable beyond 4 subscribers — source: ADR-0001
- **Never write to ScriptableObject at runtime** — pollutes Editor data; use ChangeTracker overlay instead — source: ADR-0007
- **Never use `Resources.Load()` (except AudioMixer)** — use Addressables for all asset loading — source: ADR-0002, ADR-0013
- **Never use Legacy Input Manager (`Input.GetKey`, `Input.mousePosition`)** — use New Input System package — source: ADR-0005
- **Never use UGUI Canvas** — use UI Toolkit for all new UI — source: ADR-0006
- **Never load Narrative StringTables without chapter preload** — sync StringTable loading with fragment asset preloading — source: ADR-0015
- **Never hardcode player-facing strings** — all text via LocalizationManager.GetLocalizedString() — source: ADR-0015
- **Never pass MonoBehaviour references through events** — use value types, string IDs, or immutable records — source: ADR-0001
- **Never create EventArgs subclasses** — use value type parameters for events — source: ADR-0001
- **Never leave static event subscriptions undisposed** — always unsubscribe in OnDisable/OnDestroy — source: ADR-0001

### Performance Guardrails

- **Event dispatch**: ~0.001ms per invoke, 0 GC — source: ADR-0001
- **Addressables fragment load**: <100ms for cached, <500ms for initial — source: ADR-0002
- **Save file write**: <200ms (JSON serialize + SHA-256 + atomic write) — source: ADR-0003
- **Fragment transition**: 0.5s fade-out + 0.5s fade-in = 1.0s total — source: ADR-0004
- **Chapter preload trigger**: 3 fragments remaining, background Task — source: ADR-0004
- **Input polling**: single OverlapPoint per frame (~0.1ms) — source: ADR-0005
- **UI panel push/pop**: ~1-2ms (VisualElement construction) — source: ADR-0006
- **UI data binding update**: ~0.1ms per property, throttled to 10Hz for HUD — source: ADR-0006
- **Localization lookup**: ~0.01ms (cached StringTable lookup), 0 GC — source: ADR-0015

---

## Core Layer Rules

*Applies to: data model overlay, change tracking*

### Required Patterns

- **Base SO (immutable) + ChangeTracker._overlay (mutable) two-layer model** — SO fields readonly at runtime, player changes in Dictionary overlay — source: ADR-0007
- **SaveData only serializes overlay, not base SO** — base SO reloaded from Addressables on game start — source: ADR-0007
- **GetCurrentState returns ResolvedFragmentState struct** — immutable snapshot, consumers cannot modify base or overlay — source: ADR-0007
- **6 ContentChange types with defined overlay merge algorithms** — ToggleVisualLayer, SetObjectState, SetTextContent, ModifyTagWeight, UnlockAssociation, SetFlag — source: ADR-0007
- **Overlay merge by OrderIndex ascending** — later changes override earlier ones for same fields — source: ADR-0007
- **[SerializeReference] link.xml preservation** — 16 types must be explicitly preserved for IL2CPP (6 ContentChange + 9 Condition + ContentOverrides) — source: ADR-0007

### Forbidden Approaches

- **Never directly modify ScriptableObject fields at runtime** — use ApplyChanges() → _overlay — source: ADR-0007
- **Never serialize base SO data into save files** — save only overlay + flags — source: ADR-0007
- **Never use `Task.Result` in GetCurrentState** — must be async with `await` — source: ADR-0007

### Performance Guardrails

- **GetCurrentState**: ~0.05ms (Dictionary lookup + struct copy) — source: ADR-0007
- **ApplyChanges (10 changes)**: ~0.1ms — source: ADR-0007
- **_overlay memory (50h gameplay)**: ~100-300KB — source: ADR-0007

---

## Feature Layer Rules

*Applies to: condition evaluation, web association engine, multi-ending system, cross-chapter state tracking*

### Required Patterns

- **Composite ConditionGroup pattern** — 6 leaf conditions + 3 combinators (All/Any/Not), max depth 3, short-circuit evaluation — source: ADR-0008
- **Condition evaluation triggered on query, not on state change** — EvaluateCondition() called when systems need to know — source: ADR-0008
- **Four-factor web association formula** — `Score = (TagSimilarity × 0.6 + ExplicitWeight × 0.4) × RhythmPenalty × DiscoveryBoost` — source: ADR-0009
- **Candidate pool filtering** — same chapter, unlocked, conditions met, exclude self — source: ADR-0009
- **Visual grading thresholds** — Strong (≥0.8), Medium (0.5-0.79), Faint (0.25-0.49), Trace (<0.25) — source: ADR-0009
- **3-stage multi-ending resolution** — collect triggers → IsEssential gate → accumulate ContributionWeight + EmotionalAffinity → threshold → tie-breaking — source: ADR-0010
- **Deterministic tie-breaking** — Score DESC → TriggerCount DESC → EndingId ASC — source: ADR-0010
- **Fallback ending guaranteed per chapter** — at least one ending always reachable — source: ADR-0010
- **CrossChapterFlagRegistry SO + IsImmutable protection** — immutable flags cannot be modified on replay — source: ADR-0011
- **SetFlagRaw internal interface for CrossChapterTracker only** — prevents arbitrary flag modification — source: ADR-0011

### Forbidden Approaches

- **Never exceed ConditionGroup depth 3** — deeper nesting becomes unmaintainable and unbalanceable — source: ADR-0008
- **Never present more than 5 association paths simultaneously** — information overload degrades player choice quality — source: ADR-0009
- **Never allow ending resolution without at least one fallback ending** — source: ADR-0010
- **Never reset CrossChapterFlags on chapter replay** — flags are game-scope persistent — source: ADR-0011
- **Never allow direct CrossChapterFlag modification from outside CrossChapterTracker** — use SetFlagRaw — source: ADR-0011

### Performance Guardrails

- **ConditionGroup evaluation**: <0.01ms (short-circuit, max depth 3) — source: ADR-0008
- **Web association calculation**: <1ms (per chapter end, not per frame) — source: ADR-0009
- **Candidate pool size**: ≤40 (same-chapter fragments) — source: ADR-0009
- **Multi-ending resolution**: <1ms (per chapter end) — source: ADR-0010
- **Cross-chapter flag lookup**: ~0.001ms (Dictionary Get) — source: ADR-0011

---

## Presentation Layer Rules

*Applies to: micro-animation, audio, interaction feedback*

### Required Patterns

- **Shader Graph + MaterialPropertyBlock for GPU-side animation** — 3 categories: Ambient (looping), Triggered (one-shot), Feedback (instant) — source: ADR-0012
- **MicroTween value type (~250 lines, zero GC)** — custom tween engine, no DOTween dependency — source: ADR-0012
- **4-level performance degradation** — High → Medium → Low → Minimal based on frameTime thresholds — source: ADR-0012
- **Fallback: CPU transform for vertex displacement** — if Shader Graph vertex nodes fail on URP 2D — source: ADR-0012
- **Fallback: Material.SetFloat() if MaterialPropertyBlock conflicts with SRP Batcher** — accept ~50KB memory cost — source: ADR-0012
- **4-layer Audio Mixer** — Master → SFX / Music (Music_A + Music_B) / Ambience — source: ADR-0013
- **Dual-track music crossfade** — Snapshot.TransitionTo() between Music_A_Active and Music_B_Active — source: ADR-0013
- **10-source SFX priority pool** — steal lowest-priority source when pool full — source: ADR-0013
- **Volume via Exposed Parameters + PlayerPrefs** — linear-to-dB conversion with Log10(0) guard — source: ADR-0013
- **Audio Mixer from Resources.Load as intentional ADR-0002 exception** — ~1KB boot-critical asset — source: ADR-0013
- **10-event → visual+audio mapping table** — centralized feedback definition per InteractionManager events — source: ADR-0014
- **Feedback priority system (0-10)** — higher-priority feedback suppresses lower — source: ADR-0014
- **300ms debounce per (objectId, eventName)** — prevents hover/click event storms — source: ADR-0014
- **Transition suppression via _feedbackSuppressed flag** — all feedback inhibited during scene transitions — source: ADR-0014
- **Pure event-driven (no Update())** — subscribes 10 InteractionManager + 2 SceneManager events — source: ADR-0014

### Forbidden Approaches

- **Never use Animator + AnimationClip for per-fragment effects** — GC allocation, poor scalability for 10+ renderers — source: ADR-0012
- **Never use DOTween** — external dependency, internal GC allocation — source: ADR-0012
- **Never assume Shader Graph vertex displacement works on URP 2D** — verify in prototype; use CPU fallback if needed — source: ADR-0012
- **Never use single Music AudioSource (no crossfade)** — hard cuts degrade immersion — source: ADR-0013
- **Never use FMOD/WWise** — over-engineered for 2D narrative game; Unity Audio Mixer sufficient — source: ADR-0013
- **Never exceed 10 simultaneous SFX sources** — pool hard limit prevents audio source explosion — source: ADR-0013
- **Never implement feedback per-system** — centralized mapping table guarantees consistency — source: ADR-0014
- **Never fire feedback during scene transitions** — _feedbackSuppressed flag must be checked — source: ADR-0014

### Performance Guardrails

- **Micro-animation (High perf, 10 ambient + 2 feedback)**: ~1.0ms CPU — source: ADR-0012
- **Micro-animation (Minimal perf)**: ~0.05ms CPU — source: ADR-0012
- **Micro-animation memory (10 instances)**: ~5KB — source: ADR-0012
- **SFX PlaySFX latency (cached)**: ~0.1ms CPU — source: ADR-0013
- **Music crossfade**: ~0.2ms (Snapshot transition, audio thread) — source: ADR-0013
- **SFX pool memory**: ~2KB (10 AudioSource components) — source: ADR-0013
- **Interaction feedback single execution**: ~0.2ms — source: ADR-0014
- **Interaction feedback debounce check**: ~0.01ms — source: ADR-0014
- **All audio/shaders/feedback systems combined**: ~3.3ms per frame (within 16.6ms budget) — source: ADR-0001~0015

---

## Global Rules (All Layers)

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `PlayerController`, `AudioManager` |
| Public fields/properties | PascalCase | `MoveSpeed`, `CurrentHealth` |
| Private fields | _camelCase | `_currentHealth`, `_overlay` |
| Methods | PascalCase | `TakeDamage()`, `PlaySFX()` |
| Events | PascalCase + no suffix | `OnChoiceSelected`, `OnFragmentTransitioned` |
| Files | PascalCase matching class | `PlayerController.cs` |
| Scenes/Prefabs | PascalCase matching content | `MemoryFragment.prefab` |
| Constants | UPPER_SNAKE_CASE | `MAX_MEMORY_FRAGMENTS` |

### Performance Budgets

| Target | Value |
|--------|-------|
| Framerate | 60fps |
| Frame budget | 16.6ms |
| Draw calls | 50-100 |
| Memory ceiling | 2GB |

### Approved Libraries / Addons

- `com.unity.inputsystem` — Input handling (required, replaces legacy Input)
- `com.unity.addressables` — Asset management (required, replaces Resources)
- `com.unity.localization` — String localisation (required)
- `System.Text.Json` — Save file serialisation (Unity 6 built-in, no extra package)
- Unity Test Framework (NUnit) — Automated testing

### Forbidden APIs (Unity 6.3 LTS)

| Deprecated API | Replacement | Domain |
|---------------|-------------|--------|
| `Input.GetKey/GetMouseButton/GetAxis` | New Input System (`Keyboard.current`, `Mouse.current`) | Input |
| `Canvas` (UGUI) | `UIDocument` (UI Toolkit) | UI |
| `Text` component | UI Toolkit `Label` | UI |
| `Resources.Load()` | `Addressables.LoadAssetAsync<T>()` | Assets |
| `WWW` class | `UnityWebRequest` | Networking |
| `Application.LoadLevel()` | `SceneManager.LoadScene()` | Scene |
| Legacy Animation component | Animator Controller | Animation |
| Legacy Particle System | Visual Effect Graph | VFX |
| `ComponentSystem` (old ECS) | `ISystem` (unmanaged) | DOTS/ECS |

### Cross-Cutting Constraints

- **Verification-driven development** — write tests first for gameplay systems; UI changes verified with screenshots — source: coding-standards.md
- **Gameplay values data-driven** — external config only, never hardcoded — source: coding-standards.md
- **All public methods unit-testable** — dependency injection over singletons — source: coding-standards.md
- **ADR-0001 static event pattern is the ONLY cross-system communication mechanism** — no Observer pattern alternative, no direct references between systems in different layers — source: ADR-0001
- **Hot-path code must produce zero GC allocation** — verified by profiler before merge — source: ADR-0001, ADR-0012
- **Snapshot transitions at Time.timeScale=0 work correctly** — audio DSP uses real time, independent of timescale — source: ADR-0013 (verified)
- **Session state file must be updated after each significant milestone** — `production/session-state/active.md` — source: context-management.md
