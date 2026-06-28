# Phase 33 — MCP Server (`forge mcp`)

**Status:** Done

## Problem

MCL missions run from the CLI (`forge run`) or as an HTTP agent (`forge serve`).
Neither integrates cleanly with AI-native tools like Claude Desktop — you would need
a hand-written client, a Node.js sidecar, or manual copy-paste to bridge the gap.

## Solution

Add `forge mcp <mission.mcl>` — a subcommand that starts a **stdio MCP server**
exposing the mission as a single callable tool. Any MCP-aware client (Claude Desktop,
Claude Code, or any future tool) can invoke the mission directly with zero extra
runtime dependencies.

```json
// claude_desktop_config.json
{
  "mcpServers": {
    "what-is-next": {
      "command": "forge",
      "args": ["mcp", "/path/to/missions/what-is-next/mission.mcl"]
    }
  }
}
```

Restart Claude Desktop → the mission appears as a native tool in the UI.

## Design decisions

### Why stdio MCP (not SSE / HTTP)?
Claude Desktop's MCP integration uses stdio transport for local processes. No port
management, no firewall rules, no discovery — the host starts the process and owns
the pipe.

### Why not a Node.js sidecar?
A sidecar would add a second runtime dependency (Node.js) unrelated to MCL. Anyone
using forge missions from Claude Desktop would need Node installed. The whole point
of the AOT binary is zero runtime dependencies — the MCP server must live inside
`forge`.

### Tool shape
- **Tool name** — mission name (from the `mission` declaration in the `.mcl` file)
- **Tool parameters** — mission inputs (each `let` binding or mission parameter
  becomes a JSON schema property)
- **Tool response** — mission output as a text content block (the same markdown
  `forge run` prints to stdout)

### MCP vs agent endpoint
`forge serve` (HTTP agent) and `forge mcp` (stdio MCP) are complementary:

| | `forge serve` | `forge mcp` |
|---|---|---|
| Transport | HTTP / SSE | stdio |
| Multi-turn | Yes | No (one call, one response) |
| Integration | Custom clients, OAI-compatible | Claude Desktop, MCP-aware tools |
| Use case | Long-running agents, sessions | Mission-as-tool, one-shot queries |

MCP is the right shape for missions that are pure functions: structured inputs → 
structured output. If a mission needs multi-turn interaction with the caller, use
`forge serve`.

### NuGet package
**`ModelContextProtocol` 1.4.0** (official Microsoft SDK).

Both `ModelContextProtocol` 1.4.0 and the community `ModelContextProtocol.NET.Server`
0.3.3-alpha were tested under AOT publish (`make install`) — **both produced zero ILC
warnings**. The official Microsoft package was chosen for long-term maintenance
confidence.

The package requires `JsonTypeInfo` for tool parameter schemas, which aligns with
the existing STJ source-generation pattern already enforced project-wide.

## Motivation / origin

Discovered while demoing the `what-is-next` mission (a one-shot project status
synthesiser). Running it from the CLI works, but the natural integration target is
Claude Desktop — where the user already is. The MCP protocol is the clean bridge:
no new runtimes, no new concepts, just `forge` doing one more thing it already knows
how to do (run a mission) over a different transport.

## Spokes

| Spoke | Description | Status |
|-------|-------------|--------|
| 1 | MCP server core (stdio transport, tool registration, request/response loop) | Done |
| 2 | Input schema generation from mission parameters | Done |
| 3 | `forge.toml` provider config wiring inside `forge mcp` context | Done |
| 4 | `what-is-next` as the reference demo mission + `.mcp.json` project config | Done |
| 5 | Multi-mission server (`forge mcp missions/` — expose a directory as a toolset) | Deferred |

## Also shipped in this phase

- **`ExecExpertRunner.StreamAsync`** — fixed to yield `envelope.Text` instead of raw
  JSON envelope, so content writers (Open WebUI, CLI streaming) receive plain text.
  `ParseStreamedEnvelope` already handled non-JSON gracefully via its fallback path.
- **`PipelineRunner` null-safety** — `context["output"]` can be null when an exec
  expert writes a null value; changed `last.ToString()!` to `last?.ToString() ?? string.Empty`
  to prevent `NullReferenceException`.
