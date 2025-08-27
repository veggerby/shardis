# 🧩 Shardis Backlog

This file tracks the planned and proposed features for the Shardis sharding library. Each entry is categorized and labeled to help guide roadmap planning and contributions.

---

## ✅ Completed / In Progress

| Feature / Work Item | Status | Category | Description |
|---------------------|--------|----------|-------------|
| Redis-backed `IShardMapStore` | ✅ Done | `infra` | Key-to-shard persistence using Redis for fast, durable assignments. |
| `ConsistentHashShardRouter` | ✅ Done | `core` | Low-churn consistent hashing router with replication factor + pluggable ring hasher. |
| Ring hashers (`IShardRingHasher`, default + FNV-1a) | ✅ Done | `core` | Abstraction + implementations for ring node hashing. |
| Streamed querying primitives (`ShardBroadcaster`, `ShardStreamBroadcaster`) | ✅ Done | `core` | Parallel shard dispatch + streaming aggregation. |
| Ordered merge enumerator (`ShardisAsyncOrderedEnumerator`) | ✅ Done | `core` | K-way streaming merge for global ordering. |
| Combined enumerator | ✅ Done | `core` | Non-ordered interleaving enumerator. |
| `IShardKeyHasher<TKey>` pluggable hashing | ✅ Done | `core` | Deterministic key hashing abstraction. |
| Metrics interface (`IShardisMetrics`) + counter impl | ✅ Done | `infra` | Hookable routing metrics (hits, misses, new/existing). |
| DI options surface (`AddShardis` + options) | ✅ Done | `dx` | Configurable router strategy, hashers, replication factor, stores. |
| Benchmarks project | ✅ Done | `infra` | Baseline performance harness (routers, hashers). |
| Migration scaffolding (`IShardMigrator`, plan/execute) | ✅ Done | `core` | Foundation for future safe key moves. |
| Documentation overhaul (README, index, migration, metrics) | ✅ Done | `dx` | Core concepts + roadmap captured. |
| Assertion unification (AwesomeAssertions) | ✅ Done | `dx` | Standardized on external AwesomeAssertions package. |
| Fluent query prototype (`ShardQuery` internal) | 🧪 Prototype | `dx` | Early LINQ-style API (ordering & provider integration pending). |
| Metrics duplication elimination (consistent router) | ✅ Done | `core` | Single metrics emission via unified Resolve helper. |
| Consistent ring binary search lookup | ✅ Done | `perf` | Snapshot key array + Array.BinarySearch replaces linear scan. |
| Shared routing resolve helper (both routers) | ✅ Done | `core` | Centralizes assignment + metrics logic (removes duplication). |
| Map store CAS primitive (`TryAssignShardToKey`) | ✅ Done | `core` | Atomic first-writer wins; routers avoid overwrite races. |
| Shard ID uniqueness validation | ✅ Done | `core` | Duplicate shard IDs detected early in router constructors. |
| Broadcaster completion & cancellation hardening | ✅ Done | `core` | Deterministic channel completion + early cancellation support. |
| Removed per-item Task.Yield in enumeration | ✅ Done | `perf` | Eliminated unnecessary scheduling overhead in query prototype. |
| Early cancellation for Any / First | ✅ Done | `perf` | Linked CTS cancels remaining producers after first match. |
| Dynamic ring mutation (add/remove shards) | ✅ Done | `core` | Thread-safe add/remove with atomic ring key snapshot rebuild. |
| Replication factor upper bound validation | ✅ Done | `core` | Guards against pathological ring sizes (>10,000). |
| Map store unified fast-path (`TryGetOrAdd`) | ✅ Done | `perf` | Eliminated double lookup + double hashing on first assignment. |
| Single-miss metrics guarantee (per-key lock + de-dup) | ✅ Done | `core` | Exactly one `RouteMiss` emitted per key under contention. |
| Ring removal fallback reassignment | ✅ Done | `core` | Auto re-hash & reassign when mapping points to removed shard. |
| Dynamic ring rebuild concurrency tests | ✅ Done | `qa` | Verifies routing stability during add/remove under load. |
| Ring distribution test | ✅ Done | `qa` | Statistical evenness assertions on ring (coefficient of variation bounds). |

