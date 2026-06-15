# Phase 16 — FML → FMS Surface Rename

**Status:** Pending

## Goal

Rename the surface-visible identifiers from FML (Forge Mission Language) to FMS (Forge Mission
Script). C# namespaces (`ForgeMission.*`) are unchanged — this is a surface-only rename.

## Motivation

"FML" has a strong negative internet connotation. "FMS" is clean, unclaimed, and more accurately
describes the artefact (a script, not just a language).

## Scope

| What | Before | After |
|------|--------|-------|
| CLI binary | `fml` | `fms` |
| Mission file extension | `.fml` | `.fms` |
| Mission files in `missions/` | `mission.fml` | `mission.fms` |
| Lock file | `fms.lock` | unchanged |
| CLI `AssemblyName` in `.csproj` | `fml` | `fms` |
| All docs and README references | `fml` | `fms` |
| Grammar file | `FmlGrammar.g4` | `FmsGrammar.g4` (generated files renamed accordingly) |
| C# namespaces | `ForgeMission.*` | unchanged |
| C# class names | `FmlParser`, `FmlAstBuilder` etc. | unchanged |

## Changes Required

1. `src/ForgeMission.Cli/ForgeMission.Cli.csproj` — set `<AssemblyName>fms</AssemblyName>`
2. Rename `FmlGrammar.g4` → `FmsGrammar.g4`; regenerate parser; update `FmlParser.cs` reference
3. Rename all `missions/*/mission.fml` → `mission.fms`
4. Update `Program.cs` default mission arg from `"mission.fml"` → `"mission.fms"`
5. Find-replace in `README.md`, `docs/` — `fml` → `fms` where it refers to the binary/extension
6. Update `Makefile` demo target
7. Update `ResolveMission` default in CLI (`"mission.fml"` → `"mission.fms"`)

## Not In Scope

- C# namespace changes (`ForgeMission.*` stays)
- C# class/file renames (`FmlParser`, `FmlAstBuilder`, `FmlGrammar*` generated files can keep names internally)
- Any runtime behaviour changes

## Verification

After rename: `make demo` must pass clean using `fms` binary against `.fms` mission files.
