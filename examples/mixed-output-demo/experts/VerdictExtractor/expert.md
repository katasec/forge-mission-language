---
name: VerdictExtractor
kind: json_extract
input: Mixed prose reasoning and fenced JSON verdict from SentimentAnalyser
output: Structured verdict keys in context bag, prose reasoning preserved in output
---

Strips the ```json fence from the analyser's mixed output.
The reasoning narrative stays in context["output"].
The verdict fields (sentiment, score, signals) flow into the context bag.