---

## 🔧 High Priority (Next Up)

### Core Optimizations / Hardening

All previously listed high-impact optimizations have been completed (see Completed section). Add new items below as they are identified.

| Item | Label | Description |
|------|-------|-------------|
| Metrics miss emission unification (consistent router review) | `core` | Ensure consistent router uses same single-miss enforcement pattern (evaluate lock vs pure CAS + de-dup). |
| Per-key lock dictionary lifecycle strategy | `perf` | Evaluate memory growth; consider aging or document unbounded behavior. |
| Routing performance benchmark (pre/post TryGetOrAdd) | `perf` | Add benchmark capturing latency & allocation delta. |
| Dynamic ring rebuild cost benchmark | `perf` | Measure add/remove latency across replication factors (50–500). |
| Migration operational playbook doc | `dx` | Document scale-out (add), drain (remove), migrate sequence + invariants. |
| OpenTelemetry metrics usage example | `dx` | Sample configuration + counter mapping guidance. |
| Fuzz test: hash distribution variance | `qa` | Random shard IDs + large key set to assert CV bounds. |
| Integration test: shard removal under heavy routing | `qa` | Remove shard mid‑storm; assert no failures, fallback reassignment works. |
| CHANGELOG initialization | `dx` | Start formal change tracking pre-first release. |
| Optional read-only Route peek (`TryGetAssignment`) | `dx` | Non-mutating lookup without hit/miss side effects. |
| Mutable topology interface extraction (`IMutableShardTopology`) | `core` | Clarify capability boundary for dynamic operations. |

### Newly Added (Issue Stubs)

| Item | Label | Description | Issue |
|------|-------|-------------|-------|
| Migration execution pipeline (plan/copy/verify/swap/rollback) | `core` | End-to-end key movement lifecycle with idempotent phases and rollback safeguards. | _TBD_ |
| Migration checkpoints & dry-run diff | `core`,`dx` | Persisted progress + ability to preview moves without executing data copy. | _TBD_ |
| Migration metrics & progress events | `dx` | Counters (planned, copied, verified, failed, retried), gauges (throughput, ETA) and optional progress callbacks. | _TBD_ |
| Topology snapshot import/export | `core` | Serialize/restore ring + assignments for cold start, rollback, audits. | _TBD_ |
| Shard map compaction / stale key cleanup | `core` | Remove or archive keys referencing removed shards or post-migration tombstones. | _TBD_ |
| Transient fault policy abstraction (map stores) | `infra` | Central retry/backoff strategy for Redis/SQL stores—no ad hoc retries in callers. | _TBD_ |
| Optional read-only route peek (`TryGetAssignment`) | `dx` | Non‑mutating lookup without hit/miss metric side effects. | _TBD_ |
| Weighted shard interface precursor | `core` | Abstraction to allow future weighted / capacity-based routing without breaking changes. | _TBD_ |
| Health policy abstraction (`IShardHealthPolicy`) | `core` | Compose health checks + policy (e.g. quorum, hysteresis) distinct from raw liveness probe. | _TBD_ |
| Backpressure & timeout guarantees doc | `dx` | Document broadcaster queue bounds, cancellation propagation, timeout invariants. | _TBD_ |
| Versioning & change policy doc | `dx` | SemVer intent, pre-1.0 guarantees, deprecation workflow. | _TBD_ |
| Security & logging guidance doc | `dx` | What is safe to log; avoidance of leaking keys or shard assignments; hashing considerations. | _TBD_ |
| OpenTelemetry tracing sample (routing + migration spans) | `dx` | Example wiring of spans around resolution, broadcast, migration phases. | _TBD_ |
| Weighted / adaptive routing R&D placeholder | `experimental` | Follow-on after weighted interface: dynamic weight adjustment from metrics. | _TBD_ |

