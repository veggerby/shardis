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

---

---

## 🔧 High Priority (Next Up)

### Core Optimizations / Hardening

All previously listed high-impact optimizations have been completed (see Completed section). Add new items below as they are identified.

| Item | Label | Description |
|------|-------|-------------|
| — | — | (No outstanding core optimization tasks at this time) |

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
| Ring distribution test | `qa` | Statistical evenness under high replication factor. |
| Bench: ring lookup vs current | `perf` | Validate improvement pre/post optimization. |
| Broadcaster throughput benchmark | `perf` | Measure effect of channel capacity & backpressure. |

---

---

## 🧠 Medium-Term / Advanced Features

| Feature | Label | Description |
|--------|-------|-------------|
| Multi-Region Aware Routing | `core` | Region metadata + affinity / proximity aware routing. |
| Read/Write Split Routing | `core` | Distinct strategies for reads vs writes (leader/follower). |
| Batched Query Execution | `dx` | Group results per shard with metadata (diagnostics). |
| Shard Type Constraints / Policies | `core` | Policy layer restricting eligible shard pool per key/domain. |
| Dynamic ring mutation (add/remove shards) | `core` | Thread-safe ring rebuild + minimal churn planning integration. |
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
