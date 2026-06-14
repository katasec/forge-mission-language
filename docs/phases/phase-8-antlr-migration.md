# Phase 8 — ANTLR Migration

## Goal

Replace the hand-rolled lexer, token stream, and recursive-descent parser with an ANTLR4-generated
parser. No feature changes — the language accepted and the AST produced must be identical to today.
The existing 8 parser tests are the regression gate: they must all pass unchanged.

## Why now

Phase 9 extends the grammar with `let` bindings, mission parameters, and `with` clauses. Each new
feature currently requires touching four files (Lexer, TokenStream, FmlParser, AST). With ANTLR,
grammar changes are one `.g4` edit; the lexer and parser are regenerated automatically. Migrating
before extending eliminates the risk of compounding hand-rolled complexity.

## Completion condition

All 21 existing tests pass. Hand-rolled `Lexer.cs`, `TokenStream.cs`, and `FmlParser.cs` are
removed. `Fml.g4` is the authoritative grammar — `docs/design/language.md` BNF section updated to
reference it.

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Add `Antlr4.Runtime.Standard` and `Antlr4BuildTasks` NuGet packages to `ForgeMission.Core` | Not Started |
| 2 | Write `Fml.g4` — grammar covering the current language (mission, expert, `\|>`, PascalCase identifiers) | Not Started |
| 3 | Configure ANTLR code generation via `Antlr4BuildTasks` (generates lexer + parser at build time) | Not Started |
| 4 | Implement `FmlAstBuilder` — walks the ANTLR parse tree, produces the same AST records (`Program`, `MissionDeclaration`, `ExpertDeclaration`, `Pipeline`) | Not Started |
| 5 | Update `FmlParser.Parse(string)` entry point to call ANTLR-generated parser + `FmlAstBuilder` | Not Started |
| 6 | Verify all 21 existing tests pass with zero changes to test code | Not Started |
| 7 | Delete `Lexer.cs`, `TokenStream.cs`, hand-rolled `FmlParser.cs` | Not Started |
| 8 | Update `docs/design/language.md` — replace BNF section with reference to `Fml.g4` as authoritative grammar | Not Started |

## Key decisions

- **`Antlr4BuildTasks`** (NuGet) runs the ANTLR tool at `dotnet build` time — no Java or separate
  toolchain step required. Generated files appear under `obj/` and are not checked in.
- The public `FmlParser.Parse(string)` signature does not change — callers (CLI, tests) are
  unaffected.
- `ParseException` is preserved; ANTLR parse errors are translated to it so error message format
  stays consistent.

## Notes

- ANTLR4 C# runtime: `Antlr4.Runtime.Standard`
- Build-time codegen: `Antlr4BuildTasks` (avoids Java dependency — tool ships as a .NET global tool)
- Generated output: `FmlLexer.cs`, `FmlParser.cs` (ANTLR-generated, not hand-rolled), `FmlListener.cs`, `FmlVisitor.cs`
- Use the visitor pattern (`FmlBaseVisitor<T>`) for AST construction — cleaner than the listener for tree-to-object mapping
