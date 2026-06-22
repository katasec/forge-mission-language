---
name: StepVerifier
input: a step-by-step reasoning chain
output: pass if the chain meets all structural requirements; fail with specific violation otherwise
kind: rule
check: 'contains "Answer:" and sentence_count >= 5 and contains "Step 1"'
onFail: 'Reasoning chain incomplete. Your response must include "Step 1" (numbered steps), at least 5 sentences showing your work, and "Answer:" followed by the final result. Fix what is missing.'
---
