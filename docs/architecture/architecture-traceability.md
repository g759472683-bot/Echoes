# Architecture Traceability Index — 回响 (Echoes)

**Date**: 2026-05-19
**Based on**: `/architecture-review full` — `docs/architecture/architecture-review-2026-05-12.md`
**TR Registry**: `docs/architecture/tr-registry.yaml` (92 TR-IDs, 19 systems)
**Last Update**: Added ADR-0016, ADR-0017, ADR-0018 — all 19 systems now have dedicated ADR coverage

## Traceability Matrix (Per-System)

### Foundation Layer

| # | System | GDD | Dedicated ADR | Also Covered By | TR-IDs | Status |
|---|--------|-----|---------------|-----------------|--------|--------|
| 1 | Input System | input-system.md | ADR-0005 | ADR-0001 | TR-input-system-001~006 | ✅ |
| 2 | Data Management | data-management.md | ADR-0002 | — | TR-data-management-001~005 | ✅ |
| 3 | Audio System | audio-system.md | ADR-0013 | ADR-0002 | TR-audio-system-001~005 | ✅ |
| 4 | Localization | localization.md | ADR-0015 | ADR-0001 | TR-localization-001~005 | ✅ |
| 5 | UI Framework | ui-framework.md | ADR-0006 | ADR-0001, 0005 | TR-ui-framework-001~006 | ✅ |
| 6 | Scene Management | scene-management.md | ADR-0004 | ADR-0001, 0002 | TR-scene-management-001~006 | ✅ |
| 7 | Save/Load | save-load-system.md | ADR-0003 | ADR-0007 | TR-save-load-system-001~005 | ✅ |

**Foundation**: 7/7 systems have dedicated ADRs. Zero gaps.

### Gameplay Layer

| # | System | GDD | Dedicated ADR | Also Covered By | TR-IDs | Status |
|---|--------|-----|---------------|-----------------|--------|--------|
| 8 | Memory Fragment Data Model | memory-fragment-data-model.md | ADR-0007 | ADR-0008 | TR-memory-fragment-001~005 | ✅ |
| 9 | Micro-Animation | micro-animation-system.md | ADR-0012 | — | TR-micro-animation-001~004 | ✅ |
| 10 | Emotional Tag System | emotional-tag-system.md | ADR-0016 | ADR-0009, 0010, 0012 | TR-emotional-tag-001~005 | ✅ |
| 11 | Scroll Interaction | scroll-interaction-system.md | ADR-0017 | ADR-0001, 0004, 0005, 0014 | TR-scroll-interaction-001~005 | ✅ |
| 12 | Memory Change Tracking | memory-change-tracking.md | ADR-0007 | ADR-0001, 0003, 0008 | TR-memory-change-tracking-001~005 | ✅ |
| 13 | Web Association Engine | web-association-engine.md | ADR-0009 | ADR-0008 | TR-web-association-001~005 | ✅ |
| 14 | Multi-Ending System | multi-ending-system.md | ADR-0010 | ADR-0003, 0008 | TR-multi-ending-001~005 | ✅ |

**Gameplay**: 7/7 systems have dedicated ADRs. Zero gaps.

### Progression Layer

| # | System | GDD | Dedicated ADR | Also Covered By | TR-IDs | Status |
|---|--------|-----|---------------|-----------------|--------|--------|
| 15 | Chapter Management | chapter-management.md | ADR-0018 | ADR-0001, 0007, 0010, 0011 | TR-chapter-management-001~005 | ✅ |
| 16 | Cross-Chapter State | cross-chapter-state-tracking.md | ADR-0011 | ADR-0003 | TR-cross-chapter-state-001~004 | ✅ |

**Progression**: 2/2 systems have dedicated ADRs. Zero gaps.

### UI Layer (MVP only)

| # | System | GDD | Dedicated ADR | Also Covered By | TR-IDs | Status |
|---|--------|-----|---------------|-----------------|--------|--------|
| 17 | In-Game HUD | in-game-hud.md | — (piecemeal) | ADR-0001, 0006, 0009, 0015 | TR-in-game-hud-001~006 | ⚠️ No dedicated ADR (acceptable) |
| 18 | Interaction Feedback | interaction-feedback.md | ADR-0014 | ADR-0001, 0004, 0012, 0013 | TR-interaction-feedback-001~005 | ✅ |
| 19 | Main Menu | main-menu.md | — (piecemeal) | ADR-0006, 0013, 0015 | TR-main-menu-001~006 | ⚠️ No dedicated ADR (acceptable) |

**UI**: 1/3 MVP systems have dedicated ADRs. Systems #17 (HUD) and #19 (Main Menu) are Presentation-layer — piecemeal coverage by cross-cutting ADRs is acceptable.

---

## ADR → System Coverage Map

