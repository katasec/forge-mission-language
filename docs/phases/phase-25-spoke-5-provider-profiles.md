# Phase 25 — Spoke 5: Provider Profiles

## Status: Todo

## Overview

Provider configuration moves from `let` bindings in `mission.mcl` to named profiles in `forge.toml`. A provider is a function — `f(provider, model, apiKey, endpoint?)` — and `forge.toml` supplies the arguments. The runtime holds the function implementations.

## `forge.toml` profiles

```toml
[providers.default]
provider = "openai"
model    = "gpt-4o-mini"
apiKey   = env("MCL_API_KEY")

[providers.architect]
provider = "anthropic"
model    = "claude-opus-4-8"
apiKey   = env("ANTHROPIC_API_KEY")

[providers.local]
provider = "ollama"
model    = "llama3"
endpoint = "http://localhost:11434"
```

## Per-step override in `mission.mcl`

```fsharp
mission BuildOperatorDesign(goal, persona) = {
    KubernetesArchitect using architect
    -> SecurityArchitect
    -> PrincipalReviewer(style: "terse ADR")
    -> Synthesiser(format: "ADR") using architect
}
```

`using architect` tells the runtime to look up the `architect` profile in `forge.toml` and use it for that step only. All other steps use `default`. `()` context is always domain — no reserved keys, no infrastructure meaning.

`using` and `()` context are independent and composable — both, either, or neither may appear on a step.

## Resolution

At step execution time:

1. Check for `using <profile>` clause on the step
2. If present — look up named profile in `ForgeManifest.Providers`
3. If absent — use `providers.default`
4. Construct `IChatClient` from the resolved profile
5. Execute the step

## Supported provider types

| `provider` value | Required fields | Notes |
|-----------------|-----------------|-------|
| `openai` | `apiKey`, `model` | Default endpoint is `api.openai.com` |
| `anthropic` | `apiKey`, `model` | |
| `azure` | `apiKey`, `model`, `endpoint` | `endpoint` is mandatory |
| `ollama` | `model`, `endpoint` | No `apiKey` required |

Unknown `provider` values are an error at startup — not at step execution time. Third-party provider registration is a future extension point; Phase 25 supports built-in providers only.

## Provider schema registry

`ProviderSchemaRegistry` is a compile-time, AOT-safe dictionary in Core. It is the single source of truth for required/optional fields per provider. Consumed by:
- `ForgeTomlReader` (Spoke 2) — validates required fields when parsing `forge.toml`
- `forge provider scaffold` (this spoke) — generates ready-to-paste TOML blocks

```csharp
// AOT-safe: no reflection, no dynamic dispatch
static class ProviderSchemaRegistry
{
    static readonly IReadOnlyDictionary<string, ProviderSchema> Known = ...
}

record ProviderSchema(string[] Required, string[] Optional, IReadOnlyDictionary<string, string> FieldDocs);
```

Validation errors are schema-aware:
```
error[C003]: provider profile 'default' is missing required field 'apiKey'
  = provider 'openai' requires: model, apiKey
  = help: add `apiKey = env("OPENAI_API_KEY")` to [providers.default] in forge.toml
     or: run `forge provider scaffold openai` to regenerate the full block
```

## Discoverability commands

`forge provider list` — prints all known providers and their purpose. Directs user to `forge provider scaffold` for setup.

`forge provider scaffold <name>` — prints a ready-to-paste TOML block with inline field comments. `--write` patches `forge.toml` directly without overwriting existing profiles.

```
forge provider scaffold openai
```
```toml
[providers.default]
provider = "openai"
model    = "gpt-4o-mini"         # or: gpt-4o, gpt-4-turbo
apiKey   = env("OPENAI_API_KEY") # set this env var before running
# endpoint = "..."               # optional — omit for default OpenAI endpoint
```

These are the primary discoverability path — users should not need docs to configure a known provider.

## What changes from current behaviour

Currently `provider`, `apiKey`, `model`, `endpoint` are `let` bindings in `mission.mcl` read by the runtime from the context bag. That mechanism is replaced by `forge.toml` profiles.

The `let` bindings for domain variables (`goal`, `persona`, etc.) are unchanged — only the four reserved infrastructure bindings (`provider`, `apiKey`, `model`, `endpoint`) are removed from `.mcl`.

## Migration of existing missions

Existing missions with `let provider = ...` bindings continue to work during a transition period — the runtime falls back to context bag resolution if no `forge.toml` is present. A deprecation warning is emitted. Full removal in a subsequent phase.
