# Phase 32 — Safe Execution as a First-Class Primitive (`kind: exec`)

> **Status: Design**
> **Priority: Higher than Phase 31 (Forge Runtime Platform)**
> **Depends on:** Phase 22a (kind dispatch infrastructure), Phase 22b (ONNX — establishes multi-artifact expert packaging pattern)
> **Purpose:** Add deterministic, out-of-process execution as a first-class expert kind.
> Completes the heterogeneous intelligence stack and enables the "reasoning, measuring,
> verifying" pattern where experts generate evidence before drawing conclusions.

---

## Motivation

MCL's current intelligence stack:

| Kind | Model | Execution |
|---|---|---|
| `llm` | Probabilistic | In-process, `IChatClient` |
| `rule` | Deterministic | In-process, expression evaluator |
| `onnx` | Deterministic | In-process, ONNX runtime |
| `http` | Deterministic | Remote, pre-deployed service |
| `json_extract` | Bridge | In-process |

There is a gap between `rule` (in-process, simple expressions) and `http` (remote,
pre-deployed service). Real-world deterministic pipelines — feature engineering,
static analysis, SQL queries, graph algorithms, custom scoring tools — need to run
code packaged *with* the expert, out-of-process, without requiring a separately
deployed service.

### The epistemological problem

LLMs confabulate about things that can be measured. A code review expert inventing
plausible-sounding vulnerability claims is a different class of problem from one
reasoning over actual output from `semgrep` or `ast-grep`.

The current model:

```
Repository
    |
LLM reasons
    |
Answer  (probabilistic, unverified)
```

With `kind: exec`:

```
Repository
    |
LLM generates analysis plan
    |
exec: run static analysis (semgrep, ast-grep, custom scanner)
    |
json_extract: structured findings
    |
LLM reasons over actual evidence
    |
Answer grounded in measurement
```

Wherever a measurement can replace or ground a probabilistic inference, the result
is strictly more reliable. This is the neurosymbolic argument (Phase 28 `kind: rule`,
Gary Marcus) extended to arbitrary deterministic computation.

### Expert evolution

Today an expert is:

```
Knowledge + Methodology + Reasoning
```

With `kind: exec`, an expert can be:

```
Knowledge + Methodology + Reasoning + Verification + Execution
```

An OCI-distributed expert package might contain:

```
experts/SecurityAuditor/
  expert.md
  analysis.py       ← static analysis script
  queries.sql       ← evidence-gathering queries
  graph.go          ← dependency graph analyser
```

MCL continues to orchestrate experts. The expert decides whether deterministic
execution is required internally.

---

## Language changes

### `kind: exec` in expert frontmatter

```markdown
---
name: StaticAnalyser
kind: exec
executable: ./analysis.py
runtime: python3
inputs: [repo_path, language, ruleset]
outputKey: findings
timeout: 30s
---

Runs static analysis against the target repository and returns structured findings.
```

| Field | Required | Description |
|---|---|---|
| `executable` | Yes | Path to script/binary, relative to expert directory. Or a system-installed tool name (no `./` prefix). |
| `runtime` | No | Interpreter/runtime: `python3`, `node`, `go run`, etc. Omit for native binaries. |
| `inputs` | Yes | Comma-separated context bag keys to pass as input JSON. |
| `outputKey` | Yes | Context bag key to write the result into. |
| `timeout` | No | Execution timeout. Default: `30s`. |

`kind` defaults to `llm` when absent — fully backward compatible. No existing experts
are affected.

### Kind dispatch extension

The static dispatch switch gains one new arm — AOT-safe by construction:

```csharp
IExpertRunner RunnerFor(string kind) => kind switch
{
    "llm"        => new DirectExpertRunner(chatClient),
    "onnx"       => new OnnxExpertRunner(),
    "http"       => new HttpExpertRunner(),
    "exec"       => new ExecExpertRunner(execBackend),
    _            => throw new ExpertLoadException($"Unknown expert kind '{kind}'")
};
```

---

