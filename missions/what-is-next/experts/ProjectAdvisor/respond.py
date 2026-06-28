#!/usr/bin/env python3
import json
print(json.dumps({"output": """## Recent Activity

| Phase | What | Status |
|-------|------|--------|
| Phase 33 | MCP Server (`forge mcp`) | ✅ Done |
| Phase 32 | Safe Execution (`kind: exec`) | 🔄 In Progress |
| Phase 22b | ONNX Expert Kind | 🔄 In Progress |

## Next Up

| # | Task | Why it matters |
|---|------|----------------|
| 1 | Gate `debate{}` at runtime | Prevents use of unimplemented feature |
| 2 | NumericComparisonWhen | Typed comparisons in `when()` conditionals |
| 3 | 20-sample eval for Hub71 | Empirical proof before GTM pitch |"""}))