### Feature Enablement

| Item | Label | Description |
|------|-------|-------------|
| Fluent Developer API (MVP) | `dx` | Solidify query planner, ordering merge integration, terminal ops. |
| SQL-backed `IShardMapStore` | `infra` | Durable relational store (optimistic concurrency for migrations). |
| Migration execution (copy + verify + map swap) | `core` | Implement idempotent per-key pipeline with metrics. |
| Shard Health Monitoring | `core` | `IShardHealthCheck` + broadcaster skip / degrade logic. |
| Lightweight extended metrics (latency, distribution) | `dx` | Optional observer / histogram without polluting hot path. |

### Testing & Benchmarks

| Item | Label | Description |
|------|-------|-------------|
| Concurrency stress tests (routing) | `qa` | Verify single assignment under race, metrics consistency. |
| Bench: ring lookup vs current | `perf` | Validate improvement pre/post optimization. |
| Broadcaster throughput benchmark | `perf` | Measure effect of channel capacity & backpressure. |
| Enumerator performance benchmarks (skew & shard count matrix) | `perf` | Measure ordered/combined enumerators under high skew and varying shard counts. |
| Merge enumerator benchmark suite (`merge` category) | `perf` | Ordered vs unordered streaming merge (heap vs interleave) latency & allocation. |
| Segmented / streaming migration planner | `core` | Large key set planning without full materialization (range cursors, pagination). |
| Allocation regression guard (CI baseline) | `perf` | Establish BenchmarkDotNet baselines + threshold alerts for hot paths. |
| Hash & migration property test suite | `qa` | Property-based tests for hash uniformity, migration idempotency, dynamic ring churn. |

---

---

## 🧠 Medium-Term / Advanced Features

| Feature | Label | Description |
|--------|-------|-------------|
| Multi-Region Aware Routing | `core` | Region metadata + affinity / proximity aware routing. |
| Read/Write Split Routing | `core` | Distinct strategies for reads vs writes (leader/follower). |
| Batched Query Execution | `dx` | Group results per shard with metadata (diagnostics). |
| Shard Type Constraints / Policies | `core` | Policy layer restricting eligible shard pool per key/domain. |
| Provider adapters (Marten/EF/Dapper) | `dx` | Queryable backend integration for fluent API. |
| Query latency / slow shard detection | `dx` | Emit timing + per-shard latency histograms. |

---

---

## 🧩 Transactional Consistency & Coordination

| Feature | Label | Description |
|--------|-------|-------------|
| Sharded Transaction Coordination | `core` | Explore options for transactional consistency across shards, including best-effort commit protocols, local transaction scopes, or eventual consistency tools. Optional integration with outbox/inbox or distributed transaction patterns (e.g., Saga orchestration). |

---

## 🚀 Stretch Goals / R&D

| Feature | Label | Description |
|--------|-------|-------------|
| Load-Based / Adaptive Allocation | `experimental` | Dynamic weight adjustment from observed load vs static hashing. |
| Shard Isolation & Circuit Breaking | `core` | Circuit state transitions + degraded mode routing. |
| gRPC-Based Shard Proxying | `infra` | Remote shard session dispatch via service boundary. |
| Distributed LINQ/Query DSL | `experimental` | Higher-order query distribution & expression rewriting pipeline. |
| Predictive key pre-sharding | `experimental` | ML / heuristics to pre-allocate hot tenants. |

---

---

## 🏷 Labels & Categories

- `core`: Impacts routing, hashing, session creation, or enumeration.
- `infra`: Relates to storage backends or system-level integration.
- `dx`: Developer Experience enhancements, APIs, extensions, or helpers.
- `experimental`: Under research or speculative. May not make it to production.

---

## ✍ Contributing

If you want to contribute to any of these areas, open a GitHub issue referencing the feature name above. Contributions are welcome in both code and design ideas!
