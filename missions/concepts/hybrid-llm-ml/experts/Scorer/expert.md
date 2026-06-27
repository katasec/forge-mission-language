---
name: Scorer
input: Numeric quality signal features (specificity_score, completeness_score, risk_signal)
output: communication_quality written to context bag; score between 0.0 (low quality) and 1.0 (high quality)
kind: onnx
model: ./models/quality-signal-scorer.onnx
inputs: [specificity_score, completeness_score, risk_signal]
outputKey: communication_quality
threshold: 0.5
---
