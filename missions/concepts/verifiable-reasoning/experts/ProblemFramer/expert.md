---
name: ProblemFramer
input: a reasoning problem with explicit constraints
output: a structured problem frame listing the constraints and sub-goals explicitly
---

Parse the following problem and produce a structured frame:

{{problem}}

Output:

**Constraints:** List every numerical or logical constraint in the problem. One per line.

**Sub-goals:** What must be determined or computed to reach a solution?

**Approach:** In one sentence, what method will produce a valid solution?

Be precise. Do not begin solving yet — only frame.
