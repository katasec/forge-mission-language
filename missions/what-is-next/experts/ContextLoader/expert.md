---
name: ContextLoader
kind: exec
command: python3
args: [./load_context.py]
input: none
inputs: [output]
outputKey: context
output: Concatenated project planning documents
---

Reads project planning files and returns them as a single context string.
