# ADR 0001: Core Architecture of Shardis

**Status:** Accepted
**Date:** 2025-08-25
**Author:** Jesper Veggerby
**Supersedes:** None
**Superseded by:** None
**Related:** [ADR 0002](./0002-key-migration-execution.md) (Key Migration Execution Model), [Backlog](../backlog.md)

---

## 1. Context

Shardis is a .NET sharding framework providing deterministic routing of logical keys to physical shards, with a focus on correctness, performance, and developer ergonomics. The core must support:

* Deterministic **key → shard** assignment
* Low-churn dynamic topology changes (add/remove shards)
* Pluggable persistence for key→shard assignments
* Streaming, parallel querying across shards
* Observability (metrics) without polluting hot paths
* Extensibility for future concerns (health, migration, weighting) without breaking existing users

This ADR captures the **foundational architectural decisions**. Detailed key migration execution is specified separately in **ADR 0002**.

---

## 2. Problem Statement

We need a stable architectural baseline that:

1. Separates concerns (hashing, routing, persistence, querying, metrics).
2. Ensures thread-safe, lock-minimized routing under concurrency and topology mutation.
3. Avoids coupling domain models with infrastructure logic.
4. Supports multiple persistence backends (Redis, SQL, in-memory).
5. Enables future migration and health-based routing without redesign.

---

## 3. Forces / Constraints

* Determinism over probabilistic/adaptive heuristics (at this stage).
* High read concurrency; writes (assignments, topology mutation) are comparatively rare.
* Minimal allocations in hot routing path.
* No blocking waits over async operations (deadlock avoidance).
* Public API stability and clarity (avoid churn pre-1.0).
* Must function with dozens to low hundreds of shards; replication factor bounded to prevent ring explosion (>10k vnodes rejected).

---

## 4. High-Level Architecture

Component layers:

1. **Model Layer** — immutable value objects (`ShardId`, `ShardKey`) used as identifiers.
2. **Hashing Layer** —

   * `IShardKeyHasher<TKey>` (key → hash)
   * `IShardRingHasher` (shard ID + replica → hash)
     These are decoupled to allow independent tuning.
3. **Routing Layer** — routers (`DefaultShardRouter`, `ConsistentHashShardRouter`) resolve keys to shards via hashing and map store. A shared helper consolidates metrics and CAS logic.
4. **Persistence Layer** — `IShardMapStore<TKey>` for durable key→shard mappings. Implementations (in-memory, Redis, SQL). Provides atomic operations (`TryAssignShardToKey`, `TryGetOrAdd`).
5. **Topology Management** — consistent hashing ring with replication factor. Thread-safe mutation produces immutable snapshot arrays for `O(log n)` lookups.
6. **Querying Layer** — broadcasters and async enumerators (`ShardBroadcaster`, ordered/combined enumerators) for parallel shard operations and streaming merge.
7. **Metrics Layer** — `IShardisMetrics` (no-op default) for counters, decoupled from routing logic.
8. **DI Integration** — `ServiceCollectionExtensions.AddShardis` configures hashers, router, replication factor, map store, metrics.

---

## 5. Public API Surface

Main interfaces and entry points:

* `ShardId`, `ShardKey` (model)
* `IShardKeyHasher<TKey>`, `IShardRingHasher`
* `IShardRouter<TKey>` (resolves key→shard)
* `IShardMapStore<TKey>` (CAS persistence)
* `IShardisMetrics` (counters, gauges)
* `ServiceCollectionExtensions.AddShardis` (DI setup)

---

## 6. Key Decisions

| Decision                                       | Rationale                                          | Alternatives Considered                                           |
| ---------------------------------------------- | -------------------------------------------------- | ----------------------------------------------------------------- |
| Consistent hashing ring + replication factor   | Even distribution, low churn on topology change.   | Rendezvous hashing (simpler weighting, but early complexity).     |
| Snapshot-based ring lookups (immutable arrays) | Lock-free reads; only writers allocate new arrays. | Per-lookup locks (contention), concurrent collections (overhead). |
| Binary search on sorted virtual node keys      | `O(log n)` resolution, minimal allocations.        | Linear scan (slower at scale).                                    |
| Per-key CAS at map store                       | Prevents duplicate assignments & race conditions.  | Global lock (bottleneck).                                         |
| Separation of key vs ring hasher               | Allows tuning independently.                       | Single combined hasher (less flexible).                           |
| Streaming enumeration                          | Low latency, partial results, cancellation.        | Batch gather & merge (higher latency/memory).                     |
| No implicit weighting/adaptive logic (yet)     | Simpler, deterministic baseline.                   | Early weighting (premature).                                      |
| Metrics via interface only                     | Decouples from observability stack.                | Hardcoded OpenTelemetry/Prometheus.                               |
| DI single entry point                          | Predictable config surface.                        | Multiple extension points (confusing).                            |

---

## 7. Concurrency Model

* **Routing reads**: lock-free via immutable ring snapshots (`Volatile.Read` + binary search).
* **Topology mutations**: writer lock builds new ring, atomically publishes snapshot. Mutations are rare compared to reads.
* **Per-key assignment contention**: handled via in-memory per-key lock dictionary (non-durable) + CAS at map store. Future eviction/aging strategy TBD.
* **Querying**: broadcasters spawn per-shard tasks with cooperative cancellation.

---

## 8. Error Handling & Resilience

* Duplicate shard IDs → fail fast at router construction.
* Replication factor bounds enforced.
* Missing shard on existing mapping → fallback reassignment.
* All async paths propagate cancellation.

---

## 9. Extensibility & Safety Rails

* Migration (ADR 0002) plugs into topology snapshot + map store CAS.
* Health policies (`IShardHealthCheck`, `IShardHealthPolicy`) can be layered without changing routers.
* Weighted routing may extend ring hashing later.
* New persistence stores must implement atomic assign + read-first.
* **Non-extensible boundary**: custom routers MUST use the shard map store; bypassing it breaks CAS guarantees.

---

## 10. Non-Goals

* Distributed transactions across shards.
* Automatic/adaptive rebalancing.
* Cross-language portability.
* Multi-region latency-aware routing.

---

## 11. Alternatives Rejected

| Alternative                       | Reason Rejected                          |
| --------------------------------- | ---------------------------------------- |
| Single hash for key & ring        | Less flexible; hard to evolve.           |
| Rendezvous hashing initially      | Too complex; ring sufficient now.        |
| Centralized lock for assignments  | Contention bottleneck.                   |
| Materialized full-result querying | Higher latency & memory.                 |
| Embedded metrics provider         | Ties library to stack, reduces adoption. |

---

## 12. Risks & Mitigations

| Risk                                          | Mitigation                                  |
| --------------------------------------------- | ------------------------------------------- |
| Per-key lock dictionary unbounded growth      | Document behavior; backlog eviction policy. |
| Misconfigured replication factor → large ring | Bound + docs.                               |
| Migration complexity bleeding into routing    | ADR 0002 defines separate boundary.         |
| Hash distribution bias                        | Tests, pluggable hashers.                   |

---

## 13. Open Questions / Deferred

* Migration state machine integration (ADR 0002).
* Checkpoint & rollback semantics (ADR 0002).
* Health degradation handling for existing vs new keys.
* Weighted capacity adjustments vs full remap.

---

## 14. Decision Outcome

Architecture accepted as the baseline.
Subsequent ADRs refine migration (ADR 0002), health, weighting, and persistence backends without altering the abstractions captured here.

---

## 15. References

* ADR 0002 — Key Migration Execution Model
* `docs/backlog.md`
* Source: `src/Shardis/` (hashing, routing, persistence, querying modules)
