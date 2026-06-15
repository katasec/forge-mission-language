# Phase 11 — OCI Source Support

**Status:** Blocked

## Goal

Allow `use "oci://..."` declarations to resolve experts from an OCI registry at `fml init` time,
so missions can depend on versioned, remotely-hosted expert bundles.

## Current State

The grammar parses `use "oci://..."` correctly. The `SourceResolver` throws `FMS010` at runtime
because no OCI pull logic is implemented. Intentionally blocked pending design of the registry
contract and authentication story.

## Blocked On

- OCI registry contract: what does an expert bundle look like as an OCI artifact?
- Authentication: how does `fml init` authenticate to private registries?
- Caching: where do pulled experts land on disk? How is staleness managed?

## When Unblocked

1. Implement OCI pull in `SourceResolver` — detect `oci://` prefix, pull to a local cache dir
2. Wire cache path into `LockFile` alongside the existing `path` field
3. Add `fml init --refresh` to force re-pull even when lock file is current
4. Error codes: `FMS010` (OCI pull failed), `FMS011` (auth error)
