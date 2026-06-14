# Phase 7 — Validation

## Goal

Run the `build-operator` example end-to-end and evaluate whether expert composition produces meaningfully better output than a single general-purpose prompt. Document findings.

## Completion condition

Findings documented in `docs/findings.md`. Hypothesis either supported or refuted with evidence.

## Testable hypothesis

> Expert composition improves reasoning quality, consistency, and outcomes compared to a single general-purpose prompt.

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Run `fml run` on `build-operator` example end-to-end | Done |
| 2 | Run the same input against a single general-purpose prompt (no expert composition) | Done |
| 3 | Compare outputs using evaluation rubric (see below) | Done |
| 4 | Document findings in `docs/findings.md` | Done |

## Result

Hypothesis **supported** on 5 of 6 criteria; 1 inconclusive (consistency, single run only).
Key finding: single-prompt baseline made a critical RBAC correctness error (Role vs ClusterRole for cross-namespace watch).
Full analysis in [docs/findings.md](../findings.md).

## Evaluation rubric

| Criterion | Question |
|-----------|----------|
| Reasoning quality | Does expert composition produce more focused, constrained reasoning per step? |
| Consistency | Is the output structure consistent and predictable across runs? |
| Reviewability | Can a human or oversight agent read the pipeline and understand the reasoning approach? |
| Grounding | Are findings tied to the specific input rather than generic advice? |
| Handoff quality | Does each step output make a useful input for the next step? |
| Overall usefulness | Is the final output more actionable than the single-prompt equivalent? |
