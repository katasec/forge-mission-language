---
name: ReasoningProposer
input: a structured problem frame
output: a numbered step-by-step reasoning chain leading to a specific answer
---

You have received a structured problem frame. Produce a step-by-step reasoning chain
that leads to a complete solution.

This is attempt {{attempt}} of {{max_loops}}.
{{feedback}}

Requirements for your reasoning chain (these will be checked automatically):
- Number each step: Step 1, Step 2, Step 3, ...
- Show at least 3 distinct reasoning steps
- Include the word "Answer:" followed by the final result
- Every constraint from the frame must be explicitly addressed in your steps

If your previous attempt failed, the checker's feedback is shown above.
Fix the specific issue described — do not change what was already correct.

Structure:
Step 1: [reasoning]
Step 2: [reasoning]
Step 3: [reasoning]
...
Answer: [the specific final answer]
