# Phase 26 — Tooling Foundation

## Status: Todo (after Phase 25)

## Motivation

Once the grammar stabilises in Phase 25, the language is ready for developer tooling. Three tiers build on each other:

```
Syntax highlighting    — token-level, no semantic understanding needed
Code completion        — needs AST + symbol awareness  
Go-to-definition       — needs AST positions + file resolution
```

The current design is well-positioned for all three. The grammar is clean, no whitespace sensitivity, no context-sensitive keywords, and the `UPPER_ID`/`LOWER_ID` distinction already encodes what is valid at every cursor position. The only structural gap is source positions on AST nodes — everything else is a mechanical derivation from what already exists.

## Spokes

| Spoke | Description | Status |
|-------|-------------|--------|
| [Spoke 1 — Source Positions](phase-26-spoke-1-source-positions.md) | Add `SourceSpan` to every AST node — prerequisite for all editor tooling AND the error message underline renderer (see Decision 1 in Phase 25 pre-flight) | Todo |
| [Spoke 2 — TextMate Grammar](phase-26-spoke-2-textmate.md) | Syntax highlighting for VS Code, GitHub, Sublime — derivable from ANTLR grammar | Todo |
| [Spoke 3 — Tree-sitter Grammar](phase-26-spoke-3-tree-sitter.md) | Incremental parsing, error-tolerant — used by Neovim, GitHub, VS Code | Todo |
| [Spoke 4 — LSP Server](phase-26-spoke-4-lsp.md) | Completion, hover, go-to-definition, diagnostics — workspace-aware | Todo |

## Execution order

Spokes are sequentially dependent:

```
Spoke 1 (source positions) → Spoke 2 (TextMate) → Spoke 3 (Tree-sitter) → Spoke 4 (LSP)
```

Spoke 2 can ship independently as soon as Spoke 1 is done. Spoke 3 and 4 can be deferred until there are external users — Spoke 1 and 2 deliver immediate value.

## What does not change

The language grammar is not modified in this phase. Phase 26 is purely additive — it builds tooling on top of a stable grammar without touching the runtime or CLI.
