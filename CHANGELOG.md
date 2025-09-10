# Changelog

All notable changes to the `Shardis.*` packages will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added (Unreleased)

- Marten migration provider (`Shardis.Migration.Marten`): copy-only `MartenDataMover<TKey>`, canonical checksum verification strategy (`DocumentChecksumVerificationStrategy<TKey>`), DI extension `AddMartenMigrationSupport<TKey>()`.
- Entity Framework Core migration provider (`Shardis.Migration.EntityFrameworkCore`): data mover (`EntityFrameworkCoreDataMover`), rowversion verification (`RowVersionVerificationStrategy`), checksum verification (`ChecksumVerificationStrategy`), DI extension `AddEntityFrameworkCoreMigrationSupport<TKey,TContext,TEntity>()` + checksum registration helper.
- Marten executor integration tests: happy path, resume from copied checkpoint, swap retry (optimistic conflict), mismatch then re-copy.
- Canonicalization deep-dive documentation (`docs/canonicalization.md`) with guidance on invariants, extensibility, and future enhancements; linked from index and migration tiers.
- Migration consistency contract documentation (detailing phases, write/read handling, atomicity, failure & resume guarantees).
- Updated migration package READMEs (core, EF Core, Marten) to reflect 0.2.x features (checksum strategies, canonicalization doc links, expanded abstractions list, roadmap alignment).
- Added experimental `Shardis.Migration.Sql` project providing durable SQL-backed checkpoint and shard map stores (preview, APIs subject to change).
- Migration metrics: extended `IShardMigrationMetrics` with duration observation methods (`ObserveCopyDuration`, `ObserveVerifyDuration`, `ObserveSwapBatchDuration`, `ObserveTotalElapsed`) and instrumented `ShardMigrationExecutor` to record per-key / batch timings.
- SQL shard map store (`SqlShardMapStore`) now emits `AssignmentChanged` event after successful optimistic insert (invalidation hook for future cache layers).
- New migration extension points: `IEntityProjectionStrategy` (projection), `IStableCanonicalizer` (`JsonStableCanonicalizer` default), `IStableHasher` (`Fnv1a64Hasher` default) enabling deterministic checksum strategies.
- Adaptive migration throttling primitives: `IBudgetGovernor` + `SimpleBudgetGovernor` (preview) with global/per-shard concurrency budgeting hints.
- Expanded migration options: dual read/write toggles, budget hints (`MaxConcurrentMoves`, `MaxMovesPerShard`), health/staleness settings (`HealthWindow`, `MaxReadStaleness`).
- OpenTelemetry-style tracing spans (`shardis.migration.execute`, `copy`, `verify`, `swap_batch`) added via `ActivitySource` integration.
- Samples: `Shardis.Migration.Sample` (end-to-end scenarios), `Shardis.Query.Samples.Marten`, enhanced EF sample (Postgres env-driven setup).
- Public API baselines extended for new assemblies (Migration.Marten, Migration.Sql, Migration.EntityFrameworkCore) & new abstractions.
- Optional shard map enumeration: `IShardMapEnumerationStore<TKey>` + in-memory & SQL implementations.
- Snapshot factory helper: `TopologySnapshotFactory.ToSnapshotAsync` (cancellable, memory cap, tracing `shardis.snapshot.enumerate`).
- EF Core migration sample updated to derive source topology from enumeration (no synthetic "from" snapshot).
- Segmented enumeration migration planner (`SegmentedEnumerationMigrationPlanner<TKey>`) with DI opt-in `UseSegmentedEnumerationPlanner` for large keyspaces (streaming source topology, deterministic ordering maintained).
- Dry-run diff capability (`DryRunAsync`) on segmented planner returning examined key and move counts (capacity forecasting without full move allocation).
- Topology validation & drift utilities: `TopologyValidator.ValidateAsync` (duplicate detection) and `TopologyValidator.ComputeHashAsync` (order-independent hash) for snapshot integrity and drift detection.
- Benchmarks: `SegmentedPlannerBenchmarks` (category `plan`) comparing in-memory vs segmented planner and dry-run allocations across key counts & segment sizes.
- Migration usage docs updated with segmented planner section & dry-run planning guidance.
- Query ergonomics: `IShardQueryClient` + `ShardQueryClient` (deferred, DI friendly) with `AddShardisQueryClient` registration.
- Executor extensions: `IShardQueryExecutor.Query<T>()` and `Query<T,TResult>(...)` shorthand overloads.
- Terminal operators: `FirstOrDefaultAsync`, `AnyAsync`, `CountAsync` in `ShardQueryableTerminalExtensions` (client-side enumeration helpers).
- EF Core executor ordered (buffered) factory: `EfCoreShardQueryExecutor.CreateOrdered<TContext,TOrder>` exposing basic global ordering via materialization.
- EF Core execution options: `EfCoreExecutionOptions` (Concurrency, ChannelCapacity, PerShardCommandTimeout, DisposeContextPerQuery) – currently ChannelCapacity + timeout applied; others reserved.
- Failure handling strategies for queries: `FailFastFailureStrategy`, `BestEffortFailureStrategy` (public, singleton instances) with internal wrapper executor.
- EF Core provider DI extensions: `AddShardisEfCoreOrdered<TContext,TOrder>` (buffered global ordering) and `DecorateShardQueryFailureStrategy(IShardQueryFailureStrategy)` for post-registration decoration.
- Activated EF Core execution options: `Concurrency` and `DisposeContextPerQuery` now honored by `EntityFrameworkCoreShardQueryExecutor` (previously reserved placeholders).
- Query latency OpenTelemetry histogram `shardis.query.merge.latency` (single emission per enumeration) with stable tag schema (`db.system`, `provider`, `shard.count`, `target.shard.count`, `merge.strategy`, `ordering.buffered`, `fanout.concurrency`, `channel.capacity`, `failure.mode`, `result.status`, `root.type`).
- OpenTelemetry test suite validating single histogram point across success, canceled, failed, ordered/unordered, targeted fan-out, and failure handling strategies (fail-fast / best-effort) plus tag correctness.
- Added `invalid.shard.count` tag to latency histogram and tracing activity; all-invalid targeting now emits a zero-result histogram with `target.shard.count=0`.
- Best-effort failure handling now surfaces explicit `failure.mode=best-effort` in query latency histogram (previously always `fail-fast`).
- Hardened latency metric contract: tests now enforce exactly-one histogram point per enumeration across success, cancellation, failure, ordered, and failure-handling wrappers (suppression + unified emission internally).
- ADR 0006: Unified query latency single-emission model (documents suppression + pending context design, invariants, future work).
- Added ordered cancellation telemetry test (ensures single emission on cancel in ordered path).
- Added benchmark `QueryLatencyEmissionBenchmarks` measuring unordered vs ordered latency emission overhead (best-effort path shares same suppression code path; excluded to avoid duplicate measurement).

