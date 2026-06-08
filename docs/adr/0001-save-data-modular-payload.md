# ADR-0001: Save data model — modular opaque payload

- **Status:** Accepted
- **Date:** 2026-06-08
- **Deciders:** project owner
- **Supersedes:** —

## Context

The save subsystem must persist game state locally and synchronize it with a .NET backend. Two structural approaches are common in Unity games:

1. **Modular opaque payload** — `SaveData` holds a `Dictionary<string, ModulePayload>` where each entry is an opaque JSON string with a per-module schema version. The Save assembly knows nothing about feature data; each feature owns its model and (de)serializes against its own type.
2. **Typed entry classes** — a base `AbstractEntry<TData>` with one concrete subclass per data domain (resources, inventory, quests, etc.). All entry types live in a shared assembly and the Save core has compile-time knowledge of every field.

Constraints that shaped the choice:

- MVP scope, single developer.
- Target uncompressed save size < 30 KB.
- Backend is a separate .NET service that exchanges JSON over REST. No shared C# code between client and server is planned.
- Strict modular asmdef layout (`docs/ASMDEF_RULES.md`): features must not depend on each other directly, and infrastructure must not depend on features.
- Schemas are expected to evolve fast as features land and reshape.
- Need to ship and iterate; not optimizing for theoretical maximum throughput.

## Decision

Use the **modular opaque payload** approach.

Concretely:

- `SaveData { MetaData Meta, Dictionary<string, ModulePayload> Modules }`
- `ModulePayload { int Version, string Json }`
- Each feature serializes its own model into a `ModulePayload` keyed by a feature-owned constant string.
- The `Save` assembly has zero references to any feature assembly.
- Migration of an individual module's schema is the responsibility of that feature, gated by `ModulePayload.Version`.

## Consequences

### Positive

- **Strict feature isolation** — Save has no compile-time knowledge of any feature, satisfying the asmdef rules with no `.API` plumbing required.
- **Fast iteration** — adding a new persisted feature is a one-place change inside that feature; no edits to a central God Object.
- **Per-module schema versioning** — each module evolves on its own clock; migrations are local.
- **JSON debuggability** — saves are human-readable, simplifying field investigations and recovery.
- **Backend simplicity** — the server treats the save as a blob with metadata; no shared types or schema registry needed.
- **Smaller blast radius on refactors** — renaming a field in one feature does not require recompiling a shared schema assembly.

### Negative

- **No cross-feature type-level guarantees** — if feature A needs to read feature B's persisted data, this must go through a runtime contract (an `.API` interface), not direct field access.
- **No automatic per-field delta sync** — the whole module payload is sent on each save; this is acceptable while saves remain small.
- **Module schema versioning is decentralized** — there is no single place to audit "what does the save look like across all features". Mitigated by payload telemetry that logs module key sizes on every save.
- **Possible double serialization on hot paths** — reading a module deserializes JSON; updating a module serializes back. Acceptable for current scale; revisit if profiling shows hotspots.

## When to revisit

The modular payload approach should be re-evaluated if any of the following becomes true:

- **Backend starts sharing C# code with the client.** End-to-end type safety on persisted state becomes a major win and tilts the balance toward typed entries.
- **Save size grows past ~100 KB.** At that point delta synchronization stops being optional, and delta requires the core to know the structure.
- **Server-authoritative game logic appears.** If the backend has to read or validate specific fields of the save (anti-cheat, server-side rewards, etc.), opaque JSON becomes friction.
- **The number of persisted features grows past ~30 and onboarding becomes painful.** Having a single typed registry of all save fields gives discoverability that scattered module keys lack.

If two or more of those conditions hold, migration to a typed entry model is justified. Until then, the modular payload remains the right default.

## Implementation notes

- The decision is implemented in `Assets/Game/Features/Save/`.
- Feature-side conventions for module keys, schema versions, and migrations are documented per-feature, not centrally.
- Payload telemetry (size breakdown by module key) is emitted on each save to keep visibility into growth.
