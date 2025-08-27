# Changelog

All notable changes to the `Shardis.*` packages will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added (Unreleased)

- Streaming globally ordered query API: `QueryAllShardsOrderedStreamingAsync<TResult,TKey>` with bounded per-shard prefetch (`prefetchPerShard`).
- Proactive k-way merge enumerator (`ShardisAsyncOrderedEnumerator`) supporting deterministic tie-break `(key, shardIndex, sequence)` and bounded memory.
- Internal merge probe hook (`IOrderedMergeProbe`) for test/diagnostic observation of heap size.
- Parallelized eager ordered path (`QueryAllShardsOrderedEagerAsync`) materializing per shard concurrently before merge.
- Expanded test suite: duplicate-key determinism, early emission (first item before slow shards complete), heap bound enforcement, cancellation hygiene, exception propagation.
- Merge observer lifecycle extension: `IMergeObserver.OnShardStopped(ShardId, ShardStopReason)` with reasons (`Completed|Canceled|Faulted`).
- Backpressure instrumentation hooks (`OnBackpressureWaitStart/Stop`) for unordered/channel path.
- Heap size sampling callbacks for ordered merge with configurable throttle (`heapSampleEvery`).
- Deterministic cancellation & startup fault tests covering stop reason emission.
- First-item latency micro benchmark (time-to-first-item instrumentation).
- README observer snippet & documented lifecycle callback semantics.
- Bounded prefetch validation (`prefetchPerShard >= 1`) and broadcaster argument validation (`channelCapacity`, `heapSampleEvery`).
- CI link-check workflow and benchmark smoke workflow.
- Ordering / throttle / deterministic cancel tests asserting single-fire + ordering of `OnShardCompleted` before `OnShardStopped`.

### Changed (Unreleased)

- Ordered querying no longer relies on eager materialization by default; explicit streaming vs eager APIs clarify memory / latency trade-offs.
- Eager ordered path now uses parallel per-shard buffering then reuses ordered merge enumerator for consistency.
- `IMergeObserver` callbacks may now be invoked concurrently (thread-safety requirement documented).
- `IMergeObserver` extended with `OnShardStopped`; `OnShardCompleted` now only signals successful completion.

### Fixed (Unreleased)

- Potential under-prefetch (single-item buffering) replaced by proactive top-up loop ensuring shards are kept at `≤ prefetchPerShard` buffered items.
- Ensured exceptions from any shard during ordered streaming propagate immediately and dispose all enumerators.
- Ensured single-fire guarantee for shard stop events across success / cancel / fault paths.
- Guarded observer callbacks against downstream exceptions (no pipeline impact).

### Deprecated (Unreleased)

- Legacy `QueryAllShardsOrderedAsync` marked obsolete in favor of `QueryAllShardsOrderedStreamingAsync` and `QueryAllShardsOrderedEagerAsync`.

### Internal / Quality (Unreleased)

- Added deterministic sequence number in heap ordering to guarantee stable ordering across runs with duplicate keys.
- Added cancellation tests validating prompt disposal.
- Documentation and code comments aligned toward Step 5 (backpressure differentiation) groundwork.
- Heap sampling throttle (`heapSampleEvery`) reduces observer overhead in hot path.
- Added argument validation for broadcaster parameters (`channelCapacity`, `heapSampleEvery`, `prefetchPerShard`).
- Added ordering, throttle, startup fault, and deterministic cancellation tests for observer lifecycle.

## [0.1.1] - 2025-08-26

### Added (0.1.1)

- Initial shard migration scaffold (`Shardis.Migration` package):
  - `IShardMigrator<TKey,TSession>` abstraction + `DefaultShardMigrator<TKey,TSession>` baseline implementation (plan + execute skeleton).
  - `ShardMigrationPlan<TKey>` immutable plan type.
  - In-memory migration components (checkpoint / planner helpers) for tests.
  - Migration metrics abstraction hooks (counters planned: plan.keys, key.committed, key.failed, duration histogram scaffolding).
  - ADR & design docs (`MIGRATION.md`, `docs/adr/0002-key-migration-execution.md`) outlining phases, idempotency, data integrity tiers, dry-run roadmap.
- Unit tests covering planning invariants, retries, checkpoint persistence, and idempotent plan re-execution.

### Changed (0.1.1)

- Map store interfaces extended to support migration planning scenarios (non-breaking additions).

### Internal / Quality (0.1.1)

- Introduced structured migration backlog and task breakdown docs.
- Added benchmarks project placeholder for future migration throughput measurements.

### Notes (0.1.1)

- Data copy & verification pipeline is not yet implemented (Tier 0: mapping changes only). Future releases will layer read / write / checksum phases and dry-run.

## [0.1.0] - 2025-08-25

### Added

- Default modulo-based routing (`DefaultShardRouter`).
- Consistent hashing router with virtual nodes and dynamic topology (`AddShard` / `RemoveShard`) + atomic ring snapshot.
- Pluggable key hashing & ring hashing abstractions with default + FNV-1a implementations.
- In-memory & Redis shard map stores with atomic `TryAssignShardToKey` and `TryGetOrAdd`.
- Shard broadcasting primitives (`ShardBroadcaster`, `ShardStreamBroadcaster`) and ordered / combined async enumerators.
- Metrics abstraction (`IShardisMetrics`) and single-miss routing metrics invariant.
- Migration scaffolding (planning & execution skeleton).
- Benchmark project (routers, hashers, streaming) + docs.
- DI extension (`AddShardis`) with configurable replication factor, hashers, store factory, optional custom router factory, preserves existing `IShardMapStore` / `IShardisMetrics` registrations.
- Replication factor upper bound validation (≤ 10,000).
- Fallback reassignment for removed shard mappings.
- Comprehensive test additions (replication factor validation, concurrency, dynamic ring rebuild, metrics invariants, ring distribution, broadcaster cancellation).
- Documentation: dynamic topology, metrics semantics, ring rebuild atomicity, CAS optimization, backlog & changelog.

### Changed

- Unified default routing path with per-key lock (single miss guarantee).
- Consistent router now uses `TryGetOrAdd` and records miss only on first assignment.
- Metrics semantics tightened (miss only on creation; hit differentiates new vs existing).
- Service registration refactored: consistent hashing router is now the default (unless disabled or custom factory supplied) and existing map store / metrics registrations are no longer overridden.

### Fixed

- Multiple `RouteMiss` emissions under high concurrency.
- KeyNotFound after shard removal (automatic reassignment).
- Metrics test flakiness (thread-safe collection).

### Internal / Quality

- Single miss invariant; replication cap; shard ID uniqueness validation.
- Atomic ring snapshot swap invariant & tests.
- Backlog expanded (performance, migration, observability, memory strategy).

### Deprecated

- None.

### Removed

- None.

### Notes

- Dynamic topology limited to consistent hashing router; default router static to avoid full rehash churn.
- Per-key lock & miss tracking dictionaries currently unbounded (future lifecycle strategy).

---
Template categories follow Keep a Changelog conventions (Added / Changed / Fixed / etc.). Once merged, tag under the next version (e.g., `0.2.0` if previous was `0.1.x`).
