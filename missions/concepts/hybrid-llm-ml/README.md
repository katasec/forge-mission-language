# Hybrid LLM + Classical ML

## 1. Foundational concept

Neural systems (LLMs) are fluent, flexible, and capable of open-ended reasoning.
Classical ML systems (logistic regression, random forests, calibrated classifiers)
are reproducible, fast, and produce calibrated numeric scores that are comparable
across thousands of inputs.

Neither alone is optimal. An LLM can assess text quality but produces inconsistent
scores across runs — it has no calibrated notion of "0.73 quality." A classical ML
model can produce consistent, calibrated scores but cannot extract semantic features
from unstructured text.

The Composition of Experts (CoE) literature (2024) demonstrates that routing inputs
to heterogeneous specialised models — not just to different LLMs — outperforms any
single model. As Marcus & Belle (AAAI 2025) argue: the architecture should use each
component for what it does best. LLMs for perception and reasoning; ML for calibrated,
repeatable scoring.

MCL makes this composition first-class: `kind: onnx` treats a classical ML model as
a pipeline expert, declared the same way as an LLM expert, resolved by the same
runtime. The handoff between LLM output (text) and ML input (floats) is handled by
`kind: json_extract`.

## 2. References

**Papers:**
- Feng, L., et al. — "Composition of Experts / CoE" — 2024 —
  https://arxiv.org/pdf/2412.01868
  Modular expert composition; routing inputs to specialised experts outperforms
  monolithic models. Extends naturally to heterogeneous expert types.
- Marcus, G. & Belle, V. — "The Future Is Neuro-Symbolic" — AAAI 2025 —
  https://www.rivista.ai/wp-content/uploads/2025/11/Belle_Marcus_AAAI-2.pdf
  Neural generation + classical/symbolic scoring as the architectural pattern for
  reliable AI systems.
- Wang, J., et al. — "Towards Generalized Routing: Model and Agent Orchestration" —
  2024 — https://arxiv.org/html/2509.07571v1
  Generalised routing across heterogeneous model types — the conceptual foundation
  for LLM + classical ML pipelines.

**Industry/blog:**
- Zaharia, M., et al. — "The Shift from Models to Compound AI Systems" — BAIR Blog,
  Feb 2024 — https://bair.berkeley.edu/blog/2024/02/18/compound-ai-systems/
  Compound AI systems as the SOTA approach; heterogeneous components as the mechanism.
- IBM — "What Are Compound AI Systems?" —
  https://www.ibm.com/think/topics/compound-ai-systems

## 3. How MCL demonstrates this

```
// Hybrid LLM + Classical ML — heterogeneous expert composition
// CoE (2024) + Marcus & Belle, AAAI 2025

let content = "New feature shipped: CSV export for user data. Completes the
core data portability requirement. Implementation was straightforward and code
is clean. JSON export may be added later but is not required."

mission HybridLLMML(content) = {
    LLMAnalyst
    -> ExtractFeatures
    -> Scorer
    -> LLMInterpreter
}

output(HybridLLMML)
```

Four experts, two kinds. `LLMAnalyst` extracts three quality signals as JSON.
`ExtractFeatures` (`kind: json_extract`) bridges them into typed numeric context
keys — no LLM call, no network. `Scorer` (`kind: onnx`) runs the trained model
in-process and writes `{{communication_quality}}` to context. `LLMInterpreter`
reads the score and explains it. Each step does exactly what it is best at.

### Example A — CoE heterogeneous expert composition

The CoE paper identifies heterogeneous expert composition — different model types for
different pipeline stages — as a key extension of the mixture-of-experts pattern.
This mission is that extension applied to professional text evaluation:

```
LLMAnalyst (kind: llm)
    → ExtractFeatures (kind: json_extract)
    → Scorer (kind: onnx)
    → LLMInterpreter (kind: llm)
```

- **LLMAnalyst** extracts three quality signals from the text as a JSON object:
  specificity, completeness, and risk signal. LLMs are good at semantic extraction.
- **ExtractFeatures** (`kind: json_extract`) promotes the JSON keys into typed numeric
  context variables. This is the LLM → ML handoff bridge.
- **Scorer** (`kind: onnx`) runs a trained logistic regression model on the three
  numeric features and produces a calibrated quality score. ML models are good at
  consistent, calibrated scoring.
- **LLMInterpreter** reads the score and explains it in plain language. LLMs are good
  at generating human-readable explanations.

Each component does what it does best. The handoff is explicit and visible in the
mission file.

**What MCL adds:** the pipeline treats the ONNX model as a first-class expert — not
a preprocessing step or an external call bolted on outside the language. The composition
is declared in MCL syntax and the handoff is handled by the runtime.

### Example B — general heterogeneous pipeline

The LLM → json_extract → ONNX → LLM pattern generalises to any use case where:
1. An LLM extracts semantic features from unstructured text
2. A classical ML model produces a calibrated score from those features
3. An LLM interprets the score for a human audience

Examples:
- Log anomaly detection (LLM extracts anomaly signals, ONNX scores severity)
- Customer support triage (LLM extracts issue signals, ONNX prioritises queue position)
- Document classification (LLM extracts topic signals, ONNX assigns category probabilities)

Swap the `LLMAnalyst` prompt and the `generate_model.py` training data for any new domain.

## 4. Why this is normally hard

Without MCL, a hybrid LLM + ML pipeline requires:
- A Python LLM client for feature extraction
- JSON parsing code to convert the LLM's text output into typed values
- NumPy array construction from the parsed values for the ML model
- ONNX Runtime setup and model loading
- Score extraction from the ONNX output tensor
- A second LLM API call for interpretation, passing the score as a formatted string
- Error handling at every handoff (parse errors, ONNX inference errors, API failures)

This spans 100–150 lines of Python across an LLM client, a parser, and a scoring
module. Swapping the ML model or adding a feature requires touching multiple files.
Most teams avoid the pattern entirely and use an LLM alone — accepting inconsistent,
uncalibrated scores in return for simpler code.

**With MCL:**

The handoff is declared in the mission file. The `kind: json_extract` step bridges
LLM output to ONNX input automatically. The ONNX model runs in-process — no sidecar,
no network call, no serialisation overhead. Swapping the ML model means changing one
path in `experts/Scorer/expert.md`.

## Setup

```bash
# 1. Generate the ONNX model (one-time)
cd missions/concepts/hybrid-llm-ml
pip install scikit-learn skl2onnx numpy
python generate_model.py

# 2. Set your API key
export MCL_API_KEY=sk-...   # or ANTHROPIC_API_KEY for Anthropic

# 3. Run
forge run --mission HybridLLMML
```

The default `forge.toml` uses OpenAI (`gpt-4o-mini`). To switch to Anthropic, edit
the `[providers.default]` block.

To score a different piece of text, edit the `let content` variable in `mission.mcl`.
