# ADR 0003: Merge Modes for Shard Stream Aggregation

**Status:** Accepted
**Date:** 2025-08-27
**Author:** Jesper Veggerby
**Supersedes:** None
**Superseded by:** None
**Related:** [ADR 0001](./0001-core-architecture.md) (Core Architecture), [ADR 0002](./0002-key-migration-execution.md) (Key Migration Execution), [Backlog](../backlog.md)

---

## Context

Shardis supports querying multiple shards in parallel and merging results into a single consumer stream (`IAsyncEnumerable<T>`).
Different consumers and workloads have different needs around **ordering**, **latency**, and **memory usage**:

* **Low-latency dashboards** benefit from seeing results as soon as any shard yields.
* **Large ordered result sets** require global ordering without materializing all items in memory.
* **Small, simple queries** may prefer eager materialization for simplicity at the cost of memory and latency.

To balance these trade-offs, we define **three explicit merge modes**. This ADR fixes the terminology, API naming, and selection guidance.

---

## Decision

Shardis will support the following merge strategies:

| Mode                    | API                                                                          | Ordering Guarantee                                                                | Memory Profile                         | Time-to-First-Item                                               | When To Use                                                       | Trade-offs                                                                                           |
| ----------------------- | ---------------------------------------------------------------------------- | --------------------------------------------------------------------------------- | -------------------------------------- | ---------------------------------------------------------------- | ----------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| **Unordered Streaming** | `ShardStreamBroadcaster.QueryAllShardsAsync` (current)                       | None (arrival/interleave order only)                                              | O(shards + channel capacity per shard) | Fastest (first producer yield)                                   | Low latency fan-out where global order not required               | Consumer must tolerate arbitrary interleaving; non-deterministic across runs if shard timing differs |
| **Ordered Streaming**   | `QueryAllShardsOrderedStreamingAsync` (new)                                  | Global ascending by provided key selector (stable for equal keys via shard index) | O(shards × prefetch)                   | Fast (bounded by slowest shard holding the globally minimal key) | Large result sets requiring ordering without full materialization | Slightly higher coordination overhead (min-heap, per-shard prefetch)                                 |
| **Ordered Eager**       | `QueryAllShardsOrderedEagerAsync` (rename of existing internal ordered path) | Global ascending by key selector                                                  | O(total items)                         | Slowest (must drain all shards before yielding)                  | Small/medium result sets where simplicity outweighs memory cost   | High peak memory; delayed first item; can impact GC                                                  |

---

## Selection Guidance

1. **Unordered Streaming** – Default for exploratory or latency-sensitive scenarios (dashboards, live monitoring).
2. **Ordered Streaming** – Use when clients need sorted results but datasets are too large for eager materialization.
3. **Ordered Eager** – Reserve for small result sets (≤ a few thousand items) or when the full collection is needed in memory post-processing.

---

## Determinism & Stability

* Ordered modes break ties first by the **selected key**, then by **shard enumeration index**.
* Stable output is guaranteed if shard enumeration order is stable.
* Unordered streaming is inherently non-deterministic when shard timing varies.

---

## Consequences

* The API surface becomes explicit and predictable.
* Developers can choose based on latency vs memory vs ordering, avoiding “one-size-fits-all” merges.
* Additional complexity in documentation and testing, but reduced ambiguity in usage.
* Benchmarking and tuning can focus on these three supported modes without proliferating alternatives.

---

## Alternatives Considered

* **Single unified API with mode flags**

  * *Rejected*: Hides complexity, but reduces clarity and increases risk of misuse. Explicit APIs are preferable.

* **Always eager ordered merge**

  * *Rejected*: Simpler implementation, but unacceptable memory profile and delayed time-to-first-item for large datasets.

* **Always unordered streaming**

  * *Rejected*: Fastest, but results are unstable across runs and unsuitable for consumers needing sorted output.

* **Expose only two modes (unordered streaming + ordered streaming)**

  * *Rejected*: Ordered eager still has a role in small datasets and simplifies downstream processing in some cases.

---

## Open Questions

* Should **Ordered Eager** remain public, or be internal/private until proven valuable for external consumers?
* Should all merge APIs take an **explicit `CancellationToken`** parameter to enforce cancellation responsiveness?
* How do we **instrument and benchmark prefetch tuning** across different shard topologies?

---

## Future Work

| Feature                                   | Status     | Notes                                             |
| ----------------------------------------- | ---------- | ------------------------------------------------- |
| Backpressure capacity for streaming modes | Planned    | Parameter to throttle per-shard producers.        |
| Heap size & latency metrics               | Planned    | Via `IMergeObserver` instrumentation.             |
| Prefetch hint tuning                      | Planned    | Default 1–2; benchmark-driven.                    |
| Cancellation responsiveness               | Planned    | Ensure consumers can abort long merges promptly.  |
| Partial materialization (`Take N`)        | Considered | Optimization path for ordered eager vs streaming. |

---

## References

* Design discussions (future link placeholder).
* Benchmarks (see `/benchmarks/`).
