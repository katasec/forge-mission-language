# Phase 11 — OCI Source Support

**Status:** Ready to implement

**Prerequisite resolved:** [`katasec/oci-client-dotnet`](https://github.com/katasec/oci-client-dotnet) v0.1.0 is published to GitHub Packages (`nuget.pkg.github.com/katasec`). All integration tests pass (pull manifest, pull blob, push/pull round-trip, bearer auth).

---

## Goal

Allow expert blocks to declare an OCI registry source. `forge init` pulls the remote
expert into `./experts` so the runtime sees it identically to a locally-authored expert.

---

## Syntax

```fsharp
let apiKey = env("MCL_API_KEY")
let model  = env("MCL_MODEL", "gpt-4o-mini")
let goal   = "Design a production-grade K8s build operator"
let persona = "Principal SRE, Tekton specialist"

expert KubernetesArchitect =
    from "ghcr.io/katasec/forge-kubernetes-architect"
    version "0.1.0"

expert SecurityArchitect =
    from "ghcr.io/katasec/forge-security-architect"
    version "0.1.0"

expert PrincipalReviewer =
    from "ghcr.io/katasec/forge-principal-reviewer"
    version "0.1.0"

mission BuildOperatorDesign(goal, persona) =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer with { style = "terse ADR" }

output(BuildOperatorDesign)
```

OCI-sourced and local experts compose identically in the pipeline.

---

## Registry — ghcr.io/katasec

The canonical registry is **GitHub Container Registry** under the `katasec` org.

All forge-maintained experts are published at `ghcr.io/katasec/forge-<name>:<version>` and are **public** (no auth needed for `forge init` pull).

### Published packages (v0.1.0)

| Package | OCI ref |
|---------|---------|
| KubernetesArchitect | `ghcr.io/katasec/forge-kubernetes-architect:0.1.0` |
| SecurityArchitect | `ghcr.io/katasec/forge-security-architect:0.1.0` |
| PrincipalReviewer | `ghcr.io/katasec/forge-principal-reviewer:0.1.0` |
| PitchDrafter | `ghcr.io/katasec/forge-pitch-drafter:0.1.0` |
| PitchCritic | `ghcr.io/katasec/forge-pitch-critic:0.1.0` |
| PitchJudge | `ghcr.io/katasec/forge-pitch-judge:0.1.0` |
| PitchReviser | `ghcr.io/katasec/forge-pitch-reviser:0.1.0` |
| PitchWriter | `ghcr.io/katasec/forge-pitch-writer:0.1.0` |
| ContextOverloaded | `ghcr.io/katasec/forge-context-overloaded:0.1.0` |
| QualityJudge | `ghcr.io/katasec/forge-quality-judge:0.1.0` |
| ContextOverloadedNaive | `ghcr.io/katasec/forge-context-overloaded-naive:0.1.0` |

Pushed using `oras` CLI. Pull verified unauthenticated via anonymous GHCR bearer token.

---

## OCI client library

**[`katasec/oci-client-dotnet`](https://github.com/katasec/oci-client-dotnet) v0.1.0**

- `net10.0`, `IsAotCompatible=true`
- STJ source generation throughout — no bare `JsonSerializerOptions`
- `OciClient.PullExpertAsync(registry, name, tag)` — manifest + blob fetch in one call
- `OciClient.PushExpertAsync(registry, name, tag, content)` — blob upload + manifest push
- `BearerAuth` — automatic 401 → Basic-auth token fetch → Bearer JWT retry

**Auth flow (discovered during testing):**

GHCR does not accept a GitHub PAT directly as a Bearer token. The correct flow:
1. First request → 401 with `WWW-Authenticate: Bearer realm=...,service=...,scope=...`
2. Fetch scoped JWT: `GET {realm}?service=...&scope=...` with `Authorization: Basic base64("token:{PAT}")`
3. Retry original request with `Authorization: Bearer {JWT}`

`BearerAuth` implements this automatically. Pass the PAT as `credential` to `OciClient`.
Public packages get an anonymous JWT (no credential needed).

**Push quirk — relative Location header:**

GHCR's blob upload `POST /v2/{name}/blobs/uploads/` returns a relative `Location` header.
`OciClient` resolves it against the registry base before appending the digest. This is handled in `PushBlobAsync`.

**Path annotation — ignore on pull:**

When blobs are pushed via `oras` CLI, the layer gets an `org.opencontainers.image.title`
annotation containing the original source path (e.g. `/Users/.../expert.md`). `OciClient`
ignores this annotation entirely — it fetches the blob by digest and writes it as
`expert.md` in the target directory.

---

## Design decisions

### 1. Grammar

Add `from` and `version` as keywords and `ociSource` as an alternative body for `expert`:

```antlr
FROM    : 'from'    ;
VERSION : 'version' ;

expert
    : EXPERT UPPER_ID params? EQUALS (ociSource | pipeline)
    ;

ociSource
    : FROM STRING VERSION STRING
    ;
```

`ociSource` and `pipeline` are mutually exclusive — an expert is either remotely sourced or
locally composed, never both.

### 2. AST

```csharp
public record OciSource(string Registry, string Version);

// Pipeline becomes nullable — null when the expert is OCI-sourced
public record ExpertDeclaration(
    string Name,
    IReadOnlyList<string> Params,
    Pipeline? Pipeline,
    OciSource? Source)
    : Declaration(Name);
```

Exactly one of `Pipeline` and `Source` is non-null.

### 3. Resolution — pull into `./experts`

`forge init` pulls OCI experts directly into `./experts`, the same directory used for
locally-authored experts. After init, the runtime sees no difference:

```
experts/
  KubernetesArchitect/     ← pulled from ghcr.io/katasec/forge-kubernetes-architect:0.1.0
    expert.md
  SecurityArchitect/       ← pulled from ghcr.io/katasec/forge-security-architect:0.1.0
    expert.md
  LocalReviewer/           ← locally authored
    expert.md
```

`SourceResolver` is unchanged — it always reads `./experts`.

**Vendoring vs gitignore — deferred.** Whether to commit pulled experts (vendoring) or
gitignore them and always pull fresh is an open question. The lock file's `source` field
records the origin either way, so the decision can be made from experience after initial
use.

### 4. Lock file

Lock file format is unchanged. `Source` records the canonical OCI ref; `Path` is always
a relative path under `./experts`, identical to local experts:

```yaml
# mcl.lock
version: 1
experts:
  KubernetesArchitect:
    source: ghcr.io/katasec/forge-kubernetes-architect:0.1.0
    path: experts/KubernetesArchitect/expert.md
  LocalReviewer:
    source: ./experts
    path: experts/LocalReviewer/expert.md
```

No changes to `LockFileIO` or `SourceResolver`.

### 5. OCI artifact format

| Field | Value |
|-------|-------|
| Config mediaType | `application/vnd.forge.expert.config.v1+json` |
| Layer mediaType | `application/vnd.forge.expert.v1` |
| Layer content | Raw UTF-8 `expert.md` bytes (not tarred) |

Pull = fetch manifest → find first layer by `application/vnd.forge.expert.v1` mediaType → fetch blob bytes → write as `expert.md`.

The layer annotation `org.opencontainers.image.title` is ignored on pull — always write to `<expertsDir>/<ExpertName>/expert.md`.

### 6. Authentication

**forge login:**
```bash
forge login ghcr.io --token <GITHUB_TOKEN>
```
Writes to `~/.forge/credentials.json`:
```json
{ "credentials": { "ghcr.io": { "token": "gho_..." } } }
```
Uses STJ source generation (AOT-safe). No bare `JsonSerializerOptions`.

**CI/CD:**
`FORGE_REGISTRY_TOKEN` env var is checked when no credentials file entry exists for the registry.

**Public registries:** no auth required. All `ghcr.io/katasec/forge-*` packages are public.

**Credential lookup order:**
1. `~/.forge/credentials.json` entry for the registry host
2. `FORGE_REGISTRY_TOKEN` env var
3. Unauthenticated (anonymous GHCR bearer token — works for public packages)

### 7. forge run — OCI not-pulled check

`forge run` fails fast if a declared OCI expert hasn't been pulled yet:

```
error: MCL010 Expert 'KubernetesArchitect' not resolved — run 'forge init' to pull remote experts
```

This check happens after parse, before validation — iterates `ExpertDeclaration`s with a non-null `Source` and asserts `./experts/<Name>/expert.md` exists on disk.

---

## Implementation tasks

1. **NuGet reference** — add `Katasec.OciClient` v0.1.0 to `ForgeMission.Cli.csproj` from `nuget.pkg.github.com/katasec`.

2. **Grammar** — add `FROM`, `VERSION` keywords and `ociSource` rule to `MclGrammar.g4`. Regenerate the ANTLR parser.

3. **AST** — add `OciSource` record; make `ExpertDeclaration.Pipeline` nullable; add `ExpertDeclaration.Source`.

4. **Visitor** — update `MclAstBuilder.VisitExpert` to handle `ociSource` alternative.

5. **Credentials** — `ForgeCredentials` / `RegistryCredential` POCOs with STJ source gen; `CredentialStore` reads/writes `~/.forge/credentials.json`.

6. **forge init** — after resolving local experts, iterate `ExpertDeclaration`s with a non-null `Source`; skip if `./experts/<Name>/expert.md` already exists (unless `--refresh`); call `OciClient.PullExpertAsync`; write to `./experts/<Name>/expert.md`; then run `SourceResolver` as normal.

7. **forge run** — add Pass 2 check: for each OCI-declared expert, assert the file exists; throw `MCL010` if missing.

8. **forge login** — new CLI verb `forge login <registry> --token <tok>` writing to `CredentialStore`.

9. **forge init --refresh** — force re-pull even when `./experts/<Name>/expert.md` already exists.

10. **ExpertLoader.Validate** — guard the `e.Pipeline.Steps` access for null `Pipeline` (OCI experts have no sub-pipeline).

11. **Error codes** — `MCL010` (OCI expert not pulled — run forge init), `MCL011` (OCI pull failed).

---

## AOT constraints

- `Katasec.OciClient` is `net10.0` + `IsAotCompatible=true` — safe to reference directly.
- `ForgeCredentials` / `RegistryCredential` need `[JsonSerializable]` in a new `ForgeJsonContext`. No bare `JsonSerializerOptions`.
- No new YamlDotNet POCOs — `LockFile`/`LockFileExpert` are unchanged.

---

## New error codes

| Code | Meaning |
|------|---------|
| MCL010 | OCI expert not pulled — run `forge init` |
| MCL011 | OCI pull failed (network error, 404, bad artifact, auth denied) |
