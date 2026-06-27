# Design Decisions — v0.7.0 (kind:exec, mixed prose+JSON)

Recorded during the implementation session that produced v0.7.0. Each decision was
reviewed through two lenses: **Anders Hejlsberg** (C#/TypeScript — type safety,
readability, self-documenting APIs) and **Rob Pike** (Go — simplicity, composability,
trust the OS, fewer concepts).

---

## 1. `command` + `args` over `runtime` + `executable`

**Original design:** `runtime: python3`, `executable: ./analyse.py`

**Decision:** Rename to `command` + `args`, following the Docker/K8s model.

**Why:** `runtime` implies a managed execution environment (JVM, CLR). `executable`
implies a self-contained binary. Neither word fits the common case of `python3 ./analyse.py`.
Docker and Kubernetes solved this already: `command` is the entry point, `args` is the
argument list. MCL should use the same mental model that platform engineers already have.

**Pike:** fewer new concepts. Reuse an existing model operators know.
**Anders:** `command`/`args` is a more accurate type — `runtime` misleads about semantics.

---

## 2. `inputs` and `args` as typed YAML lists, not comma-separated strings

**Original design:** `inputs: repo_path, language` (string, split on comma)

**Decision:** `inputs: [repo_path, language]` (native YAML sequence)

**Why:** Comma-separated strings are a mini-DSL inside a field value. They require the
parser to split, trim, and handle edge cases (spaces, empty strings). YAML lists are
already parsed by the deserialiser — no additional parsing, no ambiguity, no edge cases.
The same applies to `args`: `args: [./analyse.py, --config=auto]` is unambiguous regardless
of spaces in paths.

**Anders:** the type system should carry the contract. A field that holds a list should
be a list, not a string that happens to contain commas.
**Pike:** fewer moving parts. One less custom parser.

This required `ExpertFrontmatter.Inputs` and `Args` to become `List<string>`, and
`ExpertDefinition.Inputs` and `Args` to become `IReadOnlyList<string>?`.

---

## 3. `ProcessStartInfo.ArgumentList` over raw string args

**Decision:** Use `ProcessStartInfo.ArgumentList.Add(arg)` per argument, not a single
command string.

**Why:** `ArgumentList` bypasses shell parsing entirely. Each argument is a discrete
string — no quoting, no escaping, no shell injection. A path with spaces (`/Users/me/my project/analyse.py`) works without any quoting. This is what the typed list design makes natural.

**Anders:** the API contract is explicit. Passing a list of strings forces the caller to
think in discrete arguments, not in shell syntax.
**Pike:** let the OS handle argument passing. Don't reinvent shell quoting.

---

## 4. PATH checking removed from `forge validate`

**Original design:** `forge validate` would call `IsOnPath(command)` and throw if the
tool was not found on PATH.

**Decision:** Removed entirely. Let the OS report the error at runtime.

**Why:** PATH is ephemeral — it depends on the shell, the user, the environment, the
CI runner, the container. Checking it at validate time gives false confidence: the tool
could be on PATH now but not when the process runs (different user, different container).
It also replicates what the OS already does better. The error from `Process.Start` when
the executable is missing is clear: `No such file or directory`.

**Pike (decisive):** trust the OS. Checking PATH is redundant and checks the wrong
moment in time. Remove the check and the code.
**Anders:** agreed — the abstraction is at the wrong level. Validate what you can
guarantee; delegate what you can't.

This removed ~40 lines of code from `ExpertLoader.Validate`.

---

## 5. `kind` is invisible in the mission pipeline

**Question raised:** should the pipeline syntax distinguish expert kinds, e.g.
`CodeAnalyser using exec`?

**Decision:** No. Kind stays in the expert frontmatter, invisible to the mission.

**Why:** The pipeline is a composition of black-box experts. Whether `CodeAnalyser`
runs Python or calls an LLM is an implementation detail the mission author should not
care about or be coupled to. If you swap `CodeAnalyser` from `exec` to `http`, the
mission file should not change.

The operational characteristics that differ between kinds (cost, latency, infrastructure
requirements) should be surfaced by tooling: `forge validate` summary, LSP hover info,
`forge list`. Not by syntax.

**Pike (decisive):** the middleware contract is the abstraction. The host is blind to
implementations. Leaking kind into the pipeline breaks the abstraction.
**Anders:** agreed on principle. Countered that operational visibility matters — but
that is a tooling argument, not a language argument.

`using` remains purely for provider profile selection (infrastructure, not kind).

---

## 6. Mixed prose+JSON in `json_extract` as safety net, not primary format

**Question raised:** should `json_extract` require LLM steps to produce fenced JSON?

**Decision:** `json_extract` handles both pure JSON and mixed prose+JSON transparently.
The mixed-mode path is a safety net — it activates when the LLM adds preamble around
the JSON. Pipelines should not depend on it as the primary format.

**Why:** LLMs are nondeterministic. Prompting for "only output JSON" works most of the
time but not always — the model may add `"Sure! Here is the JSON:"` or reason before
answering. The fence extraction handles this without requiring the pipeline author to
engineer away the preamble. When mixed output is intentional (chain-of-thought reasoning
+ structured verdict), the prose flows naturally into `{{output}}` for downstream steps.

**Precedence:**
1. If output contains a ` ```json ` fence → extract the block, preserve prose in `{{output}}`
2. If output is pure JSON → pure JSON path (backwards compatible)
3. Neither → clean error: `json_extract (Name): output contains neither valid JSON nor a ```json fence`

---

## 7. `MclContext` deferred to Phase 33

**Question raised:** should MCL introduce an `MclContext` type analogous to ASP.NET
Core's `HttpContext` — a single structured object instead of a `Dictionary<string, object>`?

**Decision:** Deferred. Document the design, implement only when one of three triggers fires:
1. The parallel execution collision bug actually occurs in a real pipeline
2. A cross-cutting concern (audit, telemetry, security) needs structured runtime metadata
3. Phase 31 (Forge Runtime Platform) requires it for capability routing

**Why deferred:** The OWIN argument holds. The power of the middleware pattern is that
the context is shapeless — any middleware can read and write any key without coupling to
the host's type system. Introducing a typed `MclContext` too early forces a decision about
what belongs in "user state" vs "runtime metadata" before real usage reveals the right split.

**The right split when it arrives:**
- `State: Dictionary<string, object>` — user-domain data (same as today)
- `Runtime: MissionRuntime` — structured metadata (attempt number, mission name, step trace)

`State` must stay shapeless (OWIN argument). `Runtime` is the only new thing.

See `docs/phases/phase-33-mcl-context.md` for the full design.

---

## 8. Step error UX — clean message over stack trace

**Problem:** when a step runner threw an exception (e.g. `json_extract` receiving
non-parseable input), the runtime surfaced it as an unhandled exception with a full
.NET stack trace.

**Decision:** `PipelineRunner.ExecuteStepAsync` wraps all runner exceptions in
`InvalidOperationException` with `Step 'X' failed: <message>`. `Program.cs` catches
this and routes it through `Die()` for a clean `error:` line.

The inner exception message is NOT appended — only the MCL-level message is shown. This
prevents JSON parser internals (`'h' is an invalid start of a value. LineNumber: 0 |
BytePositionInLine: 0`) from leaking into the user-facing error.

**Result:**
```
error: Step 'VerdictExtractor' failed: json_extract (VerdictExtractor): output contains neither valid JSON nor a ```json fence
```

**Why:** The user needs to know which step failed and why in MCL terms. They do not
need to know which .NET type threw or at which byte offset the JSON parser failed.
Stack traces are for `--verbose` and bug reports, not for normal operation.
