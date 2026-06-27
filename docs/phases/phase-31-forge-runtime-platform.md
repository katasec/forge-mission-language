# Phase 31 — Forge Generate & Capability Packaging

> **Status: Design**
> **Depends on:** Phase 19 (forge serve, OaiServer), Phase 25 (forge.toml, two-file model), Phase 29 (UC reference missions)
> **Purpose:** Give operators a way to deploy MCL missions to production Kubernetes clusters
> without introducing a proprietary runtime. Forge generates standard K8s manifests.
> The operator applies them via their own tooling (kubectl, ArgoCD, Flux, Helm).

---

## Design philosophy

Forge's job is to generate. The operator's job is to deploy.

These concerns must not be conflated. Every enterprise Kubernetes shop already has
an operational model — GitOps, ArgoCD, Flux, Helm, whatever it is. Forge does not
need to become part of that model. It needs to emit artifacts that fit into it.

This means:

- `forge generate` writes K8s manifests to a directory or stdout. It does not apply them.
- Scheduling is a CronJob `spec.schedule`. Forge writes it; K8s runs it.
- Resource routing (GPU, memory) is node affinity and tolerations. Forge generates
  them from expert frontmatter declarations (Phase 32 Spoke 1). K8s enforces them.
- State management is the developer's concern. If the mission needs Redis, Postgres,
  or a file volume, the developer wires it up. Forge does not prescribe or manage
  state backends — that concurrency model belongs to the application, not the runtime.
- Secrets (API keys, provider credentials) are K8s Secrets or whatever the operator
  uses (Vault, external-secrets-operator). Forge generates the env var references;
  the operator populates the values.

> ~~Orleans~~ — the original design proposed an Orleans silo as the runtime substrate.
> This is retired. Orleans solves problems (virtual actor identity, sub-millisecond
> activation, managed grain lifecycle) that do not apply to MCL's current workloads,
> which are batch and scheduled pipelines. K8s already provides scheduling, isolation,
> resource routing, and observability — and the target market already operates it.
> Introducing a proprietary runtime would duplicate that infrastructure and add an
> operational dependency that Forge cannot justify owning. If a managed Forge Cloud
> offering becomes a future product decision, Orleans would be the right backend for
> that hosting tier. It is not the right substrate for self-hosted enterprise deployment.

---

## What `forge generate` produces

Given a `forge.toml` with schedule and agent declarations, `forge generate` emits
standard Kubernetes manifests into a `./k8s/` directory (or stdout with `--stdout`):

| Source declaration | Generated manifest |
|---|---|
| `[schedule]` mission | `CronJob` — one per entity instance |
| `[agents]` mission | `Deployment` running `forge serve` |
| Expert `resources.gpu` | `nodeAffinity` + `tolerations` + resource `limits` |
| Expert `resources.gpu_memory` | `resources.limits.nvidia.com/gpu-memory` |
| `forge.toml [generate.k8s]` | `namespace`, image ref, pull policy |

The generated YAML is plain Kubernetes — no CRDs, no Forge-specific operators.
Any operator who can read K8s YAML can read, review, and modify it. This is
intentional: the manifest is the operator's artifact, not Forge's.

---

## forge.toml additions

```toml
[generate.k8s]
namespace   = "forge-missions"
image       = "ghcr.io/katasec/forge-runtime:latest"
pull_policy = "IfNotPresent"

# Scheduled mission — generates one CronJob
[[generate.k8s.scheduled]]
mission  = "./missions/trade-signal/mission.mcl"
schedule = "*/15 9-16 * * 1-5"   # standard cron expression

# Served mission — generates a Deployment running forge serve
[[generate.k8s.served]]
mission  = "./missions/security-review/mission.mcl"
name     = "security-review"
replicas = 1
```

`forge generate` produces one CronJob manifest. If the operator needs to run the
same mission for multiple entities (e.g. multiple tickers), they duplicate and edit
the generated manifest as they would any K8s resource. That is standard K8s practice
and an operator concern — not a Forge concern.

