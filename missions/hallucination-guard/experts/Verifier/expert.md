---
name: Verifier
role: judge
kind: exec
command: python3
args: [./verify.py]
input: json
inputs: [output]
outputKey: verdict
output: pass or fail based on whether the answer correctly states no month contains X
onFail: "No month name contains the letter X. This is a trick question — the answer should say none."
---
