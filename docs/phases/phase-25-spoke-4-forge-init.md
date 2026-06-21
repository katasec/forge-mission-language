# Phase 25 — Spoke 4: forge init

## Status: Done

## Behaviour

`forge init` reads `forge.toml`, pulls OCI experts into `~/.forge/experts/`, and writes `mcl.lock`.

```bash
forge init                    # uses mission.mcl in current directory
forge init path/to/mission.mcl
```

## Steps

1. Read and validate `forge.toml` from the mission directory
2. For each entry in `[experts]`:
   a. Check if already cached at `~/.forge/experts/<registry>/<Name>@<version>/`
   b. If cached and hash matches `mcl.lock` — skip (no network call)
   c. If missing or stale — pull from OCI registry, write to cache
3. Write (or update) `mcl.lock` with resolved paths and SHA256 hashes
4. Print summary to stderr

## Output

```
Resolving experts...
  ✓ KubernetesArchitect   cached   ghcr.io/katasec/forge-kubernetes-architect@0.1.0
  ✓ SecurityArchitect     pulled   ghcr.io/katasec/forge-security-architect@0.1.0
  ✓ PrincipalReviewer     pulled   ghcr.io/katasec/forge-principal-reviewer@0.1.0

mcl.lock written. Run 'forge run' to execute the mission.
```

## `mcl.lock` format

```toml
[[expert]]
name     = "KubernetesArchitect"
registry = "ghcr.io/katasec/forge-kubernetes-architect"
version  = "0.1.0"
path     = "~/.forge/experts/ghcr.io/katasec/forge-kubernetes-architect@0.1.0/expert.md"
sha256   = "abc123..."

[[expert]]
name     = "SecurityArchitect"
registry = "ghcr.io/katasec/forge-security-architect"
version  = "0.1.0"
path     = "~/.forge/experts/ghcr.io/katasec/forge-security-architect@0.1.0/expert.md"
sha256   = "def456..."
```

Local experts are not recorded in `mcl.lock` — they are always resolved from disk at runtime.

## What changes from current `forge init`

- Reads expert sources from `forge.toml` instead of `mission.mcl`
- No longer parses `expert … from … version …` declarations from `.mcl`
- Everything else (OCI pull, cache location, lock file write) is unchanged in principle
