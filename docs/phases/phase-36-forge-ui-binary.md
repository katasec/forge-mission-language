# Phase 36 — `forge ui` subcommand + forge-ui binary

## The idea

Bundle the ForgeUI Blazor Server app as a companion binary to the `forge` CLI,
accessible via `forge ui`. One install, two binaries, no separate setup step.

## Why not AOT-bundle into forge itself

Blazor Server depends on ASP.NET Core + SignalR which are not fully
Native-AOT-compatible. Forcing them into the AOT `forge` binary breaks the ILC
linker. This constraint is structural, not accidental.

## Proposed approach

### Two binaries, one release

| Binary        | Publish mode        | Purpose                        |
|---------------|---------------------|--------------------------------|
| `forge`       | Native AOT          | CLI — run, serve, mcp, init    |
| `forge-ui`    | Single-file non-AOT | Blazor Server web UI           |

Both are attached to the same GitHub release. The existing release workflow
already handles multiple artifacts — just add `forge-ui-osx-arm64`,
`forge-ui-linux-x64`, `forge-ui-win-arm64.exe`.

### `forge ui` subcommand

`forge ui` (added to `ForgeMission.Cli`) does three things:
1. Resolves `forge-ui` binary path (same directory as `forge`, or `$PATH`).
2. Launches it as a child process, passing `MissionPath` and `MCL_API_KEY`.
3. Opens `http://localhost:5100` in the default browser.

```
forge ui                                  # uses mission.mcl in cwd
forge ui --mission ./missions/hallucination-guard/mission.mcl
forge ui --port 5200
```

### Install experience

```bash
make install       # installs forge → ~/.local/bin/forge  (unchanged)
make install-ui    # installs forge-ui → ~/.local/bin/forge-ui
```

Or a combined `make install-all`.

## Longer-term alternative: Blazor WASM embedded in forge

If ForgeUI is ever ported to Blazor WASM, the compiled artefacts (HTML + JS +
WASM) can be embedded as `EmbeddedResource` in the AOT `forge` binary and served
from a minimal Kestrel host. This gives a true single-binary experience at the
cost of a ~2MB WASM payload and the complexity of the WASM port.

Not worth doing until the UI is stable.

## Status

- [ ] `forge ui` subcommand in ForgeMission.Cli
- [ ] `make install-ui` target
- [ ] GitHub Actions: attach `forge-ui-*` artifacts to release
- [ ] README: update install instructions
