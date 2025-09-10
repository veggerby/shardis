# Shardis Terminology & Vocabulary

Authoritative glossary for domain and Shardis-specific concepts. Terms are normative unless marked (experimental / future).

## A

**Adaptive Paging**
Dynamic adjustment of per-shard page size (e.g. Marten) to converge batch latency toward a target window while remaining deterministic. Controlled by min/max bounds, grow/shrink factors.

**ActivitySource**
OpenTelemetry diagnostic source used to emit spans for migration, query execution, and adaptive paging operations.

**Assignment Hit**
Router lookup where a key already has a persisted shard mapping.

**Assignment Miss**
First-time key routing event; router performs CAS to persist the new mapping.

**Atomic (Operation)**
Guarantee that an operation (e.g., shard assignment CAS) appears indivisible to concurrent actors.

## B

**Backpressure**
Intentional slowing of producers to prevent unbounded buffering (unordered merge uses bounded channel capacity to block writers). ([Reactive Streams](https://www.reactive-streams.org/))

**Backpressure Wait**
Interval where a producer is blocked because the bounded channel / buffer is full; surfaced via `IMergeObserver.OnBackpressureWaitStart/Stop`.

**Bounded Channel**
Finite-capacity queue backing unordered merge broadcasting; enables backpressure. ([System.Threading.Channels](https://learn.microsoft.com/dotnet/api/system.threading.channels))

**Budget Governor (Throttling)**
Adaptive component (planned / partial) regulating concurrency or in‑flight operations under dynamic load constraints.

## C

**CAS (Compare-And-Set)**
Atomic conditional write used by shard map stores to persist a key→shard assignment only if no mapping (or an expected mapping) exists. ([Compare-and-swap](https://en.wikipedia.org/wiki/Compare-and-swap))

**Checkpoint (Migration)**
Snapshot `{ PlanId, Version, UpdatedAtUtc, KeyStates, LastProcessedIndex }` persisted to allow idempotent resume after interruption. (See [ADR-0002](adr/0002-key-migration-execution.md))

**Checkpoint Flush**
Write of the current in-memory migration state to the checkpoint store based on transition count or elapsed interval. (See [ADR-0002](adr/0002-key-migration-execution.md))

**Checksum Verification**
Data integrity technique comparing an aggregate hash or checksum of source vs. target entity representation.

**Concurrency (Segment / Copy / Verification)**
Parallelism level for simultaneous key moves or segment executions; bounded to preserve determinism and resource safety.

**Consistent Hashing / Hash Ring**
Strategy to distribute keys across shards using a ring of hash slots minimizing reassignment on topology change. ([Consistent hashing](https://en.wikipedia.org/wiki/Consistent_hashing))

**Cold Start (Router)**
Initial period where shard map cache is empty; first assignments incur misses until cache warm-up. (See [ADR-0001](adr/0001-core-architecture.md))

**Cutover (Migration)**
Moment a verified key’s authoritative mapping is atomically switched to the target shard (inside Swap Phase). (See [ADR-0002](adr/0002-key-migration-execution.md))

## D

**Data Mover (`IShardDataMover`)**
Provider-specific implementation that copies (and optionally preps for verify) data for a single key from source to target shard.

**Determinism**
Property ensuring identical inputs (key set, topology, options) produce identical routing, paging, and migration outcomes.

**Durable Store**
Persistent implementation (e.g., SQL / Redis) of shard map or checkpoint state surviving process restarts.

## E

**Entity Projection Strategy**
Abstraction controlling which entity members are selected for copy / checksum operations (EF / Marten migrations).

## F

**Fan-Out Query**
Execution pattern issuing per-shard sub-queries then merging resultant streams (ordered or unordered).

**Failure Injection**
Testing instrumentation wrapping a mover or verification strategy to induce transient/controlled faults.

## G

**Grow Factor (Adaptive Paging)**
Multiplier applied to increase the next page size when observed batch latency is below the lower threshold.

## H

**Hash Ring / Shard Ring**
Circle of virtual nodes representing shard distribution for consistent hashing.

**Heap Sampling (Merge)**
Optional periodic measurement of buffered items / internal heap size for ordered merge instrumentation.

## I

**Idempotent**
Operation safe to repeat without changing the final outcome (e.g., resuming migration from checkpoint does not double-copy verified keys). ([Idempotence](https://en.wikipedia.org/wiki/Idempotence))

**In-Memory Store**
Non-durable implementation (development / tests) for shard map or checkpoint state.

## K

**Key Move**
Atomic logical unit of migration for a specific shard key transitioning through phases: Copy → Verify → Swap (or Failed). (See [ADR-0002](adr/0002-key-migration-execution.md))

Lifecycle diagram: see migration flowchart in `migration-usage.md`.

**KeySpace**
Full set of logical shard keys under management.

**K-Way Merge**
Algorithm that merges K sorted shard streams using a min-heap (priority queue) keyed by the ordering selector, yielding a globally ordered sequence in O(N log K) time while only holding at most one buffered item per participating shard (plus heap overhead). Shardis uses this for Ordered Merge queries. ([K-way merge algorithm](https://en.wikipedia.org/wiki/K-way_merge_algorithm))

## L

**LastProcessedIndex**
Cursor within the ordered plan list indicating how far execution progressed (used in resume logic).

**Logging Abstraction (`IShardisLogger`)**
Lightweight facade capturing structured messages without imposing `Microsoft.Extensions.Logging` dependency on core packages.

## M

**Materializer**
Provider-specific enumerator / reader turning underlying storage query results into an async stream for merge.

**Merge Observer (`IMergeObserver`)**
Instrumentation hook for yield events, completion, backpressure waits, heap sampling (unordered/ordered merge paths).

**Metrics Observer (`IShardisMetrics`)**
Counter-based abstraction for routing, migration, query, or paging metrics; default is no-op.

**Migration Plan**
Immutable specification of intended key moves (source→target shard assignments + PlanId) produced by the planner. (See [ADR-0002](adr/0002-key-migration-execution.md))

Visual context: execution flow & sequence diagrams in `migration-usage.md`.

**Migration PlanId**
Stable GUID identifying a plan; reused across resumes to align with persisted checkpoints.

**Migration Verification Strategy**
Component performing logical or physical equality / checksum comparisons to validate copy correctness.

## O

**Ordered Merge**
K-way priority (min-heap) merge preserving a global ordering (e.g., by timestamp or key) across shards. (See [ADR-0003](adr/0003-merge-modes.md))

**Optimistic Concurrency (Routing / Migration)**
Pattern relying on conditional write (CAS) semantics so competing writers rarely block; failures retry with fresh state rather than locking. ([Optimistic concurrency control](https://en.wikipedia.org/wiki/Optimistic_concurrency_control))

**Oscillation (Adaptive Paging)**
Repeated up/down page size fluctuation beyond a threshold, detected to stabilize sizing decisions.

## P

**Page Size (Adaptive / Fixed)**
Number of items materialized per shard request cycle.

**Prefetch (Ordered Path)**
Bound limiting outstanding per-shard fetched items to control memory without channel backpressure semantics.

**Projection (Migration)**
Subset / shape of entity data considered for copy/verification.

## R

**Retry Budget**
Allowed number of per-key retry attempts before marking a move as Failed.

**Rowversion Verification**
EF-specific optimistic concurrency token comparison approach. ([rowversion](https://learn.microsoft.com/sql/t-sql/data-types/rowversion-transact-sql))

**Routing**
Assignment of logical keys to shards using hashing + map store persistence (CAS guarded).

**Rebalancing (Key Rebalancing)**
Migration activity redistributing existing keys across an evolved shard topology (e.g., after adding shards) to restore load uniformity with minimal movement.

## S

**Segment (Planned)**
Future optimization grouping contiguous key moves to reduce checkpoint size and overhead. (See [ADR-0004](adr/0004-segmented-planner.md))

**Segment Concurrency**
Parallelism for executing segments concurrently (future ADR-0004). (See [ADR-0004](adr/0004-segmented-planner.md))

**Shard**
Logical allocation unit (could map to DB, schema, table, or tenant partition).

**Shard Id (`ShardId`)**
Strongly typed identifier of a shard.

**Shard Key (`ShardKey<TKey>`)**
Value object representing a logical key being routed or migrated.

**Shard Map**
Persisted mapping of keys → assigned shards.

**Shard Map Store (`IShardMapStore`)**
Persistence abstraction providing atomic read / CAS write semantics for key assignments. (See [ADR-0001](adr/0001-core-architecture.md))

**Shard Map Swapper (`IShardMapSwapper`)**
Migration component applying verified assignments into the authoritative shard map.

**Shard Topology**
Active set of shards (and optionally their virtual node distribution) participating in routing.

**Shrink Factor (Adaptive Paging)**
Multiplier reducing next page size when latency exceeds target window.

**Swap Phase**
Final migration phase applying verified mapping into shard map (Cutover).

**Spillover Buffer (Merge)**
Transient holding area when consumer temporarily stalls; bounded by either channel capacity (unordered) or prefetch count (ordered) guarding against unbounded memory growth.

## T

**Telemetry (Migration / Query)**
Unified diagnostic signals (metrics + tracing + logging) across execution components.

**Throttling**
Dynamic reduction of concurrent copy / verify operations to remain within resource budgets.

**Topology Change**
Addition or removal (or weight change) of shards requiring routing re-evaluation and possible rebalancing.

**Tracing Span**
Individual Activity representing a discrete operation (e.g., per-key copy, verification, swap, query page).

## U

**Unbounded Channel**
Zero or omitted capacity configuration implying no producer backpressure (unordered merge path).

**Unordered Merge**
Merge strategy emitting items as they arrive from shard producers without global ordering guarantees.

## V

**Verification Phase**
Migration phase ensuring copied data matches source (checksum / rowversion / projection equality).

**Virtual Node**
Logical slot on the hash ring increasing distribution granularity for a shard. ([Virtual node](https://en.wikipedia.org/wiki/Consistent_hashing#Virtual_nodes))

## W

**Write Amplification (Migration)**
Additional writes incurred (e.g., duplicate copy attempts) due to retries or checkpoints—bounded by idempotent logic.

---

## Relationships Cheat Sheet

Key Move Lifecycle: Planned → Copying → Copied → Verifying → Verified → Swapping → Swapped (Success) / Failed (Terminal)
Backpressure Context: (unordered merge + bounded channel) ⇒ producer waits; ordered merge ⇒ prefetch bound only.
Adaptive Paging Loop: Observe batch latency → Compare vs target → Adjust (grow/shrink factors within min/max) → Emit decision telemetry.

---

## Status Tags

(Experimental) — Public surface subject to change; do not rely for long-term compatibility.
(Future) — Defined in ADR, not yet implemented.

---

For deeper architectural rationale see:

- ADR-0001 Core Architecture
- ADR-0002 Key Migration Execution
- ADR-0004 Segmented Planner (proposed)
- README sections: Routing, Query Merge Modes, Migration, Adaptive Paging.