---

## Resource routing — connection to Phase 32

Expert frontmatter declared in Phase 32 Spoke 1:

```markdown
---
name: VisualClassifier
kind: exec
resources:
  gpu: "8Gi"
---
```

`forge generate` reads these declarations and emits the corresponding K8s scheduling
constraints automatically:

```yaml
affinity:
  nodeAffinity:
    requiredDuringSchedulingIgnoredDuringExecution:
      nodeSelectorTerms:
      - matchExpressions:
        - key: nvidia.com/gpu
          operator: Exists
tolerations:
- key: nvidia.com/gpu
  operator: Exists
  effect: NoSchedule
resources:
  limits:
    nvidia.com/gpu: 1
    memory: "8Gi"
```

The expert author declares what the workload needs. The operator owns the cluster
configuration. `forge generate` is the translation layer between them.

`gpu: none` (or omitted) generates no affinity constraints.
`gpu: any` generates the affinity and toleration but no memory limit.

---

## State management

Forge does not manage state. This is a deliberate boundary.

If a mission needs to persist state across runs (previous signal, audit history,
entity context), the developer chooses their own backend — Postgres, Redis, SQLite,
a file volume — and wires it up in the process expert or via environment variables.
The concurrency model (optimistic locking, Redis SETNX, Postgres advisory locks)
is the developer's concern, not the runtime's.

`forge generate` can emit a `PersistentVolumeClaim` reference if declared in
`forge.toml`, but it does not create or manage the backing storage.

The rationale: state backends are a solved problem with mature tooling. Prescribing
one would constrain the developer unnecessarily and add an operational dependency
to Forge that has no business being there.

---

## The OAI endpoint / Open WebUI story

`forge serve` (Phase 19) remains the mechanism for serving missions as OAI-compatible
endpoints. For production, `forge generate` emits a `Deployment` manifest that runs
`forge serve` inside a container. The operator exposes it via their ingress of choice.

Open WebUI (or any OAI client) points at the ingress address. The capability catalog
(`/v1/models`) returns the agent names declared in `forge.toml [agents]`. The user
picks a capability; the OAI request routes to the right mission. No change to the
OAI layer — it runs inside a standard K8s pod.

```
Open WebUI
    |
    v
K8s Ingress (operator-managed)
    |
    v
Deployment: forge-serve (generated by forge generate)
    |
    v
forge serve (Phase 19) — OAI routing to named missions
```

---

## `forge publish` — OCI capability packaging

Unchanged from original design. A mission + its resolved experts packaged as a
single OCI artifact:

```bash
forge publish security-review:v1.0 --registry ghcr.io/myorg
```

The generated Deployment manifest can reference a published OCI capability directly:

```toml
[[generate.k8s.served]]
capability = "ghcr.io/myorg/security-review:v1.0"
name       = "security-review"
```

This closes the full lifecycle:

```
Author → Mission → forge publish → OCI registry
                                        |
                                   forge generate
                                        |
                                   K8s manifests → operator applies → running capability
```

---

## Hub + Spokes

| Spoke | Description |
|---|---|
| 1 | `forge generate` verb — reads forge.toml, walks mission + expert graph, writes K8s YAML to `./k8s/` |
| 2 | CronJob generation — `[generate.k8s.scheduled]` → CronJob per entity instance |
| 3 | Deployment generation — `[generate.k8s.served]` → Deployment + Service running `forge serve` |
| 4 | Resource/affinity generation — reads Phase 32 expert frontmatter `resources:`, emits nodeAffinity + tolerations |
| 5 | `forge publish` + OCI capability packaging — mission as OCI artifact, registry push |
| 6 | `forge dev start / stop` — local kind cluster for testing generated manifests (see below) |
| 7 | MVP proof — `forge generate` on Phase 29 UC missions, `forge dev start`, validate end-to-end locally |

