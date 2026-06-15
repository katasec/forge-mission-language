# FML — Architecture

## Guiding principle

The direct IChatClient adapter is the only LLM integration point. It does not appear above the adapter layer. The parser, AST, pipeline runner, and CLI know nothing about the underlying provider.

## Components

### 1. Parser — `ForgeMission.Core/Parser/`

Pure C#, zero external dependencies. Takes a `.mcl` file as a string, produces an AST.

```
Lexer → TokenStream → Parser → AST
```

**AST nodes:**

| Node | Description |
|------|-------------|
| `Program` | Root node — list of declarations |
| `MissionDeclaration` | `mission Name = pipeline` |
| `ExpertDeclaration` | `expert Name = pipeline` |
| `Pipeline` | Ordered list of identifiers |
| `Identifier` | PascalCase name |

**Testable in isolation:** unit tests with string inputs only, no disk, no LLM.

---

### 2. Expert Loader — `ForgeMission.Core/Experts/`

Resolves expert names from the AST to markdown files on disk. Parses YAML frontmatter (`name`, `input`, `output`). Validates that every expert referenced in a mission exists before execution begins. Fails fast with a clear error if anything is missing.

**Testable in isolation:** unit tests with fixture markdown files.

---

### 3. Pipeline Runner — `ForgeMission.Core/Runtime/`

Orchestration loop. Walks the pipeline in order, calls `IExpertRunner` for each step, passes the output of step N as input to step N+1, writes each step's output to `runs/<mission-name>/NN-<ExpertName>.md`.

```csharp
interface IExpertRunner
{
    Task<string> RunAsync(ExpertDefinition expert, string context, CancellationToken ct);
}
```

Knows nothing about MAF. Depends only on `IExpertRunner`.

**Testable in isolation:** unit tests with a stub `IExpertRunner` returning canned strings.

---

### 4. Direct IChatClient Adapter — `ForgeMission.Core/Adapters/DirectExpertRunner.cs`

The single place provider interaction exists in the codebase. Implements `IExpertRunner`. Builds a `[System, User]` message list, calls `IChatClient.CompleteAsync()` (or `CompleteStreamingAsync()` for streaming), deserialises the JSON response into `StepEnvelope`.

One file. Swappable without touching anything else. No MAF dependency.

**Testable:** integration test against a real LLM.

---

### 5. CLI — `ForgeMission.Cli/`

Thin entry point. Parses arguments, wires up dependencies via DI, delegates to Core. No business logic.

**Commands:**

| Command | Description |
|---------|-------------|
| `forge init [mission.mcl]` | Resolve expert sources and generate mcl.lock |
| `forge run [mission.mcl]` | Run a mission (self-contained — no input file required) |
| `forge validate [mission.mcl]` | Validate all experts exist and pipeline is well-formed |
| `forge list experts` | List available experts |
| `forge expert init <Name>` | Scaffold a new expert directory |

---

## Dependency flow

```
CLI
 └→ Pipeline Runner
      └→ Parser           (produces AST)
      └→ Expert Loader    (resolves markdown)
      └→ IExpertRunner
           └→ Direct IChatClient Adapter (hidden — only impl of IExpertRunner)
```

Strictly one direction. Nothing flows upward.

## Project references

```
ForgeMission.Cli
  └→ ForgeMission.Core

ForgeMission.Core
  └→ Microsoft.Extensions.AI   (IChatClient abstraction — adapter only)
```

## Output structure

```
runs/
  <mission-name>/
    01-<ExpertName>.md
    02-<ExpertName>.md
    ...
    final.md
```

Each step output is written before the next step begins, so a partial run is always inspectable.