## Input/output contract

### JSON stdin → JSON stdout

The runtime serialises the declared `inputs` as a JSON object and writes it to the
executable's stdin. The executable writes a JSON object to stdout. The runtime reads
stdout and writes the declared `outputKey` into the context bag.

```
forge runtime
    |
    | { "repo_path": "/src", "language": "go", "ruleset": "default" }  →  stdin
    |
    v
executable (analysis.py)
    |
    | { "findings": [...], "severity": "high", "count": 7 }  →  stdout
    |
forge runtime reads outputKey "findings" → context bag
```

**Why JSON stdin/stdout:** consistent with `json_extract` (the probabilistic/deterministic
bridge), consistent with Unix pipeline conventions, supports structured data without temp
files, and the format is already well-understood by Python/Go/Node tooling.

Stderr is captured separately and written to the step trace for debugging. It does not
affect the pipeline result.

### Context bag integration

`kind: exec` reads from and writes to the same `Dictionary<string, object>` context bag
used by all other kinds. The serialiser maps `string` → JSON string, `double` → JSON
number, and arrays/objects are passed through if already structured (from prior
`json_extract` steps).

---

## Execution backends

### `IExecBackend` abstraction

```csharp
interface IExecBackend
{
    Task<ExecResult> RunAsync(ExecRequest request, CancellationToken ct);
}

record ExecRequest(
    string Executable,
    string? Runtime,
    string InputJson,
    string WorkingDirectory,
    TimeSpan Timeout
);

record ExecResult(
    string OutputJson,
    string Stderr,
    int ExitCode,
    ExecStatus Status   // Success | Timeout | Error
);
```

Backend is an operator decision, configured in `forge.toml`. The expert author
declares only `kind: exec`. Portability is preserved — the same expert runs under
any backend.

```toml
[execution]
backend = "process"   # process | wasm | hyperlight
```

### Backend 1 — `process` (local development)

- `Process.Start` with `UseShellExecute = false`
- Explicit executable + argument list — never a shell string
- stdin/stdout/stderr pipes captured
- No isolation: filesystem, network, and subprocesses are unrestricted
- **Intended use:** local development, trusted first-party scripts only

**Security note — must be documented clearly:** The process backend provides no
sandbox. An executable can read/write any filesystem path, make network calls, and
spawn child processes. This is explicitly a trusted-code-only mode. For shared or
production environments, use the `wasm` or `hyperlight` backend.

AOT note: `Process.Start` is AOT-safe. No reflection involved.

### Backend 2 — `wasm` (sandboxed, general production)

- Execute WASM/WASI modules via Wasmtime (or WAMR as alternative)
- WASI capability-based security: filesystem and network access only if explicitly
  granted in the backend config
- No VT-x requirement — runs on any host
- Supports Python (Pyodide/wasm32-wasi), Go (WASM target), Rust, C
- Can run in-process (lower overhead) or as a subprocess

**Intended use:** general production environments; third-party expert scripts; shared
platform deployments where process isolation is required without VT-x.

### Backend 3 — `hyperlight` (hardware VM isolation)

- Hardware-backed micro-VM isolation via Microsoft Hyperlight
- Disposable execution environment per invocation
- Appropriate for AI-generated analysis scripts or fully untrusted code
- Requires VT-x/AMD-V (not available in all cloud environments — nested virtualisation
  varies by provider)
- Guest runtime support depends on Hyperlight's current language matrix

**Intended use:** maximum isolation scenarios; AI-generated code execution; multi-tenant
Forge Runtime deployments (Phase 31) where tenant code must not cross boundaries.

---

## Artifact packaging

### Expert directory model

Today an expert is one file: `expert.md`. `kind: exec` introduces multi-artifact experts.

```
experts/StaticAnalyser/
  expert.md           ← metadata, frontmatter, description
  analysis.py         ← packaged executable (referenced by ./analysis.py)
  requirements.txt    ← runtime dependencies (optional, runtime-specific)
```

