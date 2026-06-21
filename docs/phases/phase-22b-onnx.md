# Phase 22b — ONNX Expert Kind

> **Status: In Progress (Spokes 1–4 Done, Spokes 5–6 Todo)**  
> **Depends on:** Phase 22a (kind dispatch, HttpExpertRunner) — Done  
> **Blocks:** UC-3 (Log Anomaly Detection) demo mission  
> **Context:** This is the second half of Phase 22. Phase 22a shipped `kind: http` and
> the kind dispatch infrastructure. Phase 22b completes the non-LLM expert story by
> adding `kind: onnx` for in-process ML model inference.

---

## Why this matters — the use cases

Three customer use cases motivate non-LLM expert stages. All three were recorded before
this phase was planned, and all three require ML model inference at some step in the pipeline:

**UC-1: Image Analysis Pipeline** — FaceRecogniser, TextExtractor, ObjectClassifier are
classical ML/vision models. They could be `kind: http` (calling an external vision API)
but the canonical form has them as embedded ONNX models running in-process.

**UC-2: Trading Signal Aggregator** — MarketContext, StockContext, SectorContext could
be LLM experts (GPT reading market narrative), but the production use case has scoring
models running against numeric feature vectors. ONNX handles that.

**UC-3: Log Anomaly Detection** — AnomalyDetector is explicitly an ONNX isolation-forest
model in the design spec. It reads named float features (`cpu_usage`, `memory_usage`,
`request_latency`) from the context bag and emits an anomaly score. This is the hardest
dependency — `kind: http` could stand in if the model is deployed as a microservice, but
the intent is always in-process.

---

## Design

### Expert frontmatter

New fields added to `ExpertFrontmatter` and `ExpertDefinition`:

```markdown
---
name: AnomalyDetector
input: Normalised metric features
output: Anomaly score and pass/fail decision
kind: onnx
model: ./models/isolation-forest.onnx
inputs: cpu_usage, memory_usage, request_latency
outputKey: anomaly_score
threshold: 0.85
---
```

| Field | Required | Description |
|-------|----------|-------------|
| `model` | Yes (kind:onnx) | Path to the `.onnx` file, relative to the expert's directory |
| `inputs` | Yes | Comma-separated list of context bag keys to read as float features |
| `outputKey` | Yes | Context bag key to write the inference score into |
| `threshold` | Yes | Score above this → `status: fail`. At or below → `status: pass` |

Validation in `ExpertLoader.ParseFile`: if `kind == "onnx"`, require `model`, `inputs`,
`outputKey`, `threshold`. Throw `ExpertLoadException` with a clear message if any are missing.

### Context bag typing

Currently `Dictionary<string, object>` holds string values exclusively. ONNX features are
floats. The change: allow `double` to be stored alongside strings. No breaking change:

- LLM prompt interpolation (`{{key}}`) calls `.ToString()` — doubles format correctly
- `kind: rule` evaluators already work on the string `.ToString()` of the output
- ONNX runner reads `double` directly; throws `RuleEvaluationException` (or a new
  `OnnxFeatureException`) if a key is missing or not numeric

Decision: keep the bag as `Dictionary<string, object>`. Typed values are stored as `double`.
A typed context bag (with schema) is under discussion but deferred — the loose bag is
sufficient for these use cases.

### OnnxExpertRunner

```csharp
public class OnnxExpertRunner : IExpertRunner
{
    public Task<StepEnvelope> RunAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        CancellationToken ct = default)
    {
        // 1. Load InferenceSession from expert.Model path
        // 2. Extract float features from context by expert.Inputs keys
        // 3. Run inference → score (float/double)
        // 4. Write score to context[expert.OutputKey]
        // 5. Compare score to expert.Threshold
        // 6. Return StepEnvelope(score.ToString(), score > threshold ? "fail" : "pass")
    }
}
```

`InferenceSession` is created per-call (or cached — TBD based on AOT constraints).
For the initial implementation: create per-call, measure perf, cache if needed.

### Kind dispatch

