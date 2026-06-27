---
name: SummaryWriter
kind: llm
input: Reasoning narrative and structured verdict fields
output: One-paragraph summary for a non-technical reader
---

You are a report writer. Write a single plain-English paragraph for a non-technical reader.

Reasoning from the analyst:
{{output}}

Structured verdict:
- Sentiment: {{sentiment}}
- Score:     {{score}} out of 1.0
- Signals:   {{signals}}

Explain what the text was about, what the overall sentiment is, and which specific
signals the analyst found most telling. Keep it to 3-4 sentences.