The `ExpertLoader` must:
1. Resolve `executable` paths relative to the expert's directory
2. Pass the expert directory as the `WorkingDirectory` to the backend
3. Not validate or inspect executable content — that is the backend's concern

### Executable source options

Three valid sources for the executable, in order of hermeticity:

| Source | Syntax | Notes |
|---|---|---|
| Expert-packaged | `executable: ./analysis.py` | Hermetic; versioned with expert; ideal for OCI distribution |
| System-installed | `executable: semgrep` | No `./` prefix; resolved from PATH; practical for dev tooling |
| OCI-pulled | TBD | Cleanest for platform; aligns with Phase 11 expert sourcing model |

System-installed is the right default for local development workflows (e.g. `semgrep`,
`git`, `python3`, `jq`). Expert-packaged scripts are the correct model for published
capabilities in Forge Runtime (Phase 31) — the expert is self-contained.

---

## Hub + Spokes

### Spoke 1 — Frontmatter and validation

Add `kind: exec` support to `ExpertFrontmatter` and `ExpertDefinition`:
- New fields: `executable`, `runtime`, `inputs`, `outputKey`, `timeout`
- `ExpertLoader` validation: if `kind == "exec"`, require `executable`, `inputs`, `outputKey`
- `ExpertLoader` resolves `executable` path relative to expert directory for `./` paths;
  leaves system tool names as-is for PATH resolution at runtime
- Error messages follow existing `ExpertLoadException` pattern with field name + guidance

### Spoke 2 — Input/output contract

Implement the JSON stdin/stdout contract:
- Serialiser reads declared `inputs` keys from context bag, writes JSON to stdin
- Reads stdout as JSON, extracts `outputKey` value, writes to context bag
- Captures stderr to step trace (not pipeline result)
- Handles missing output key: `ExpertLoadException` with clear message
- STJ source-gen context for any new types flowing through serialisation (AOT requirement)

### Spoke 3 — `ExecExpertRunner`

Implements `IExpertRunner`. Dispatches to `IExecBackend`. Wraps result in `StepEnvelope`:
- Successful execution with valid JSON → `status: pass`
- Non-zero exit code → `status: fail` with stderr in reason
- Timeout → `status: fail` with timeout message
- JSON parse failure on stdout → `ExpertLoadException` (configuration error, not runtime)
- `StreamAsync` implementation: process backend streams stderr lines as progress;
  result is only available after exit (no true streaming for exec)

### Spoke 4 — Process backend

`ProcessExecBackend`:
- `Process.Start` with `UseShellExecute = false`, `RedirectStandardInput/Output/Error = true`
- Build argv correctly: `runtime` is the executable, `executable` is first argument (for
  interpreted scripts); omit runtime for native binaries
- Write input JSON to stdin; close stdin after write
- Read stdout and stderr concurrently (avoid deadlock on full pipe buffers)
- Enforce timeout via `CancellationToken` + `Process.Kill`
- Document explicitly: trusted code only, local development only

### Spoke 5 — WASM backend

`WasmExecBackend` via Wasmtime:
- WASI capability grants configurable in `forge.toml` per expert or globally
- Stdin/stdout/stderr mapped to WASI streams
- Python support via wasm32-wasi Python build or Pyodide
- Evaluate in-process vs subprocess execution modes (in-process preferred for latency)

### Spoke 6 — Hyperlight backend

`HyperlightExecBackend`:
- Micro-VM per invocation; disposable
- Evaluate guest runtime support matrix at time of implementation
- Fallback: if VT-x unavailable, fail with clear error directing operator to `wasm` backend
- Intended primarily for Phase 31 Forge Runtime multi-tenant deployments

### Spoke 7 — Artifact packaging and `ExpertLoader`

- `ExpertLoader` passes expert directory as working directory to backend
- Multi-artifact expert directory support (directory is already the unit — this is
  primarily a documentation and validation change)
- `forge init` resolves executable dependencies for expert-packaged scripts (e.g.
  `requirements.txt` → `pip install`) — design TBD
