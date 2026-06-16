# Phase 20 — Parser Project Extraction

**Status:** Done  
**Depends on:** Phase 11 (OCI), Phase 18 (Drop MAF)

## Goal

Extract `ForgeMission.Core/Parser` into a standalone `ForgeMission.Parser` project so the compiler (grammar + AST) has no dependency on AI or YAML packages. Enables reuse in future tooling (language server, IDE plugins) without pulling in the full runtime.

## What changed

- New project: `src/ForgeMission.Parser/` — only `Antlr4.Runtime.Standard` dependency
- Namespace renamed from `ForgeMission.Core.Parser` → `ForgeMission.Parser` across all files (hand-written + generated)
- `ForgeMission.Core` removes `Antlr4.Runtime.Standard`, gains a `ProjectReference` to `ForgeMission.Parser`
- All consumers updated: `using ForgeMission.Parser`

## Dependency graph after

```
ForgeMission.Parser   (ANTLR4 only)
       ↑
ForgeMission.Core     (MEA, YamlDotNet, Parser)
       ↑
ForgeMission.Cli      (System.CommandLine, OAI, OCI, OaiServer, Core)
ForgeMission.Tests    (xUnit, Core)
```
