# Phase 25 — Spoke 3: Expert Resolution

## Status: Done

## Resolution order

For each expert name referenced in `mission.mcl`, the runtime resolves in this order:

```
1. <mission-dir>/experts/<Name>/expert.md     ← local, always wins
2. ~/.forge/experts/<registry>/<Name>@<ver>/  ← global cache
3. Error — not found, tell user to run forge init
```

Local experts shadow global cache silently. This is intentional — a developer writing a local expert means to override. The shadow is a feature, not a footgun.

## Global cache structure

```
~/.forge/
  experts/
    ghcr.io/
      katasec/
        forge-kubernetes-architect@0.1.0/
          expert.md
        forge-security-architect@0.1.0/
          expert.md
```

## `mcl.lock` role

`mcl.lock` records the resolved registry path and content hash for every OCI expert. On `forge run`, the runtime:

1. Reads `mcl.lock`
2. Verifies the cached file at the recorded path matches the recorded hash
3. If hash mismatch or file missing — error, tell user to run `forge init`

No network calls during `forge run`. The lock file makes resolution deterministic and offline.

## Error messages

| Situation | Message |
|-----------|---------|
| Not in local or cache | `Expert 'Name' not found. Run 'forge init' to pull from registry.` |
| In `forge.toml` but not in cache | `Expert 'Name' declared in forge.toml but not cached. Run 'forge init'.` |
| Hash mismatch | `Expert 'Name' cache is corrupted or outdated. Run 'forge init' to re-pull.` |

## `--verbose` flag

`forge run --verbose` prints resolution source for each expert before execution:

```
[forge] KubernetesArchitect  → local    (experts/KubernetesArchitect/expert.md)
[forge] SecurityArchitect    → cache    (~/.forge/experts/ghcr.io/katasec/forge-security-architect@0.1.0)
[forge] PrincipalReviewer    → cache    (~/.forge/experts/ghcr.io/katasec/forge-principal-reviewer@0.1.0)
```

Always goes to stderr. Never pollutes the mission output stream.

## Implementation

- `ExpertResolver` — takes mission directory path + `ForgeManifest`, returns `ExpertDefinition` for each name
- Resolution is eager at startup — all experts resolved before any step runs. Fail fast if any are missing.
- Hash verification uses SHA256 of `expert.md` content, recorded in `mcl.lock`
- `--verbose` wired through `RunOptions` to `ExpertResolver`