Add `"onnx"` to the switch in both `ExecuteStepAsync` and `ExecuteParallelStepAsync`
in `PipelineRunner.cs`:

```csharp
var runner = expert.Kind switch
{
    "http" => (IExpertRunner)new HttpExpertRunner(),
    "rule" => new RuleExpertRunner(),
    "onnx" => new OnnxExpertRunner(),
    _      => ResolveRunner(step.Using)
};
```

---

## AOT considerations — critical gate

This is the primary risk of the phase. OnnxRuntime 1.27.0 was inspected:

- Ships native libraries for all three target platforms: `osx-arm64`, `linux-x64`, `win-arm64` ✓
- Managed bindings (`Microsoft.ML.OnnxRuntime.Managed`) are P/Invoke-based — not reflection-heavy
- No `System.Reflection.Emit` in the managed API surface
- IL3050 suppression is already configured in `Cli.csproj`

### AOT Probe Results (Spoke 1 — Done 2026-06-21)

`dotnet publish src/ForgeMission.Cli -c Release -r osx-arm64` with OnnxRuntime 1.27.0:

- **Zero ILC warnings** — no IL2xxx or IL3xxx. OnnxRuntime is pure P/Invoke.
- **Binary size:** `forge` = 26 MB, `libonnxruntime.dylib` = 37 MB, both in the same output dir.
- **Single-binary constraint: resolved → Option A.** NuGet's RID-specific native asset
  extraction puts `libonnxruntime.dylib` next to `forge` automatically at publish time.
  The release workflow must zip them together per platform.
- **No `[DynamicDependency]` or additional IL suppressions needed** for OnnxRuntime itself.

**Decision: Option A** — ship as a zip archive per platform (forge + libonnxruntime in one dir).
Users who only use `llm`/`http`/`rule` experts and never use `kind: onnx` experts
are unaffected — the dylib is inert unless an `OnnxExpertRunner` is invoked.

**AOT probe is Spoke 1** — must complete before any other spoke is scheduled.

---

## Package placement

`Microsoft.ML.OnnxRuntime` referenced in `ForgeMission.Core.csproj` (so Core owns the
runner, consistent with `HttpExpertRunner` and `RuleExpertRunner`). The native library
lands in the publish output automatically via the package's `runtimes/` folder.

---

## Hub + Spokes

### Spoke 1 — AOT Compatibility Probe (gate) ✓ Done

Added `Microsoft.ML.OnnxRuntime 1.27.0` to `ForgeMission.Core.csproj`. Wrote minimal
`OnnxExpertRunner` stub using `InferenceSession`, `DenseTensor<float>`, `NamedOnnxValue`.
Extended `ExpertDefinition` with `Model`, `Inputs`, `OutputKey`, `Threshold`, `IsOnnx`.

`dotnet publish` native AOT on osx-arm64: **zero ILC warnings**. Option A confirmed.
Release workflow update deferred to Spoke 5.

See AOT Probe Results section above for full details.

### Spoke 2 — Typed Context Bag ✓ Done

`ContextInterpolator.Interpolate` already calls `.ToString()` on all `object` values.
`Dictionary<string, object>` accepts `double` at assignment time. No code changes needed —
the bag already handles typed values correctly. `OnnxExpertRunner` stores scores as `double`;
LLM steps interpolate them via `.ToString()` transparently.

### Spoke 3 — Expert Frontmatter Extension ✓ Done

Added `Model`, `Inputs`, `OutputKey`, `Threshold` to `ExpertFrontmatter` (private POCO)
and threaded through to `ExpertDefinition` constructor. `[DynamicDependency]` already
covered the POCO via the existing attribute. Validation in `ExpertLoader.ParseFile`:
missing any of the 4 fields when `kind: onnx` → `ExpertLoadException` with clear message.

5 tests added to `ExpertLoaderTests.cs` (1 happy-path + 4 missing-field cases). All pass.

### Spoke 4 — OnnxExpertRunner ✓ Done

`OnnxExpertRunner : IExpertRunner` implemented in `src/ForgeMission.Core/Adapters/OnnxExpertRunner.cs`.
Reads named float features from context, builds `DenseTensor<float>`, runs `InferenceSession`,
writes score as `double` to `context[expert.OutputKey]`, returns pass/fail envelope.