- `forge validate` checks that declared `executable` exists (local path) or is on PATH
  (system tool) and reports missing executables early

### Spoke 8 — `forge.toml` execution config

```toml
[execution]
backend = "process"     # process | wasm | hyperlight

[execution.wasm]
allow_filesystem = false
allow_network = false
filesystem_roots = []   # explicit grants

[execution.process]
# no isolation options — trusted code only, documented as such
```

Backend selection is operator-level config. Expert authors are isolated from it.

---

## Design questions (unresolved)

| # | Question | Notes |
|---|---|---|
| 1 | **JSON stdin/stdout vs alternatives** — should binary data (images, model weights) be supported via temp files? | JSON stdin/stdout is right for structured data. Binary data is out of scope for v1; temp file pattern can be added later. |
| 2 | **Multiple output keys** — should an exec expert be able to write multiple context bag keys from one execution? | Single `outputKey` is simpler. Multiple outputs could be addressed by returning a JSON object and letting the next `json_extract` step decompose it. Defer. |
| 3 | **Loop convergence tie-in** — if `kind: exec` produces a `status: fail`, does the `onFail` pattern from `kind: rule` apply? | The loop feedback mechanism (Phase 14/28) should work uniformly across all kinds. `onFail` field on `kind: exec` experts is worth adding for consistency. Needs design. |
| 4 | **Executable dependency installation** — for expert-packaged scripts, who installs `requirements.txt` / Go modules? | Options: `forge init` handles it, user handles it, runtime handles it lazily on first activation. `forge init` is consistent with the existing init-time resolution pattern. |
| 5 | **System tool discovery** — should `forge validate` verify system-installed executables are on PATH, or defer to runtime? | Fail early is MCL's principle. `forge validate` should check PATH for non-`./` executables and warn if missing. |
| 6 | **Timeout granularity** — global default in `[execution]` or per-expert in frontmatter, or both? | Both: global default in `forge.toml`, per-expert override in frontmatter. The expert author knows the expected runtime better than the operator. |
| 7 | **AOT implications for WASM and Hyperlight backends** — embedding Wasmtime or Hyperlight in a `PublishAot` binary may require investigation | These backends likely live in `ForgeMission.Platform` (Phase 31, non-AOT) rather than the AOT CLI binary. The process backend is AOT-safe. The AOT binary supports only `process`; the platform binary adds `wasm` and `hyperlight`. |
| 8 | **`kind: exec` vs `kind: http` boundary** — when should an author choose exec over http? | `http` = pre-deployed, long-running service. `exec` = packaged, per-invocation, hermetic. The author guide should make this explicit. |
| 9 | **Streaming from exec** — should a long-running executable stream output lines back as step progress? | Process backend can stream stderr lines as progress indicators. stdout result is only available on exit. True streaming (interleaved results) deferred — not needed for v1. |
| 10 | **Security model documentation** — how prominently should the process backend's "trusted code only" constraint be surfaced? | `forge validate` should emit a warning when `kind: exec` expert is used with the process backend. Opt-out: `trusted: true` in frontmatter silences the warning. |

---

## What is NOT in scope

- Shell execution (`sh -c "..."`) — execution is always explicit argv, never a shell string
- Streaming results from exec (stdout is only available at process exit)
- Binary data over stdin/stdout (JSON only in v1)
- Distributed execution (exec runs on the same host as the Forge process)
- Per-expert backend override (backend is operator config, not author config)
- WASM or Hyperlight in the AOT CLI binary — those backends live in the platform tier

---

## Connection to Phase 31 (Forge Runtime Platform)

Executable experts compound with the platform vision. A hosted `security-audit`
capability in the Forge Runtime workforce that runs real static analysis (`semgrep`,
custom scanners) and reasons over actual evidence is a fundamentally different product
from one that only does LLM reasoning. The `wasm` and `hyperlight` backends are
particularly relevant for Phase 31 multi-tenant deployments where tenant-authored
executable experts must not cross isolation boundaries.