### Changed (Unreleased)

- Refactored `MartenDataMover<TKey>` to delegate verification to `IVerificationStrategy<TKey>` (copy-only responsibility) for parity with EF provider separation and reduced duplication.
- Core migration docs updated: metrics section now documents duration histograms (copy / verify / swap batch / total elapsed) and execution status moved from scaffold to implemented baseline.
- `ShardMigrationOptions` expanded with dual-read/write, staleness, health window, and budgeting properties (non-breaking additive changes).
- Executor now emits tracing activities and duration metrics without altering execution semantics.
- EF Core unordered executor now uses configured `EfCoreExecutionOptions.Concurrency` (bounded parallel shard fan-out) and respects `DisposeContextPerQuery=false` by retaining one `DbContext` per shard.
- Benchmarks documentation extended with segmented planner and environment variable (`SHARDIS_PLAN_KEYS`) guidance; roadmap updated to reflect partial completion of planning overhead benchmark.
- Unified ordered vs unordered query latency emission (ordered path now reuses shared instrumentation for exactly-once metric recording).
- Removed reflection usage for ordered EF Core executor creation; introduced `DefaultOrderedEfCoreExecutorFactory` internal abstraction (public for tests) replacing `CreateOrderedFromExisting` reflective invocation.
- Query README metrics section updated to document `best-effort` failure mode tagging and clarified `result.status` semantics for partial shard failures.

