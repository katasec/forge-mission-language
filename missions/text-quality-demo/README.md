# Text Quality Demo

Demonstrates the full **LLM → json_extract → ONNX → LLM** pipeline pattern in MCL.
An LLM extracts numeric features from text, an in-process ML model scores them, and a
second LLM explains the result in plain English.

## Pipeline

```
FeatureExtractor  (kind: llm)
    → ExtractFeatures  (kind: json_extract)
    → QualityScorer    (kind: onnx)
    → Explainer        (kind: llm)
```

### Why this pipeline shape?

ONNX models speak floats. LLMs speak text. The two can't be wired together directly
because the LLM's output lands in `context["output"]` as a string, and `OnnxExpertRunner`
needs named numeric values in the context bag by key.

`kind: json_extract` is the bridge. It parses the LLM's JSON output and promotes each
top-level key into the context bag as a typed value — numbers become `double`, strings
stay strings. After `ExtractFeatures` runs, downstream steps can reference `{{word_count}}`
in a prompt or read `word_count` as a float feature for ONNX inference.

### Step-by-step

**1. FeatureExtractor** (LLM)

Receives `{{text}}` and is told to output *only* a bare JSON object — no prose, no
markdown fences. The system prompt enforces the exact schema the model expects:

```json
{"word_count": 245, "avg_sentence_length": 18.3, "vocabulary_richness": 0.81}
```

**2. ExtractFeatures** (`kind: json_extract`)

No model. No HTTP call. No system prompt. The runner reads `context["output"]`, calls
`JsonDocument.Parse`, and writes each property directly into the context bag:

```
context["word_count"]            = 245.0   (double)
context["avg_sentence_length"]   = 18.3    (double)
context["vocabulary_richness"]   = 0.81    (double)
```

The original JSON string is passed through as `context["output"]` unchanged so
subsequent steps can still see it if needed.

**3. QualityScorer** (`kind: onnx`)

Reads the three named doubles from the context bag, packs them into a float32 tensor,
and runs the ONNX model in-process. No HTTP roundtrip, no external service.

```markdown
---
kind: onnx
model: ./models/quality-scorer.onnx
inputs: [word_count, avg_sentence_length, vocabulary_richness]
outputKey: quality_score
threshold: 0.5
---
```

The inference score is written to `context["quality_score"]` as a `double`. Scores
above `threshold` mark the step as `fail`; at or below → `pass`.

**4. Explainer** (LLM)

Reads `{{quality_score}}` and the raw feature values via template interpolation and
returns a plain-English explanation of what the scores mean.

## The ONNX model

The model is a scikit-learn logistic regression exported to ONNX format. It takes the
three float features as a `[1, 3]` tensor and outputs a probability between 0 and 1,
where higher means lower text quality.

It is **not checked into the repository** — ONNX model files are binary and large.
Generate it once before running the mission.

### Generate the model

```bash
pip install scikit-learn skl2onnx
python generate_model.py
```

This writes `models/quality-scorer.onnx` into the mission directory and prints a
sanity-check showing the score for a high-quality and a low-quality synthetic sample.

The training data in `generate_model.py` is intentionally simple — 8 high-quality
rows and 8 low-quality rows with clear separation so the logistic regression
generalises cleanly. The point is to demonstrate the pipeline, not to ship a
production scoring model. Swap in any ONNX model that accepts a `[1, 3]` float32
tensor named `input` and emits a single float score.

## Setup

```bash
# 1. Generate the ONNX model (one-time)
cd missions/text-quality-demo
pip install scikit-learn skl2onnx
python generate_model.py

# 2. Set your API key
export MCL_API_KEY=sk-...          # or ANTHROPIC_API_KEY for Anthropic

# 3. Run
forge run --mission TextQuality
```

The default `forge.toml` uses OpenAI (`gpt-4o-mini`). To switch to Anthropic, edit
the `[providers.default]` block in `forge.toml`.

## What makes this interesting

Most ML scoring pipelines require a microservice or a Python runtime sitting alongside
the application. `kind: onnx` runs the model inside the `forge` process — no sidecar,
no network call, no serialisation overhead. The native OnnxRuntime library ships next
to the `forge` binary in the release archive.

The `kind: json_extract` step is what makes the LLM-to-ONNX handoff clean. Without
it, you would need to hard-code the feature values as `with()` bindings in the mission
file, which defeats the purpose of having an LLM extract them dynamically.

The pattern generalises to any use case where an LLM extracts structured signal from
unstructured input and a downstream ML model scores it — log anomaly detection, trading
signal aggregation, document classification, or anything else where you want the
expressiveness of language plus the precision of a trained model.
