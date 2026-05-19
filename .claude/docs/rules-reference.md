# Path-Specific Rules

Rules in `.claude/rules/` are automatically enforced when editing files in matching paths:

| Rule File | Path Pattern | Enforces |
| ---- | ---- | ---- |
| `gameplay-code.md` | `assets/Scripts/Gameplay/**` | Data-driven values, delta time, no UI references |
| `engine-code.md` | `assets/Scripts/Core/**` | Zero allocs in hot paths, thread safety, API stability |
| `ai-code.md` | `assets/Scripts/AI/**` | Performance budgets, debuggability, data-driven params |
| `network-code.md` | `assets/Scripts/Networking/**` | Server-authoritative, versioned messages, security |
| `ui-code.md` | `assets/Scripts/UI/**` | No game state ownership, localization-ready, accessibility |
| `design-docs.md` | `design/gdd/**` | Required 8 sections, formula format, edge cases |
| `narrative.md` | `design/narrative/**` | Lore consistency, character voice, canon levels |
| `data-files.md` | `assets/data/**` | JSON validity, naming conventions, schema rules |
| `test-standards.md` | `tests/**` | Test naming, coverage requirements, fixture patterns |
| `prototype-code.md` | `prototypes/**` | Relaxed standards, README required, hypothesis documented |
| `shader-code.md` | `assets/shaders/**` | Naming conventions, performance targets, cross-platform rules |
