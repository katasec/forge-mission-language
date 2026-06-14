# Phase 3 — Expert Loader

## Goal

Resolve expert names from the AST to markdown files on disk. Parse YAML frontmatter. Validate that every expert referenced in a mission exists before execution begins.

## Completion condition

All unit tests pass. Loader correctly resolves experts from fixture files, parses frontmatter, and produces clear errors for missing or malformed experts — before any LLM call is made.

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Define `ExpertDefinition` model (`Name`, `Input`, `Output`, `SystemPrompt`) | Done |
| 2 | Implement `ExpertLoader` — scans `experts/` directory for `.md` files | Done |
| 3 | Implement YAML frontmatter parser using `YamlDotNet` (`name`, `input`, `output`) | Done |
| 4 | Implement body extraction — content below the frontmatter block becomes `SystemPrompt` | Done |
| 5 | Implement `ExpertLoader.LoadAll()` — returns `Dictionary<string, ExpertDefinition>` | Done |
| 6 | Implement `ExpertLoader.Validate(Program ast)` — checks all referenced experts exist | Done |
| 7 | Unit test: loads a valid expert markdown file correctly | Done |
| 8 | Unit test: parses `name`, `input`, `output` from frontmatter | Done |
| 9 | Unit test: body below frontmatter becomes `SystemPrompt` | Done |
| 10 | Unit test: missing expert referenced in mission produces clear error | Done |
| 11 | Unit test: missing frontmatter field produces clear error | Done |
| 12 | Unit test: directory with multiple experts loads all correctly | Done |

## Result

8/8 expert loader tests passing. 16/16 total tests passing (parser tests remain green).

## Notes

- `Validate` is static — takes both the AST and the loaded experts dictionary so it can be called without a loader instance
- Experts declared inline in the `.fml` file (via `expert X = ...`) do not require a markdown file — only leaf-level expert names do
- Tests use `IDisposable` with a temp directory per test run — no fixture files checked into the repo
