# FML Validation Findings

## Hypothesis

> Expert composition improves reasoning quality, consistency, and outcomes compared to a single general-purpose prompt.

## Method

**Test case**: `examples/build-operator/` — design a Kubernetes operator for container image builds using Tekton.

**Expert pipeline** (FML):
```
mission BuildOperatorDesign =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
```

**Baseline**: single prompt to `gpt-4o-mini` with a system prompt that asked it to cover all the same areas (CRD design, controller, RBAC, operations, security, ADR) in one shot.

Both used the same model (`gpt-4o-mini`) and the same input (`examples/build-operator/input.md`). Outputs are in `runs/`.

---

## Findings

### 1. Reasoning quality — SUPPORTED

Each pipeline step stayed within its lane. `KubernetesArchitect` produced precise Go type definitions (`BuildRequestSpec`, `BuildRequestStatus`, typed phases) and a correct ClusterRole with exactly the verbs the controller needs. It didn't venture into security.

`SecurityArchitect` received those types as structured input and identified 6 specific gaps: over-broad RBAC verbs, missing network policies, missing pod security standards, no secret management, no audit logging, unsanitised error messages. Each finding was grounded in the actual design it received — not generic advice.

The single-prompt baseline skimmed all areas. Security was one paragraph: "store secrets in Kubernetes Secrets." No specific gaps identified.

### 2. Correctness — SUPPORTED (critical difference found)

The single-prompt baseline used a namespace-scoped `Role` for RBAC. The requirement explicitly says "watch for BuildRequest CRDs in **any namespace**" — which requires a `ClusterRole`. This is a substantive correctness error.

The pipeline's `KubernetesArchitect` step correctly chose `ClusterRole` because it was focused on the Kubernetes domain and had the constraint in scope. The single-prompt model distributed its attention across all concerns and got this wrong.

The baseline also omitted the `source` field from `BuildRequestSpec` (source URL of the code to build) even though it was explicit in the requirements.

### 3. Handoff quality — SUPPORTED

Context chaining worked as intended. `SecurityArchitect` annotated the exact Go types from step 1 — for example, adding an inline comment to `BuildRequestStatus.Message` about sanitising before returning. It didn't re-derive the types or start over; it operated on concrete artefacts.

`PrincipalReviewer` received a complete, security-reviewed design and produced a structured ADR with 7 actionable conditions. It surfaced exactly the open questions left from step 2 (network policy specifics, secret management strategy, exact pod security standard to apply).

File sizes reflect context growth: `01-KubernetesArchitect.md` (5.6K) → `02-SecurityArchitect.md` (7.9K, added security section on top of base design) → `03-PrincipalReviewer.md` (3.4K distilled ADR).

### 4. Reviewability — SUPPORTED

The `runs/BuildOperatorDesign/` directory is itself a reasoning trace. A human or oversight agent can read steps 1–3 in order and understand exactly what each expert contributed. The single-prompt output is a blob; you cannot tell which concerns were considered and which were skipped until you notice the gaps.

This is what makes expert composition auditable in a way a single prompt cannot be.

### 5. Independence of the final review — SUPPORTED (key structural advantage)

`PrincipalReviewer` gave the design **"Approved with conditions"** — not a rubber stamp. It called out missing secret management detail, insufficiently specified network policies, and unclear pod security standards.

The single-prompt equivalent produced an ADR that summarised its own output approvingly. It cannot self-critique because the reasoning that produced the design and the reasoning that reviews it are the same reasoning at the same moment.

Expert composition creates genuine separation of concerns across reasoning steps.

### 6. Consistency — INCONCLUSIVE

Only one run was performed per method. Consistency across multiple runs would require repeated execution and output diffing. This is a known gap; it could be tested by running the pipeline 3–5 times and comparing structure and key decisions.

---

## Summary

| Criterion | Result | Notes |
|-----------|--------|-------|
| Reasoning quality | **Supported** | Each step focused, deeper per domain |
| Correctness | **Supported** | Single prompt made a critical RBAC scope error; pipeline did not |
| Handoff quality | **Supported** | Context chained correctly; each step built on concrete prior output |
| Reviewability | **Supported** | Pipeline produces an auditable reasoning trace; single prompt does not |
| Independent review | **Supported** | Pipeline's final step can critique what earlier steps produced |
| Consistency | **Inconclusive** | Single run only |

**Hypothesis: supported by this test case.**

Expert composition is not magic — the gains come from structural separation: each expert has a narrower, better-constrained system prompt, receives structured input rather than the original task, and cannot be distracted by concerns outside its role. These properties are hard to replicate with a single general-purpose prompt regardless of how detailed that prompt is.

---

## What this does NOT tell us

- Whether the gains hold for smaller/simpler tasks (they may not — composition adds latency)
- Whether the expert definitions in this example are optimal (they were first-draft)
- How sensitive results are to expert system prompt quality
- Whether a better-engineered single prompt (chain-of-thought, o1-style) would close the gap

These are the right questions for the next round of validation.

---

## Artefacts

| File | Description |
|------|-------------|
| `runs/BuildOperatorDesign/01-KubernetesArchitect.md` | Step 1 output |
| `runs/BuildOperatorDesign/02-SecurityArchitect.md` | Step 2 output (security review + annotated design) |
| `runs/BuildOperatorDesign/03-PrincipalReviewer.md` | Step 3 output (ADR) |
| `runs/BuildOperatorDesign/final.md` | Same as step 3 |
| `runs/single-prompt-comparison.md` | Baseline single-prompt output |
