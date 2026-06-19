# Phase 25 — Spoke 2: forge.toml

## Status: Todo

## Overview

`forge.toml` is the manifest file for a mission. It declares where experts come from and how providers are configured. It is the `Cargo.toml` / `Gemfile` analogue for MCL.

One `forge.toml` per mission directory. Parsed by `forge init` and `forge run`.

## Schema

```toml
[experts]
KubernetesArchitect = "ghcr.io/katasec/forge-kubernetes-architect@0.1.0"
SecurityArchitect   = "ghcr.io/katasec/forge-security-architect@0.1.0"
PrincipalReviewer   = "ghcr.io/katasec/forge-principal-reviewer@0.1.0"

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

### `[experts]` section

Each entry maps an expert name (PascalCase) to an OCI reference in `registry/image@version` format.

Only OCI experts are declared here. Local experts (directory-based under `experts/`) are resolved by name without any declaration — they do not appear in `forge.toml`.

### `[providers.name]` sections

Each section defines a named provider profile. The `default` profile is used when no `with { provider = "..." }` override is present on a step.

Supported fields per profile:

| Field | Required | Description |
|-------|----------|-------------|
| `provider` | Yes | Provider type: `openai`, `anthropic`, `azure`, `ollama` |
| `model` | Yes | Model name passed to the provider |
| `apiKey` | Yes (except ollama) | API key — use `env("VAR")` to read from environment |
| `endpoint` | No | Base URL override — required for `azure`, optional otherwise |

`env("VAR")` in `forge.toml` follows the same semantics as in `mission.mcl`.

## Validation rules

- `[experts]` section is optional — a mission may use only local experts
- `[providers.default]` is required if any step uses an LLM
- A profile named in a `with { provider = "name" }` clause must exist in `forge.toml`
- Unknown fields in a provider profile are an error — no silent ignore
- Unknown `provider` values (not in the built-in registry) are a hard error in Phase 25 — third-party provider registration is a future extension point

## Provider schema validation

Field validation delegates to `ProviderSchemaRegistry` (defined in Spoke 5). `ForgeTomlReader` calls the registry to check required fields for each `[providers.*]` section and emits schema-aware errors per Decision 1 format. Spoke 2 owns the TOML parsing; Spoke 5 owns the schema knowledge.

## Implementation

- Add `Tommy` or `Tomlyn` TOML parser package (AOT-safe, verify IL trimming)
- `ForgeToml` POCO with `[JsonSerializable]` / STJ source gen for any JSON paths; TOML deserialization via the chosen library
- `ForgeTomlReader` — reads and validates `forge.toml`, returns a typed `ForgeManifest`; delegates provider field validation to `ProviderSchemaRegistry`
- `ForgeManifest` exposes `IReadOnlyDictionary<string, string> Experts` and `IReadOnlyDictionary<string, ProviderProfile> Providers`
- Validate at startup in both `forge init` and `forge run` — fail fast with a clear error if `forge.toml` is missing or malformed

## File location

`forge.toml` must be in the same directory as `mission.mcl`. The CLI resolves it relative to the mission file path.