### Fixed (Unreleased)

### Deprecated (Unreleased)

### Removed (Unreleased)

### Security (Unreleased)

### Internal / Quality (Unreleased)

- Expanded Marten provider README (install, canonicalization contract, strategy matrix, projection guidance, telemetry tags, test setup).
- Added EF Core provider README (rowversion vs checksum guidance) & core migration README updates for new abstractions.
- New test suites: EF Core migration (rowversion, checksum, retry, idempotency), Marten migration (resume, conflict, mismatch), extension points (hashing, canonicalization, projection, budget governor), SQL-lite test utilities.
- Added `SqliteShardDbContextFactory<TContext>` test utility for per-shard in-memory persistence.

## [0.2.0] - 2025-09-08

### Added (0.2.0)

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
- Merge enumerator benchmark suite (`MergeEnumeratorBenchmarks`, category `merge`) comparing unordered streaming, ordered streaming (bounded prefetch) and ordered eager strategies across shard count, items/shard, skew, capacity (unordered only), and prefetch parameters.
- Export of first-item latency percentiles (p50/p95) aggregated across benchmark runs to CSV: `merge-first-item-latency-all-methods-seed<seed>.csv`.
- Broadcaster capacity sweep benchmarks (`BroadcasterStreamBenchmarks`, category `broadcaster`) including baseline and consumer-slow scenarios with first-item latency & backpressure wait/blocked metrics; CSV export with scenario + capacity percentiles (`broadcaster-capacity-sweep-seed<seed>.csv`).
- README observer snippet & documented lifecycle callback semantics.
- Bounded prefetch validation (`prefetchPerShard >= 1`) and broadcaster argument validation (`channelCapacity`, `heapSampleEvery`).
- CI link-check workflow and benchmark smoke workflow.
- Ordering / throttle / deterministic cancel tests asserting single-fire + ordering of `OnShardCompleted` before `OnShardStopped`.
- Cancellation & leak test suite (unordered early-cancel, ordered streaming mid-cancel, small-capacity deadlock guard) with WeakReference-based `LeakProbe` and capped GC retry.
- Metrics observer tests validating heap sampling (>0), lifecycle callbacks, backpressure wait symmetry and zero-wait invariants for unbounded / ordered streaming paths.
- Category traits (`[Trait("category","cancellation")]`, `[Trait("category","metrics")]`) to enable selective CI shards.
- Adaptive paging for Marten query executor with latency-targeted deterministic page adjustments (`WithAdaptivePaging`).
- Adaptive paging telemetry (`IAdaptivePagingObserver`): `OnPageDecision`, `OnOscillationDetected`, `OnFinalPageSize`.
- Consolidated multi-assembly Public API approval tests (`Shardis.PublicApi.Tests`) with auto baseline creation and drift `.received` snapshots.
- Central public API baselines (`test/PublicApiApproval/*.approved.txt`) for all assemblies (core, migration, redis, query providers, testing, marten).
- Allocation benchmark guard (adaptive vs fixed Marten paging) with JSON export + delta report (`ADAPTIVE_ALLOC_MAX_PCT`, `ADAPTIVE_ALLOC_MIN_BYTES`).
- EF Core provider README & documented command timeout usage in samples.
- README updates (adaptive paging guidance, telemetry expansion, allocation guard docs, ordered vs unordered merge guidance).
- General shard-scoped creation abstraction `IShardFactory<T>` with helpers (`DelegatingShardFactory<T>`, `ShardFactoryExtensions.UseAsync`).
- Provider-neutral shard configuration abstraction `IShardMap` + `InMemoryShardMap` implementation.
- EF Core / Marten / Redis shard factory adapters (`EntityFrameworkCoreShardFactory<TContext>`, `PooledEntityFrameworkCoreShardFactory<TContext>`, `MartenShardFactory`, `RedisShardFactory`).
- New `Shardis.DependencyInjection` package providing per-shard resource registration (`AddShard`, `AddShards`, `AddShardInstance`) and DI-based `IShardFactory<T>` resolution + safe `UseAsync` helpers.
- Documentation updates: packages table, DI quick start sample, corrected executor naming in `Shardis.Query.EntityFrameworkCore` README.
- Namespace & package rename consolidation: legacy short `EFCore` references fully expanded to `EntityFrameworkCore` across code, docs, and public API baselines.

