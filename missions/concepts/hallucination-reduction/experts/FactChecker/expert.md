---
name: FactChecker
input: a structured markdown briefing
output: pass if all structural checks pass; fail with specific violation otherwise
kind: rule
check: 'markdown_has_heading and word_count >= 150 and contains "conclusion"'
onFail: 'Structural check failed. Your briefing must have a markdown heading (## Title), at least 150 words, and the word "conclusion" somewhere in the text. Fix what is missing.'
---
