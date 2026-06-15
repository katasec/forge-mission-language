# Phase 16 — FML → MCL Full Rename

**Status:** Done

## Goal

Rename all FML (Forge Mission Language) surface identifiers to MCL (Mission Control Language), including C#
class names. The C# compiler guided the process — every broken reference was a compile error.

## Motivation

"FML" has a strong negative internet connotation. "MCL" (Mission Control Language) is clean,
unclaimed, and more accurately captures the product identity — a language for controlling reasoning
missions, not just a scripting format.

## What Changed

| What | Before | After |
|------|--------|-------|
| CLI binary | `fml` | `mcl` |
| Mission file extension | `.fml` | `.mcl` |
| Mission files in `missions/` | `mission.fml` | `mission.mcl` |
| Lock file | `fms.lock` | `mcl.lock` |
| CLI `AssemblyName` in `.csproj` | `fml` | `mcl` |
| Grammar file | `FmsGrammar.g4` | `MclGrammar.g4` |
| Generated ANTLR classes | `FmsGrammarParser`, `FmsGrammarLexer` etc. | `MclGrammarParser`, `MclGrammarLexer` etc. |
| Parser class | `FmsParser` | `MclParser` |
| AST builder class | `FmsAstBuilder` | `MclAstBuilder` |
| Error class | `FmsException` / `FmsErrorCode` | `MclException` / `MclErrorCode` |
| CLI type alias | `FmsProgram` | `MclProgram` |
| CLI default arg | `"mission.fms"` | `"mission.mcl"` |
| All docs and README references | `fms` / FMS / Forge Mission Script | `mcl` / MCL / Mission Control Language |
| C# namespaces | `ForgeMission.*` | unchanged |

## Verification

`make demo` passes clean using the `mcl` binary against `.mcl` mission files.
