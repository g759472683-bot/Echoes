# Directory Structure

```text
/
├── CLAUDE.md                    # Master configuration
├── .claude/                     # Agent definitions, skills, hooks, rules, docs
├── assets/                      # Unity Assets/ directory
│   ├── Scripts/                 # Game source code (Core, Editor assemblies)
│   │   ├── Core/                # Core assembly (Echoes.Core)
│   │   └── Editor/              # Editor assembly (Echoes.Editor)
│   ├── UI/                      # UI assets (Theme.uss, etc.)
│   ├── uss/                     # UI stylesheets
│   └── uxml/                    # UI layout files
├── design/                      # Game design documents (gdd, narrative, levels, balance)
├── docs/                        # Technical documentation (architecture, api, postmortems)
│   └── engine-reference/        # Curated engine API snapshots (version-pinned)
├── tests/                       # Test suites (unit, integration, performance, playtest)
├── tools/                       # Build and pipeline tools (ci, build, asset-pipeline)
├── prototypes/                  # Throwaway prototypes (isolated from assets/)
└── production/                  # Production management (sprints, milestones, releases)
    ├── session-state/           # Ephemeral session state (active.md — gitignored)
    └── session-logs/            # Session audit trail (gitignored)
```
