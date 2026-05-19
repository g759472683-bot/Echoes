<p align="center">
  <h1 align="center">回响 (Echoes)</h1>
  <p align="center">
    触碰记忆，重写真相。
    <br />
    A hand-drawn 2D narrative exploration game built in Unity 6.3 LTS.
  </p>
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
  <a href="#"><img src="https://img.shields.io/badge/engine-Unity%206.3%20LTS-222324?logo=unity" alt="Unity 6.3 LTS"></a>
  <a href="#"><img src="https://img.shields.io/badge/language-C%23-239120?logo=csharp" alt="C#"></a>
  <a href="#"><img src="https://img.shields.io/badge/stage-Pre--Production-orange" alt="Pre-Production"></a>
  <a href="#"><img src="https://img.shields.io/badge/systems-19%2F19%20MVP-brightgreen" alt="19/19 MVP Systems"></a>
  <a href="#"><img src="https://img.shields.io/badge/tests-79%20files-blueviolet" alt="79 Test Files"></a>
</p>

---

> 你是一个游魂，漂浮在自己活过的一生中。记忆像散落的画卷碎片——触碰、选择、重写它们。
> 每一次回溯，碎片重新排列，真相也随之改变。有些结局，藏在你从未踏足的关联里。

**Echoes** is a hand-drawn 2D narrative exploration game. You drift through memory fragments as a wandering spirit — touching objects in painted scrolls rewrites the memories themselves. There is no "correct" ending, only different truths.

| | |
|---|---|
| **Genre** | Narrative Exploration / Memory Puzzle |
| **Platform** | PC (Steam / Epic) |
| **Engine** | Unity 6.3 LTS (URP 2D Renderer) |
| **Language** | C# |
| **Stage** | Pre-Production |
| **MVP Scope** | 2 chapters, ~30 memory fragments, 1 hidden ending |
| **Full Vision** | 4 chapters, 60–100 fragments, 3 hidden endings |

---

## Design Pillars

1. **Choice is Rewriting** — Choices don't pick a path; they change the memory *itself*
2. **Imperfection is Power** — No "perfect" ending exists. The most moving endings are the hardest to reach
3. **A Web, Not a Book** — Fragments connect through emotional association, not chronological order
4. **Breathing Scrolls** — Every frame should be a painting you could hang on a wall

### Inspiration

*What Remains of Edith Finch* · *Disco Elysium* · *NieR: Automata*
Chinese ink wash painting · Wong Kar-wai films · Proust

---

## Architecture

```
┌──────────────────────────────────────────┐
│  PRESENTATION                            │
│  Micro-Animation · HUD · Feedback        │
│  Main Menu · Audio                       │
├──────────────────────────────────────────┤
│  FEATURE                                 │
│  Emotional Tags · Scroll Interaction     │
│  Change Tracking · Web Association       │
│  Multi-Ending · Chapter Management       │
│  Cross-Chapter State                     │
├──────────────────────────────────────────┤
│  CORE                                    │
│  Input System · Memory Fragment Model    │
│  UI Framework                            │
├──────────────────────────────────────────┤
│  FOUNDATION                              │
│  Data Management · Scene Management      │
│  Save/Load · Localization                │
└──────────────────────────────────────────┘
```

**19 systems** across 4 layers, governed by **18 Architecture Decision Records** (ADR-0001 through ADR-0018).

### Key Systems

| System | What It Does |
|---|---|
| **Web Association Engine** | Memory fragments connect through emotional tag similarity — real-time candidate ranking |
| **Change Tracking** | Overlay pattern rewrites fragment content without mutating source data |
| **Scroll Interaction** | Physics-based 2D interaction: touch, drag, hover, examine |
| **Multi-Ending** | 2–5 endings per chapter; hidden endings require cross-chapter chains |
| **Chapter Manager** | 3-state machine, two-part completion detection, replay semantics |

---

## Project Structure

```
├── assets/Scripts/           # C# code (Unity assemblies)
│   ├── Core/                 # Echoes.Core — 114 source files
│   └── Editor/               # Echoes.Editor — 5 source files
├── assets/UI/                # UI Toolkit Theme.uss
├── assets/uss/               # UI stylesheets
├── assets/uxml/              # UI layout files
├── design/gdd/               # 22 game design documents
├── docs/architecture/        # 18 ADRs + architecture spec + traceability
├── tests/                    # 79 test files
│   ├── unit/                 # 48 unit test files
│   └── integration/          # 21 integration test files
├── production/epics/         # 19 epics with 79 stories
└── .github/workflows/        # CI (Unity Test Framework)
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Engine | Unity 6.3 LTS (`6000.3.45f1`) |
| Rendering | URP 2D Renderer |
| UI | UI Toolkit (UXML + USS) |
| Input | Unity Input System 1.13 |
| Assets | Addressables 2.3 |
| Localization | Unity Localization 1.5 |
| Testing | Unity Test Framework 1.4 |
| CI | GitHub Actions (`game-ci/unity-test-runner@v4`) |

---

## Getting Started

### Prerequisites

- **Unity 6.3 LTS** (6000.3.x) with URP 2D template
- Git

### Clone & Open

```bash
git clone https://github.com/g759472683-bot/Echoes.git
cd Echoes
```

1. Open Unity Hub → **Add Project from Disk** → select the cloned directory
2. Unity imports packages and compiles `Echoes.Core` + `Echoes.Editor` assemblies
3. Open `Window → Asset Management → Addressables → Groups` to verify

### Run Tests

```bash
# GUI: Window → General → Test Runner → Run All
# CLI:
unity -runTests -projectPath . -testPlatform PlayMode
```

---

## Development Workflow

This project uses the Claude Code Game Studios framework — 49 specialized AI agents coordinated through skills and phase gates.

| Skill | Purpose |
|---|---|
| `/story-readiness [path]` | Validate a story before starting |
| `/dev-story [path]` | Implement a story with the right programmer agent |
| `/code-review [files]` | Review against ADRs, standards, and SOLID |
| `/story-done [path]` | Verify acceptance criteria and close |
| `/gate-check [phase]` | Validate phase-gate readiness |

See `CLAUDE.md` and `production/epics/` for the full sprint structure.

---

## Credits

Built with the [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios) AI agent framework.

---

<p align="center">
  <i>每一帧画面都应是一幅可以挂上墙的画。<br>
  Every frame should be a painting you could hang on a wall.</i>
</p>