`"onnx"` added to kind dispatch in both `ExecuteStepAsync` and `ExecuteParallelStepAsync`
in `PipelineRunner.cs`.

Note: unit tests for `OnnxExpertRunner` require a real `.onnx` file. The UC-3 demo mission
(Phase 29 Spoke 3) will serve as the end-to-end integration test with a real isolation-forest model.

### Spoke 5 — Single Binary Decision + Release Update

Decision from Spoke 1: Option A. Release workflow must be updated to zip
`forge` + `libonnxruntime.{dylib,so,dll}` per platform into a single archive.
`language.md` already documents `kind: onnx` (done in Spoke 4 session).
README update and GHA workflow zip step still outstanding.

### Spoke 6 — kind: json_extract (Feature Injection Bridge)

**Motivation:** `OnnxExpertRunner` reads named float features from the context bag
by key. But there is no built-in mechanism to take an LLM step's JSON output and
inject individual fields as separate context bag keys. Without this, the only way
to feed ONNX is via hard-coded `with()` mission parameter bindings — which is
impractical for real pipelines.

**What it does:** A new expert kind `kind: json_extract` parses `context["output"]`
as JSON and injects each top-level key as a context bag entry. No model, no HTTP call,
no system prompt — purely structural.

```markdown
---
name: ExtractFeatures
input: JSON object with float features
output: Individual context bag entries
kind: json_extract
---
```

The expert frontmatter body (system prompt) is unused. The runner reads
`context["output"]`, calls `JsonDocument.Parse`, iterates `RootElement.EnumerateObject()`,
and writes each property into the context bag:
- `JsonValueKind.Number` → stored as `double`
- `JsonValueKind.String` → stored as `string`
- `JsonValueKind.True`/`False` → stored as `string` ("True"/"False")

`ExpertDefinition` gains `IsJsonExtract` predicate (`Kind == "json_extract"`).
`ExpertLoader` validation: no extra required fields for `kind: json_extract`.
`PipelineRunner` kind dispatch: `"json_extract" => new JsonExtractExpertRunner()`.

**Full pipeline enabled by this:**

```fsharp
mission ContentQuality(text) = {
    FeatureExtractor      // kind:llm — outputs {"word_count": 245, "avg_sentence_len": 18.3}
    -> ExtractFeatures    // kind:json_extract — injects word_count, avg_sentence_len into context
    -> QualityScorer      // kind:onnx — reads word_count + avg_sentence_len as floats, scores quality
    -> Explainer          // kind:llm — reads {{quality_score}}, explains the result
}
```

**AOT:** `JsonDocument.Parse` / `JsonElement.EnumerateObject()` is STJ — fully AOT-safe.
No source-gen context needed for dynamic JSON traversal (we read raw `JsonElement`, no
deserialization to a typed object).

**Tests:**
- JSON with numeric fields → doubles written to context
- JSON with string fields → strings written to context
- Invalid JSON → `JsonException` propagates as step failure
- Downstream ONNX step reads injected keys correctly (integration)

**Note:** This spoke was identified as a gap when designing the Phase 29 Option B demo
(text → ONNX pipeline). It must land before the demo mission can be built.

---

## Dependencies

- Phase 22a (kind dispatch, `ExpertDefinition.Kind`, `ExpertLoader` validation) — Done
- Phase 21 (named outputs, `{{AnomalyDetector.output}}`) — Done
- No grammar changes — `kind: onnx` is a frontmatter field like `kind: http`

---

## Open questions

1. **Single binary constraint** — resolved by Spoke 1 probe findings (see above)
2. **InferenceSession caching** — per-call creation is simple; cache by model path if
   perf is a concern (deferred until measured)
3. **Model path resolution** — relative to expert.md directory or to CWD? Recommend
   relative to expert.md (consistent with OCI pull behaviour)
4. **Float vs double** — ONNX tensors are float32. Context bag stores double. Cast at
   the runner boundary; document the precision loss is expected.