### Spoke 6 — `forge dev start / stop`

A local development convenience that spins up a [kind](https://kind.sigs.k8s.io/)
(Kubernetes in Docker) cluster, runs `forge generate`, and applies the manifests —
giving the developer a real K8s environment to test against without touching a
production cluster or going through a GitOps pipeline.

This is explicitly a **dev-only verb**. It does not set a precedent for `forge deploy`
in production. The separation of concerns is preserved: `forge generate` still only
generates; `forge dev start` wraps it for local iteration.

Follows the same pattern as Phase 23 (`forge agent start`, `forge webui start`):
prereq check → Docker/kind bootstrap → apply → report status.

```bash
forge dev start     # start kind cluster, generate manifests, apply, report ready
forge dev stop      # delete the kind cluster
forge dev status    # show running pods and CronJob schedules in the dev cluster
```

**`forge dev start` sequence:**

1. Prereq check — `kind` and `kubectl` present; Docker running. Spectre.Console
   TUI output consistent with Phase 23 prereq checker.
2. `kind create cluster --name forge-dev` if not already running.
3. Load the forge-runtime container image into the kind cluster
   (`kind load docker-image`) — avoids registry round-trip in dev.
4. Run `forge generate --stdout` internally and pipe to `kubectl apply -f -`
   against the kind context (`--context kind-forge-dev`).
5. Wait for pods/CronJobs to reach ready state; stream status via Spectre.Console.
6. Print a summary: which CronJobs are scheduled, which Deployments are running,
   how to access any served endpoints.

**`forge dev stop` sequence:**

1. `kind delete cluster --name forge-dev`
2. Confirm deletion; note that any state written by the workload to non-persistent
   volumes is gone.

**What this is not:**

- Not a production deployment path.
- Not a substitute for the operator's GitOps workflow.
- Not a managed state backend — any state the workload needs in dev is the
  developer's own concern (e.g. a local Redis via Docker Compose alongside the
  kind cluster).

**Prerequisites declared in docs, not enforced silently:**

- `kind` — `brew install kind` / `go install sigs.k8s.io/kind`
- `kubectl` — assumed present (same prereq as `forge generate`)
- Docker — running locally

---

## What is NOT in scope

- `forge deploy` — Forge does not call `kubectl apply`. The operator does. This is a firm boundary.
- State backends — not Forge's concern. Developer wires their own.
- Secrets population — Forge emits references (env var names); the operator populates values.
- Ingress / networking — operator concern.
- ArgoCD / Flux Application CRDs — operator's GitOps concern.
- Multi-cluster — single manifest output; multi-cluster is the operator's routing concern.
- Custom K8s operators or CRDs — standard K8s resources only.

---

## Open design questions

| # | Question | Notes |
|---|---|---|
| 1 | What is the container image for `forge-runtime`? | The AOT `forge` binary packaged in a minimal Linux container. Needs a `Dockerfile` and a release pipeline. `ghcr.io/katasec/forge-runtime` is the natural home. |
| 2 | How are provider credentials injected into generated manifests? | Generate env var references (`FORGE_API_KEY`, `FORGE_PROVIDER`) as `valueFrom.secretKeyRef`. Operator creates the K8s Secret. Secret name configurable in `[generate.k8s]`. |
| 3 | Should `forge generate` emit a single combined YAML or one file per resource? | One file per resource type is more GitOps-friendly (easier to diff, review, and patch). `--combined` flag for operators who prefer a single apply. |
| 4 | How does entity instance naming work for long ticker/entity strings? | Sanitise to DNS-label format (`toLower`, replace non-alphanumeric with `-`, truncate to 52 chars to leave room for resource type suffix). |
| 5 | Should generated manifests include `forge generate` provenance annotations? | Yes — `forge.katasec.com/generated-by`, `forge.katasec.com/mission`, `forge.katasec.com/version` as K8s annotations. Makes manifest origin inspectable without reading the YAML content. |
