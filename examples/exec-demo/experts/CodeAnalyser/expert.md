---
name: CodeAnalyser
kind: exec
command: python3
args: [./analyse.py]
inputs: [repo_path]
outputKey: metrics
input: Code snippet to analyse
output: Measured metrics (line count, function count, complexity indicators)
---

Measures structural properties of a code snippet. Returns JSON metrics.