### Changed (0.2.0)

- Ordered querying no longer relies on eager materialization by default; explicit streaming vs eager APIs clarify memory / latency trade-offs.
- Eager ordered path now uses parallel per-shard buffering then reuses ordered merge enumerator for consistency.
- `IMergeObserver` callbacks may now be invoked concurrently (thread-safety requirement documented).
- `IMergeObserver` extended with `OnShardStopped`; `OnShardCompleted` now only signals successful completion.
- Public API approval moved from custom reflection snapshot in query tests to standardized PublicApiGenerator-based consolidated project (stable ordering & clearer diffs).
- Adaptive paging materializer now records decision history to detect oscillation, emits final page size summary.
- `EntityFrameworkCoreShardQueryExecutor` now depends on `IShardFactory<DbContext>` instead of `Func<int,DbContext>`; adapt by wrapping existing delegates with `DelegatingShardFactory<DbContext>` or using `EntityFrameworkCoreShardFactory<TContext>`.
- Removed `IShardSessionProvider<TSession>` in favor of unified `IShardFactory<T>`; `Shard<TSession>` now exposes both `CreateSession()` and `CreateSessionAsync()` delegating to the factory.

### Fixed (0.2.0)

- Potential under-prefetch (single-item buffering) replaced by proactive top-up loop ensuring shards are kept at `≤ prefetchPerShard` buffered items.
- Ensured exceptions from any shard during ordered streaming propagate immediately and dispose all enumerators.
- Ensured single-fire guarantee for shard stop events across success / cancel / fault paths.
- Guarded observer callbacks against downstream exceptions (no pipeline impact).
- Eliminated flaky public API approval failures caused by ordering drift (stable generator & normalization).

### Deprecated (0.2.0)

- Legacy `QueryAllShardsOrderedAsync` marked obsolete in favor of `QueryAllShardsOrderedStreamingAsync` and `QueryAllShardsOrderedEagerAsync`.

### Internal / Quality (0.2.0)

- Added deterministic sequence number in heap ordering to guarantee stable ordering across runs with duplicate keys.
- Added cancellation tests validating prompt disposal.
- Documentation and code comments aligned toward Step 5 (backpressure differentiation) groundwork.
- Heap sampling throttle (`heapSampleEvery`) reduces observer overhead in hot path.
- Added argument validation for broadcaster parameters (`channelCapacity`, `heapSampleEvery`, `prefetchPerShard`).
- Added ordering, throttle, startup fault, and deterministic cancellation tests for observer lifecycle.
- Deterministic per-shard delay schedules reused in benchmarks (seeded) for reproducible merge latency measurements.
- Added `.gitignore` rule for `*.received.txt` (keeps transient diff artifacts out of commits).
- Added PublicApiGenerator (v11.x) test dependency; baseline writer establishes approvals automatically on first run.
- Allocation guard minimum-byte threshold reduces noise for trivial deltas.
- Stabilized adaptive paging telemetry test (CI flakiness) by relaxing timing‑sensitive observer assertions (no functional change to adaptive paging feature).

### Removed (0.2.0)

- `DefaultShardMigrator<TKey,TSession>` removed from the core `Shardis` project: this type has been intentionally deleted to avoid duplicate migration pathways and to make `Shardis.Migration` the single, canonical migration executor. This is a breaking change for any consumer who depended on the core stub; the recommended replacement is the `Shardis.Migration` package and its `ShardMigrationExecutor<T>` execution pipeline (register via `AddShardisMigration<T>()`). The removal was considered low-risk since the core stub was a lightweight, non-production scaffold.

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
