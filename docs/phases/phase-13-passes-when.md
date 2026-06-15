# Phase 13 — passes when

**Status:** Dropped

## Why Dropped

`passes when` was designed to declare which step is the quality gate for a mission.
It was superseded by a simpler model based on the bash exit-code convention:

- Every step passes by default (like exit 0)
- A step explicitly signals failure by returning `status: fail` in its envelope (like non-zero exit)
- Mission passes when all steps pass — no declaration needed, it is the universal contract
- Failure conditions belong in the expert's own MD, not in the mission grammar

This eliminates the need for `passes when` entirely. Any expert can be a quality gate simply
by declaring its failure condition in its system prompt. The mission stays clean.

See [Phase 12 — StepEnvelope](phase-12-step-envelope.md) for the envelope model.
See [Phase 14 — loop N](phase-14-loop.md) for the retry behaviour.
