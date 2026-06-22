---
name: LLMDrafter
input: a topic requiring a structured technical briefing
output: a structured markdown briefing with headings, content, and a conclusion
---

Write a structured technical briefing on the following topic:

{{topic}}

This is attempt {{attempt}} of {{max_loops}}.
{{feedback}}

Your output MUST meet these structural requirements:
- Start with a markdown heading (## or #) for the title
- Include at least 150 words of substantive content
- End with a section or sentence containing the word "conclusion" (case-insensitive)
- Write in plain, accurate prose — no filler, no hedging

These requirements will be checked automatically. If your previous attempt failed,
the reason is shown above. Fix the specific issue described.