| ADR | Title | Status | Layer | Covers System(s) | Risk |
|-----|-------|--------|-------|-------------------|------|
| ADR-0001 | Event Bus Architecture | Accepted | Foundation | Cross-cutting (all event communication) | LOW |
| ADR-0002 | Data Management Strategy | Accepted | Foundation | #2 Data Management | MEDIUM |
| ADR-0003 | Save Serialization Strategy | Accepted | Foundation | #7 Save/Load | LOW |
| ADR-0004 | Scene Management | Accepted | Foundation | #6 Scene Management | LOW |
| ADR-0005 | Input System Architecture | Accepted | Foundation | #1 Input System | HIGH |
| ADR-0006 | UI Framework | Accepted | Foundation | #5 UI Framework | HIGH |
| ADR-0007 | SO Immutable Overlay Pattern | Accepted | Core | #8 Memory Fragment, #12 Change Tracking | MEDIUM |
| ADR-0008 | Condition Group Engine | Accepted | Feature | Supports #7, #8, #12, #13 | LOW |
| ADR-0009 | Web Association Engine | Accepted | Feature | #13 Web Association | LOW |
| ADR-0010 | Multi-Ending Algorithm | Accepted | Feature | #14 Multi-Ending | LOW |
| ADR-0011 | Cross-Chapter State | Accepted | Feature | #16 Cross-Chapter State | LOW |
| ADR-0012 | Micro-Animation System | Accepted | Presentation | #9 Micro-Animation | HIGH |
| ADR-0013 | Audio Architecture | Accepted | Presentation | #3 Audio System | MEDIUM |
| ADR-0014 | Interaction Feedback Mapping | Accepted | Presentation | #18 Interaction Feedback | LOW |
| ADR-0015 | Localization Strategy | Accepted | Foundation | #4 Localization | MEDIUM |
| ADR-0016 | Emotional Tag System | Accepted | Core | #10 Emotional Tag System | LOW |
| ADR-0017 | Scroll Interaction System | Accepted | Core | #11 Scroll Interaction | MEDIUM |
| ADR-0018 | Chapter Management | Accepted | Core | #15 Chapter Management | LOW |

---

## Dependency Graph (Implementation Order)

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

Core (continued):
  9. ADR-0016 (Emotional Tag System) — requires ADR-0007
 10. ADR-0017 (Scroll Interaction) — requires ADR-0001, ADR-0004, ADR-0005, ADR-0007
 11. ADR-0018 (Chapter Management) — requires ADR-0004, ADR-0002, ADR-0007, ADR-0003

Feature (depends on Core):
 12. ADR-0008 (Condition Group) — requires ADR-0007
 13. ADR-0003 (Save System) — requires ADR-0007
 14. ADR-0011 (Cross-Chapter State) — requires ADR-0007, ADR-0003, ADR-0018

Feature (depends on Feature):
 15. ADR-0010 (Multi-Ending) — requires ADR-0008, ADR-0011, ADR-0018
 16. ADR-0009 (Web Association) — requires ADR-0002, ADR-0008, ADR-0010, ADR-0016
 17. ADR-0004 (Scene Management) — requires ADR-0001, ADR-0002

Presentation:
 18. ADR-0014 (Interaction Feedback) — requires ADR-0001, ADR-0012, ADR-0013, ADR-0017
```

No dependency cycles detected. All chains are directed acyclic.

---

## GDD Revision Flags

All 5 GDD revision flags resolved on 2026-05-19:

| GDD | Issue | Severity | Status |
|-----|-------|----------|--------|
| scroll-interaction-system.md | Missing 10 public static event definitions on InteractionManager | Blocking (B1) | ✅ 已修复 — OnHoverEnter/OnHoverExit 签名从 `Action<InteractiveObject>` 改为 `Action<string>`（匹配实现） |
| scene-management.md | Missing OnFragmentTransitionStarted event; missing #12 as downstream consumer | Blocking (B2) + Warning (W3) | ✅ 已验证 — 两项均已存在于 GDD 中，添加了 Revision Notes |
| memory-change-tracking.md | OnFragmentTransitioned parameter signature mismatch (1 vs 2 params) | Warning (W1) | ✅ 已修复 — AC 中的单参数调用改为 `OnFragmentTransitioned(chapterKey, "frag_D")` |
| audio-system.md | Incorrect downstream: UI Framework (#5) listed as PlaySFX consumer | Warning (W2) | ✅ 已验证 — GDD 已正确记录 #5 不直接调用 PlaySFX，添加了 Revision Notes |
| save-load-system.md | Load path calls SceneManager directly — must use ChapterManager.LoadAndRestore | Warning (W4) | ✅ 已验证 — GDD 已正确记录 LoadAndRestore 路径，添加了 Revision Notes |

---

## Coverage Summary

| Layer | Systems | Dedicated ADRs | Piecemeal | Gap |
|-------|---------|----------------|-----------|-----|
| Foundation | 7 | 7 | 0 | 0 |
| Gameplay | 7 | 7 | 0 | 0 |
| Progression | 2 | 2 | 0 | 0 |
| UI (MVP) | 3 | 1 | 2 (#17, #19) | 0 |
| **Total** | **19** | **17** | **2** | **0** |

**All 19 MVP systems have ADR coverage. 17/19 have dedicated ADRs.** #17 (In-Game HUD) and #19 (Main Menu) are Presentation-layer — piecemeal coverage by cross-cutting ADRs (ADR-0006, ADR-0013, ADR-0015) is acceptable for UI systems.

### TR-ID Summary

- **Total TR-IDs**: 92 (in `tr-registry.yaml`)
- **Covered (explicit ADR reference)**: 86
- **Partial (implicit/multi-ADR)**: 6
- **Gaps**: 0

---

## Related

- `docs/architecture/architecture-review-2026-05-12.md` — Full review report (CONCERNS verdict)
- `docs/architecture/architecture.md` — Master architecture document
- `docs/architecture/tr-registry.yaml` — Requirement ID registry (92 entries)
- `docs/architecture/adr-0001~0018` — Architecture Decision Records
