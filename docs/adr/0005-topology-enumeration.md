# ADR 0005 – Optional Shard Map Enumeration Interface

Date: 2025-09-10

## Status

Accepted

## Context

Migration planning previously relied on synthetic `TopologySnapshot` construction in samples. Real deployments need to derive the "from" topology from the authoritative shard map store (in-memory, SQL, etc.). The existing `IShardMapStore<TKey>` abstraction intentionally only exposes per-key operations to keep routing surface minimal. Introducing enumeration directly there would be a breaking change and force all implementations to support potentially heavy operations.

## Decision

Introduce a *separate*, opt-in interface `IShardMapEnumerationStore<TKey>` that extends `IShardMapStore<TKey>` with:

```csharp
IAsyncEnumerable<ShardMap<TKey>> EnumerateAsync(CancellationToken ct = default);
```

A helper (`TopologySnapshotFactory.ToSnapshotAsync`) materializes a `TopologySnapshot<TKey>` from any enumeration-capable store with:

- Cancellation support
- Hard key-count cap (default 1,000,000) to guard memory
- Activity tracing (`shardis.snapshot.enumerate`) recording elapsed + key count

In-memory and SQL stores implement the interface. Samples now use enumeration for the source snapshot; synthetic topologies are retained only for target (planned) states.

## Rationale

- **Non-breaking**: Existing stores remain valid; only stores that can safely enumerate opt in.
- **Separation of concerns**: Routing stays lean; migration/planning code can detect enumeration support via pattern check.
- **Streaming-friendly future**: The async enumerable allows later incremental / paged planning without forcing full materialization (future segmented planner).
- **Operational safety**: Cap + cancellation avoids runaway memory consumption for extremely large key spaces.

## Alternatives Considered

1. Add `Enumerate` to `IShardMapStore<TKey>` directly – rejected (breaking + forces all providers to implement heavy operation).
2. Provide only a synchronous `IEnumerable` – rejected (no cancellation, blocks threads for I/O backends).
3. Expose a dedicated snapshot service abstraction – overkill for current needs; interface extension is simpler.

## Consequences

- Public API expanded with a new interface and new extension method in migration assembly.
- Consumers must feature-detect enumeration (`store is IShardMapEnumerationStore<TKey> e`).
- Future planner enhancements can directly accept an `IAsyncEnumerable<ShardMap<TKey>>` to avoid intermediate dictionary.

## Future Work

- Streaming / segmented planner that consumes enumeration without full snapshot.
- Delta planner computing target shard directly on copy instead of pre-computing the target snapshot.
- Additional durable providers (Redis, etc.) implementing enumeration with version verification to detect drift.

## References

- ADR 0002 – Key migration execution (planning/execution phases)
- `TopologySnapshotFactory` implementation (cancellation, cap, tracing)
