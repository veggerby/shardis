# Shardis

> **Shardis**: _Bigger on the inside. Smarter on the outside._

[![Build](https://github.com/veggerby/shardis/actions/workflows/publish.yml/badge.svg)](https://github.com/veggerby/shardis/actions)
[![Coverage](https://codecov.io/gh/veggerby/shardis/branch/main/graph/badge.svg)](https://codecov.io/gh/veggerby/shardis)
[![License](https://img.shields.io/github/license/veggerby/shardis)](./LICENSE)
[![Last Commit](https://img.shields.io/github/last-commit/veggerby/shardis)](https://github.com/veggerby/shardis/commits/main)
[![Benchmarks](https://img.shields.io/badge/Benchmarks-BDN-informational)](./docs/benchmarks.md)
[![Docs](https://img.shields.io/badge/Docs-index-blue)](./docs/index.md)

<!-- NuGet badges (will show once packages are published) -->
[![NuGet (Shardis)](https://img.shields.io/nuget/v/Shardis?label=Shardis&color=004880)](https://www.nuget.org/packages/Shardis/)
[![NuGet (Shardis.Migration)](https://img.shields.io/nuget/v/Shardis.Migration?label=Shardis.Migration&color=004880)](https://www.nuget.org/packages/Shardis.Migration/)
[![NuGet (Shardis.Redis)](https://img.shields.io/nuget/v/Shardis.Redis?label=Shardis.Redis&color=004880)](https://www.nuget.org/packages/Shardis.Redis/)
[![NuGet (Shardis.Marten)](https://img.shields.io/nuget/v/Shardis.Marten?label=Shardis.Marten&color=004880)](https://www.nuget.org/packages/Shardis.Marten/)

**Shardis** is a lightweight, scalable sharding framework for .NET designed to help developers partition and route aggregates across multiple databases cleanly and efficiently.
Built for domain-driven systems, event sourcing architectures, and multi-tenant platforms, Shardis ensures that data routing remains deterministic, maintainable, and completely decoupled from business logic.

![Shardis](https://img.shields.io/badge/Shardis-Shard%20Routing%20for%20.NET-blueviolet?style=flat-square)

---

## ✨ Features

- 🚀 **Deterministic Key-based Routing**
  Route aggregate instances consistently to the correct database shard based on a strong hashing mechanism.

- 🛠️ **Pluggable Shard Map Storage**
  Abstract where and how shard mappings are stored — support in-memory development, persistent stores, or distributed caches.

- 🔗 **Designed for Event Sourcing and CQRS**
  Integrates naturally with systems like MartenDB, EventStoreDB, and custom event stores.

- 🧩 **Simple, Extensible Architecture**
  Swap out routing strategies or extend shard metadata without leaking sharding concerns into your domain models.

- 🏗 **Ready for Production Scaling**
  Shard assignments are persistent, predictable, and optimized for horizontal scalability.
- 📊 **Instrumentation Hooks**
  Plug in metrics (counters, tracing) by replacing the default no-op metrics service.
  Ordered and unordered streaming paths are covered by metrics observer tests (item counts, heap samples, backpressure waits) ensuring instrumentation stability.
- 🔄 **Consistent Hashing Option**
  Choose between simple sticky routing and a consistent hashing ring with configurable replication factor & pluggable ring hashers.
- 📥 **Ordered & Unordered Streaming Queries**
  Low-latency unordered fan-out plus deterministic k‑way heap merge (bounded prefetch) for globally ordered streaming.
- 📈 **Adaptive Paging (Marten)**
  Deterministic latency-targeted page size adjustments with oscillation & final-size telemetry.
- 🧪 **Central Public API Snapshots**
  Consolidated multi-assembly approval tests ensure stable public surface; drift produces clear `.received` diffs.

---

## 📦 Installation

🔜*(Coming soon to NuGet.)*

For now, clone the repository:

```bash
git clone https://github.com/veggerby/shardis.git
cd Shardis
```

Reference the Shardis project in your solution, or package it locally using your preferred method.

---

## 🚀 Getting Started

### Migration (recommended)

For key migration, prefer the dedicated `Shardis.Migration` package which provides a planner and an executor with in-memory defaults suitable for tests and samples.

Canonical DI usage:

```csharp
var services = new ServiceCollection()
  .AddShardisMigration<string>()
  .BuildServiceProvider();

var planner = services.GetRequiredService<Shardis.Migration.Abstractions.IShardMigrationPlanner<string>>();
var executor = services.GetRequiredService<Shardis.Migration.Execution.ShardMigrationExecutor<string>>();

var from = new Shardis.Migration.Model.TopologySnapshot<string>(new Dictionary<Shardis.Model.ShardKey<string>, Shardis.Model.ShardId>());
var to   = new Shardis.Migration.Model.TopologySnapshot<string>(new Dictionary<Shardis.Model.ShardKey<string>, Shardis.Model.ShardId>());
var plan = await planner.CreatePlanAsync(from, to, CancellationToken.None);
await executor.ExecuteAsync(plan, CancellationToken.None);
```

See `docs/MIGRATION.md` and `src/Shardis.Migration/README.md` for details and production guidance.

Setting up a basic router:

```csharp
using Shardis.Model;
using Shardis.Routing;
using Shardis.Persistence;
using Shardis.Hashing;

// Define available shards
var shards = new[]
{
    new SimpleShard(new("shard-001"), "postgres://user:pass@host1/db"),
    new SimpleShard(new("shard-002"), "postgres://user:pass@host2/db"),
    new SimpleShard(new("shard-003"), "postgres://user:pass@host3/db")
};

// Initialize the shard router

### Using Dependency Injection

var shardRouter = new DefaultShardRouter(
    shardMapStore: new InMemoryShardMapStore(),
    availableShards: shards
);

// Route a ShardKey
var userId = new ShardKey("user-451");
var shard = shardRouter.RouteToShard(userId);

Console.WriteLine($"User {userId} routed to {shard.ShardId}");
```

### Using Dependency Injection

```csharp
// Register shards & configure options
services.AddShardis<IShard<string>, string, string>(opts =>
{
  opts.UseConsistentHashing = true;         // or false for DefaultShardRouter
  opts.ReplicationFactor = 150;             // only for consistent hashing
  opts.RingHasher = Fnv1aShardRingHasher.Instance; // optional alternative ring hasher

  opts.Shards.Add(new SimpleShard(new("shard-001"), "postgres://host1/db"));
  opts.Shards.Add(new SimpleShard(new("shard-002"), "postgres://host2/db"));
});

// Override map store BEFORE AddShardis if desired:
services.AddSingleton<IShardMapStore<string>>(new InMemoryShardMapStore<string>());

// Provide metrics (default registered is no-op):
services.AddSingleton<IShardisMetrics, MetricShardisMetrics>();
```

---

## 🧠 How It Works

1. **ShardKey**: A value object representing the identity of an aggregate or entity to be routed.
2. **Shard**: Represents a physical partition (e.g., a specific PostgreSQL database instance).
3. **ShardRouter**: Routes incoming ShardKeys to the appropriate Shard based on hashing.
4. **ShardMapStore**: Caches key-to-shard assignments to ensure stable, deterministic routing over time.
5. **Metrics**: Routers invoke `IShardisMetrics` (hits, misses, new/existing assignment) – default implementation is a no-op.

### Validation & Safety Invariants

The following invariants are enforced at startup / construction to fail fast and keep routing deterministic:

| Invariant | Enforcement Point | Exception |
|-----------|-------------------|-----------|
| At least one shard registered | `AddShardis` options validation | `ShardisException` |
| ReplicationFactor > 0 and <= 10,000 | `AddShardis` options validation & router construction | `ShardisException` |
| Non-empty shard collection for broadcasters | `ShardBroadcaster` / `ShardStreamBroadcaster` constructors | `ArgumentException` |
| Null shard collection rejected | Broadcaster constructors | `ArgumentNullException` (ParamName = `shards`) |
| Null query delegate rejected | Broadcaster `QueryAllShardsAsync` methods | `ArgumentNullException` (ParamName = `query`) |

### Default Key Hashers

`DefaultShardKeyHasher<TKey>.Instance` selects an implementation by type:

| Key Type | Hasher |
|----------|--------|
| `string` | `StringShardKeyHasher` |
| `int` | `Int32ShardKeyHasher` |
| `uint` | `UInt32ShardKeyHasher` |
| `long` | `Int64ShardKeyHasher` |
| `Guid` | `GuidShardKeyHasher` |
| other | (throws) `ShardisException` |

Override via `opts.ShardKeyHasher` if you need a custom algorithm (e.g. xxHash, HighwayHash) – ensure determinism and stable versioning.

### Consistent Hash Ring Guidance

`ReplicationFactor` controls virtual node count per shard. Higher values smooth distribution but increase memory and ring rebuild time. Empirically:

| ReplicationFactor | Typical Shard Count | Distribution Variance (cv heuristic) |
|-------------------|---------------------|---------------------------------------|
| 50 | ≤ 8 | ~0.40–0.45 |
| 100 (default) | 8–16 | ~0.32–0.38 |
| 150 | 16–32 | ~0.28–0.33 |
| 200+ | 32+ | Diminishing returns |

Variance numbers are approximate and workload dependent; adjust after observing real key distributions.

Replication factor hard cap: values greater than **10,000** are rejected to prevent pathological ring sizes (memory amplification + long rebuild latency).

### Shard Map Store CAS Semantics

`IShardMapStore<TKey>` exposes two atomic primitives:

- `TryAssignShardToKey` (compare-and-set). First writer wins; concurrent attempts racing to assign the same key yield exactly one `true`.
- `TryGetOrAdd` – fetch an existing assignment or create it without a separate preliminary lookup (eliminates double hashing / allocation patterns in hot routing paths).

Routers rely on these to avoid duplicate assignments under bursty traffic. Tests stress thousands of concurrent attempts to ensure a single winner.

### Routing Metrics Semantics

Routers emit exactly one `RouteMiss` for the first key assignment followed by:

1. `RouteHit(existingAssignment=false)` for the initial persisted assignment.
2. `RouteHit(existingAssignment=true)` for every subsequent route.

Single-miss guarantee (even under extreme concurrency) is enforced via:

- Per-key lock in the Default router collapsing races to a single creator.
- Miss de-dup dictionary in both routers so even if optimistic creation paths surface multiple contenders, only the first records the miss.

Consistent hash router only records a miss if `TryGetOrAdd` actually created the mapping and it has not yet been recorded for that key.

### Broadcasting & Streaming

`ShardBroadcaster` (materializing) and `ShardStreamBroadcaster` (streaming) enforce non-empty shard sets and parameter validation. The streaming broadcaster:

- Starts one producer task per shard.
- Supports optional bounded channel capacity (backpressure) – unbounded by default.
- Cancels remaining work early for short‑circuit operations (`AnyAsync`, `FirstAsync`).
- Guarantees that consumer observation order is the actual arrival order (no artificial reordering unless using ordered merge utilities).
- Emits lifecycle callbacks via `IMergeObserver`:
  - `OnItemYielded(shardId)` – after an item is yielded to the consumer.
  - `OnShardCompleted(shardId)` – shard produced all items successfully.
  - `OnShardStopped(shardId, reason)` – exactly once per shard with `Completed|Canceled|Faulted`.
  - `OnBackpressureWaitStart/Stop()` – unordered path only when bounded channel is full.
  - `OnHeapSizeSample(size)` – ordered merge heap sampling (throttled by `heapSampleEvery`).

#### Minimal Observer Example

```csharp
using Shardis.Querying;
using Shardis.Model;

public sealed class LoggingObserver : IMergeObserver
{
    private int _count;
    public void OnItemYielded(ShardId shardId) => Interlocked.Increment(ref _count);
    public void OnShardCompleted(ShardId shardId) => Console.WriteLine($"Shard {shardId} completed.");
    public void OnShardStopped(ShardId shardId, ShardStopReason reason) => Console.WriteLine($"Shard {shardId} stopped: {reason} (items so far={_count}).");
    public void OnBackpressureWaitStart() { }
    public void OnBackpressureWaitStop() { }
    public void OnHeapSizeSample(int size) => Console.WriteLine($"Heap size: {size}");
}

// Wiring:
var observer = new LoggingObserver();
var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, channelCapacity: 64, observer: observer, heapSampleEvery: 10);
```

Observer implementations MUST be thread-safe; callbacks can occur concurrently.

### Ordered vs Combined Enumeration

- `ShardisAsyncOrderedEnumerator` performs a k‑way merge using a min-heap keyed by the provided selector – stable for identical keys (tie broken by shard enumeration order).
- `ShardisAsyncCombinedEnumerator` simply interleaves items as each shard advances; no global ordering guarantees.

### Cancellation Behavior

Enumerators and broadcasters honor passed `CancellationToken`s; ordered/combined enumerators propagate cancellation immediately on next `MoveNextAsync` and broadcasters swallow expected cancellation exceptions after signaling completion.

---

## 📚 Example Use Cases

- Distribute user accounts across multiple PostgreSQL clusters in a SaaS platform.
- Scale event streams across multiple event stores without burdening domain logic.
- Implement tenant-based isolation by routing organizations to their assigned shards.
- Future-proof a growing CQRS/Event Sourcing system against database size limits.

---

## ⚙️ Extending Shardis

Shardis is designed for extension:

- **Custom Routing Strategies**
  Implement your own `IShardRouter` if you need consistent hashing rings, weighted shards, or region-aware routing.

- **Persistent Shard Maps**
  Replace the in-memory `IShardMapStore` with implementations backed by SQL, Redis, or cloud storage.

- **Shard Migrations and Rebalancing**
  Coming soon: native support for safely reassigning keys and migrating aggregates between shards.
- **Metrics / Telemetry**
  Implement `IShardisMetrics` to export counters to OpenTelemetry / Prometheus.

---

## 🛡️ Design Philosophy

Shardis is built around three core principles:

1. **Determinism First**:
   Given the same ShardKey, the same shard must always be chosen unless explicitly migrated.

2. **Separation of Concerns**:
   Domain models should never "know" about shards — sharding remains purely an infrastructure concern.

3. **Minimal Intrusion**:
   Shardis integrates into your system without forcing heavy infrastructure or hosting requirements.

---

## 🚧 Roadmap

- [ ] Persistent ShardMapStore options (SQL, Redis)
- [ ] Shard migrator for safe rebalance operations
- [ ] Read/Write split support
- [ ] Multi-region / geo-sharding support
- [ ] Lightweight metrics/telemetry package
- [ ] Benchmarks & performance regression harness

---

## 👨‍💻 Contributing

Pull requests, issues, and ideas are welcome.
If you find an interesting edge case or want to extend Shardis into more advanced scaling patterns, open a discussion or a PR!

See [CONTRIBUTING.md](./CONTRIBUTING.md).

---

## 📊 Benchmarks

BenchmarkDotNet benchmarks live in `benchmarks/`.

Run (from repo root):

```bash
dotnet run -c Release -p benchmarks/Shardis.Benchmarks.csproj --filter *RouterBenchmarks*
dotnet run -c Release -p benchmarks/Shardis.Benchmarks.csproj --filter *HasherBenchmarks*
```

Use these to compare (by `--anyCategories`):

- `router`: Default vs Consistent hash routing cost
- `hasher`: Different ring hash algorithms (Default vs FNV-1a) & replication factor impact
- `migration`: Migration executor throughput across concurrency / batch matrix
- `broadcaster`: Fast vs slow shard streaming (fairness, interleaving, backpressure sensitivity). This suite remains as a baseline ahead of the upcoming ordered vs unordered merge benchmarks.

Planned (in active design):

- `merge`: Ordered vs unordered streaming merge enumerators (k‑way heap vs combined interleave) – will complement (not replace) the broadcaster suite to show impact of global ordering.

After optimization: routing hot path avoids double hashing (via `TryGetOrAdd`) and maintains constant single miss emission under high contention.

---

## 🧪 Testing & Quality

xUnit tests live in `test/Shardis.Tests/` covering:

- Routing correctness
- Consistent hashing determinism
- Metrics invocation
- DI registration & overrides
- Migration planning scaffolding
- Ordered merge enumerator

Run:

```bash
dotnet test
```

Assertion policy: the test suite relies on the `AwesomeAssertions` NuGet package for fluent, deterministic assertions.

Additional invariants covered:

- Single route miss under high concurrency
- Dynamic ring add/remove maintains routing without KeyNotFound or inconsistent assignment
- Deterministic ordering for duplicate keys in ordered merge
- Statistical ring distribution bounds (coefficient of variation heuristic)
- Non-empty broadcaster shard enforcement & null parameter guards

### Public API Stability

All public surfaces across assemblies are snapshotted via `Shardis.PublicApi.Tests` using PublicApiGenerator. Baselines live under `test/PublicApiApproval/*.approved.txt` (committed). When an intentional API change is made:

1. Run `dotnet test -c Debug -p:PublicApi` (or simply `dotnet test`).
2. A `.received` file will be written alongside the affected `.approved` file.
3. Inspect the diff; if intentional, replace the `.approved` content with the `.received` content and delete the `.received` file (the test does this automatically on next green run).

The test auto-creates missing `.approved` files (first run does not fail). Only stable, documented APIs should be added—avoid leaking internal abstractions.

---

## 🔄 Migration (Scaffolding)

Migration implementation now lives in the dedicated `Shardis.Migration` package. The core repository no longer exposes the previous migration stub. For migration work, prefer the `Shardis.Migration` executor which provides an end-to-end execution pipeline (copy, verify, swap) with checkpointing and metrics.

Quick start:

1. Add the migration services in your composition root:
  `services.AddShardisMigration<TKey>();`
2. Use the planner / executor from the package to create a plan and execute it with durable components (data mover, verifier, swapper, checkpoint store).

See `docs/MIGRATION.md`, `docs/adr/0002-key-migration-execution.md` and the `Shardis.Migration` README for examples and operational guidance.

---

## 📡 Broadcasting & Streaming Queries

Two broadcaster abstractions exist today:

- `ShardBroadcaster` – dispatches a synchronous / Task-returning delegate to every shard and aggregates materialized results.
- `ShardStreamBroadcaster` – dispatches async streaming queries (`IAsyncEnumerable<T>` per shard) and yields a merged asynchronous stream without buffering entire shard result sets.

Utility enumerators:

- `ShardisAsyncOrderedEnumerator` – k-way merge for globally ordered streams.
- `ShardisAsyncCombinedEnumerator` – simple interleaving without ordering guarantees.

Higher-level fluent query API (LINQ-like) is under active design (see `docs/api.md` & `docs/linq.md`).

### LINQ MVP (Where / Select Only)

Experimental minimal provider (see ADR 0003 cross-link) allows composing simple per-shard filters and a single projection and executing unordered:

```csharp
var exec = /* IShardQueryExecutor implementation (e.g. InMemory / EFCore) */;
var q = Shardis.Query.ShardQuery.For<Person>(exec)
                               .Where(p => p.Age >= 30)
                               .Select(p => new { p.Name, p.Age });
await foreach (var row in q) { Console.WriteLine($"{row.Name} ({row.Age})"); }
```

Constraints (MVP):

- Only `Where` (multiple) + single terminal `Select`.
- No ordering operators; use `ShardStreamBroadcaster.QueryAllShardsOrderedStreamingAsync` for global ordering or order after materialization.
- Unordered merge semantics identical to `QueryAllShardsAsync`.
- Cancellation respected mid-stream.

Future work (tracked): join support, ordering pushdown, aggregation.

#### Provider Matrix (MVP)

| Provider | Package | Where | Select | Ordering | Cancellation | Metrics Hooks |
|----------|---------|-------|--------|----------|--------------|---------------|
| InMemory | `Shardis.Query.InMemory` | ✅ | ✅ | ❌ (post-filter only) | Cooperative (no throw) | ✅ (OnShardStart/Stop/Items/Completed/Canceled) |
| EF Core  | `Shardis.Query.EFCore`   | ✅ server-side | ✅ server-side | ❌ (use ordered streaming merge for global order) | Cooperative | ✅ |
| Marten (adapter)* | `Shardis.Marten` | ✅ | ✅ | Backend native only (no global merge) | Cooperative | (planned) |

Ordering: for global ordering across shards use `QueryAllShardsOrderedStreamingAsync(keySelector)` (streaming k-way merge) or materialize then order.

### Adaptive Paging & Provider Capabilities

| Provider | Unordered Streaming | Ordered Merge Compatible | Native Pagination | Adaptive Paging | Notes |
|----------|---------------------|---------------------------|------------------|-----------------|-------|
| InMemory | Yes (in-process)    | Yes                       | N/A              | N/A             | Uses compiled expression pipelines. |
| EF Core  | Yes (IAsyncEnumerable) | Yes                    | Yes (Skip/Take)  | Not yet         | Relies on underlying provider translation. |
| Marten   | Yes (paged)         | Yes                       | Yes              | Yes             | Fixed or adaptive paging materializer. |

Adaptive paging (Marten) grows/shrinks page size within configured bounds to keep batch latency near a target window. It is deterministic (pure function of prior elapsed times) and never exceeds `maxPageSize`. Choose:

- Fixed page size: predictable memory footprint, steady workload.
- Adaptive: heterogeneous shard performance, aims to reduce tail latency without overfetching.

### Cancellation Semantics (Queries)

| Aspect | InMemory | EF Core | Marten (Fixed) | Marten (Adaptive) |
|--------|----------|---------|----------------|-------------------|
| Mid-item check | Between MoveNext calls | Provider awaits next row | Before each page & per item | Before each page & per item |
| On cancel effect | Stops yielding, completes gracefully | Stops enumeration, disposes | Stops paging loop | Stops paging loop, retains last page decision state |
| Exception surface | None (cooperative) | OperationCanceledException may bubble internally then swallowed | Swallows after signaling metrics | Same as fixed |

Guidance: always pass a token with timeout for interactive workloads; enumerators honor cancellation promptly.

*Marten executor currently requires a PostgreSQL instance; tests are scaffolded and skipped in CI when no connection is available.

#### Unordered Merge Non-Determinism

Unordered execution intentionally interleaves per-shard results based on arrival timing. For identical logical inputs, interleaving order may vary across runs. Applications requiring deterministic global ordering must either:

1. Use an ordered merge (`ShardisAsyncOrderedEnumerator`) supplying a stable key selector, or
2. Materialize then order results explicitly.

#### Cancellation Semantics

All executors observe `CancellationToken` cooperatively. Enumeration stops early without throwing unless the underlying provider surfaces an `OperationCanceledException`. Metrics observers receive `OnCanceled` exactly once.

#### Query Benchmarks

Run the new query benchmark suite:

```bash
dotnet run -c Release -p benchmarks/Shardis.Benchmarks.csproj --filter *QueryBenchmarks*
```

### Multi-Provider Example (InMemory vs EF vs Marten)

```csharp
// InMemory
var inMemExec = new InMemoryShardQueryExecutor(new[] { shard1Objects, shard2Objects }, UnorderedMerge.Merge);
var inMemQuery = ShardQuery.For<Person>(inMemExec).Where(p => p.Age >= 30).Select(p => p.Name);
var names1 = await inMemQuery.ToListAsync();

// EF Core (Sqlite)
var efExec = new EfCoreShardQueryExecutor(2, shardId => CreateSqliteContext(shardId), UnorderedMerge.Merge);
var efQuery = ShardQuery.For<Person>(efExec).Where(p => p.Age >= 30).Select(p => p.Name);
var names2 = await efQuery.ToListAsync();

// Marten (single shard adapter for now)
using var session = documentStore.LightweightSession();
var martenNames = await MartenQueryExecutor.Instance
  .Execute(session, q => q.Where(p => p.Age >= 30).Select(p => p))
  .Select(p => p.Name)
  .ToListAsync();

// NOTE: Unordered merge => arrival-order, not globally deterministic across shards.
```

**Important:** Unordered execution is intentionally non-deterministic. For deterministic ordering across shards use an ordered merge (`QueryAllShardsOrderedStreamingAsync`) or materialize then order.

### Exception Semantics

Shardis executors use a cooperative cancellation model: when cancellation is requested the async iterator stops yielding without throwing unless the underlying provider surfaces an `OperationCanceledException`. Translation/database/provider exceptions are propagated unchanged. Consumers requiring explicit cancellation signaling should inspect the token externally.

### Ordered Merge

For deterministic cross-shard ordering use `OrderedMergeHelper.Merge` supplying a key selector. Each shard stream must already be locally ordered by that key. The merge performs a streaming k-way heap merge (O(log n) per item, where n = shard count) without materializing full result sets.

### Provider Matrix

| Package | Dependency | Where/Select | Streaming | Ordering | Notes |
|---------|------------|-------------|-----------|----------|-------|
| Shardis.Query | none | ✔️ | n/a | n/a | Core query model & merge helpers |
| Shardis.Query.InMemory | none | ✔️ | ✔️ | ❌ | Dev/test executor |
| Shardis.Query.EFCore | EF Core | ✔️ | ✔️ | ❌ | Server-side translation (Sqlite tests) |
| Shardis.Query.Marten | Marten | ✔️ | ✔️ (paged/native) | ❌ | Async/paged materializer |

### Backpressure

`UnorderedMerge` uses an **unbounded** channel by default for lowest latency. Provide a `channelCapacity` (e.g. 64–256) to enforce backpressure and cap memory:

```csharp
var broadcaster = new ShardStreamBroadcaster<MyShard, MySession>(shards, channelCapacity: 128);
await foreach (var item in broadcaster.QueryAllShardsAsync(s => Query(s))) { /* ... */ }

// Or directly via helper returning merged stream (capacity 128 example):
var merged = UnorderedMergeHelper.Merge(shardStreams, channelCapacity: 128);
await foreach (var row in merged) { /* consume */ }

// Minimal helper creation showing explicit capacity tradeoff
var merge = UnorderedMergeHelper.Merge(shardStreams, channelCapacity: 128); // capacity => more memory, fewer producer stalls (lower tail latency)
```

Guidance:

- 0 / null (unbounded): lowest per-item latency, potential burst amplification.
- 32–64: balance memory vs throughput for medium fan-out.
- 128–256: higher sustained throughput where producers are faster than consumer.
- >512 rarely justified unless profiling shows persistent producer starvation.

Backpressure wait events are surfaced via `IMergeObserver.OnBackpressureWaitStart/Stop` so instrumentation can record stall time. Ordered (k-way) merges and unbounded unordered merges emit zero wait events by design.

### Query Telemetry & Adaptive Paging

Two observer surfaces exist:

| Interface | Purpose |
|-----------|---------|
| `IQueryMetricsObserver` | Lifecycle + item counters (shard start/stop, items produced, completion, cancellation). |
| `IAdaptivePagingObserver` | Adaptive Marten paging decisions (previous size, next size, last batch latency). |

Marten executor can switch between fixed and adaptive paging:

```csharp
var fixedExec = MartenQueryExecutor.Instance.WithPageSize(256);
var adaptiveExec = MartenQueryExecutor.Instance.WithAdaptivePaging(
  minPageSize: 64,
  maxPageSize: 1024,
  targetBatchMilliseconds: 50,
  observer: myAdaptiveObserver);
```

Adaptive strategy grows/shrinks page size deterministically based on prior batch latency relative to a target window. It never exceeds bounds and emits a decision event only when the page size changes.

Additional telemetry (adaptive):

- `OnOscillationDetected(shardId, decisionsInWindow, window)` – high churn signal; consider narrowing grow/shrink factors or widening latency target.
- `OnFinalPageSize(shardId, finalSize, totalDecisions)` – emitted once per shard enumeration for tuning & dashboards.

### Benchmark Allocation Guard

CI runs a smoke allocation benchmark comparing fixed vs adaptive Marten paging. A JSON exporter is parsed and a markdown delta report (`ADAPTIVE-ALLOC-DELTA.md`) is uploaded. Environment thresholds:

- `ADAPTIVE_ALLOC_MAX_PCT` (default 20) – percentage delta guard
- `ADAPTIVE_ALLOC_MIN_BYTES` (default 4096 B/op) – ignore noise below this absolute per-op allocation

Currently advisory / non-blocking. After several green runs remove the fallback `|| echo` in the workflow to make regressions fail the job.

### Merge Modes & Tuning

See `docs/merge-modes.md` for a full matrix. Quick guidance:

```csharp
// Unordered streaming (arrival order, lowest latency)
await foreach (var item in broadcaster.QueryAllShardsAsync(s => Query(s))) { /* ... */ }

// Ordered streaming (bounded memory k-way merge)
await foreach (var item in broadcaster.QueryAllShardsOrderedStreamingAsync(s => Query(s), keySelector: x => x.Timestamp, prefetchPerShard: 2)) { /* ... */ }

// Tuning prefetch:
// 1 => minimal latency & memory (default)
// 2 => balanced latency vs throughput
// 4 => higher throughput if shards intermittently stall

int prefetch = isLowLatencyScenario ? 1 : 2; // rarely >4
await foreach (var item in broadcaster.QueryAllShardsOrderedStreamingAsync(s => Query(s), x => x.Id, prefetch)) { }
```

// Cancellation & Observability
// Early / mid-stream cancellation is tested (no deadlocks, resources released, no leaks via WeakReference probe).
// Metrics observer tests assert heap sampling (>0 for ordered), symmetric backpressure wait events for bounded channels,
// and zero wait events for unbounded / ordered streaming scenarios.

Memory scale: O(shards × prefetch). Increase only if profiling shows the merge heap frequently empty while shards are still producing (starvation).

---

## 🗄 Persistence (Shard Map Stores)

Several `IShardMapStore<TKey>` implementations are (or will be) available:

| Implementation | Package / Location | Use Case |
|----------------|-------------------|----------|
| `InMemoryShardMapStore<TKey>` | Core | Tests, local dev, ephemeral scenarios |
| `RedisShardMapStore<TKey>` | `Shardis.Redis` | Low-latency distributed cache + persistence |
| SQL-backed (planned) | 🚧 | Durable relational storage |

### Redis Example

```csharp
// Add package reference to Shardis.Redis (when published)
services.AddSingleton<IShardMapStore<string>>(sp => new RedisShardMapStore<string>("localhost:6379"));
services.AddShardis<IShard<string>, string, string>(opts =>
{
  opts.Shards.Add(new SimpleShard(new("shard-001"), "postgres://host1/db"));
  opts.Shards.Add(new SimpleShard(new("shard-002"), "postgres://host2/db"));
});
```

The Redis implementation stores assignments as simple string keys under the prefix `shardmap:`. It should be supplemented with persistence / snapshot strategy if you rotate Redis.

Both InMemory and Redis map stores implement `TryGetOrAdd` to minimize the number of hash computations and branch decisions in router hot paths.

---

## 📦 Documentation Index

See `docs/index.md` for a curated set of design and roadmap documents (fluent query API, migration, backlog, benchmarks).

---

## 🏗 Architectural Invariants

- Routing is deterministic (no randomness besides stable hash functions).
- No shard logic leaks into domain models; models are plain data structures.
- All public APIs are documented with XML docs.
- Hashing and ring strategies are pluggable (`IShardKeyHasher<TKey>`, `IShardRingHasher`).
- Metrics capture is optional and zero-cost when using the no-op implementation.
- Consistent hash ring rebuilds (add/remove) swap an immutable key snapshot atomically for lock-free lookups.
- Default router guarantees one `RouteMiss` per key via per-key lock, preserving historical metric semantics.

---

---

## ⚙️ Dependency Injection Options

Configured via `AddShardis<TShard,TKey,TSession>(opts => { ... })`:

| Option | Purpose | Default |
|--------|---------|---------|
| UseConsistentHashing | Toggle consistent vs simple router | true |
| ReplicationFactor | Virtual nodes per shard (ring) | 100 |
| RingHasher | Ring hashing implementation | DefaultShardRingHasher |
| ShardMapStoreFactory | Custom map store factory | InMemoryShardMapStore |
| ShardKeyHasher | Override key -> uint hash | DefaultShardKeyHasher |
| RouterFactory | Provide totally custom router | null |
| Shards | Collection of shards | (empty) |

Overridable services (register before AddShardis):

- `IShardMapStore<TKey>`
- `IShardisMetrics`

---

## 📈 Metrics Integration

Routers report two primitive events through `IShardisMetrics`:

- `RouteMiss(router)` – a key had no prior assignment and hashing/selection occurred.
- `RouteHit(router, shardId, existingAssignment)` – a shard was chosen; `existingAssignment` indicates whether the key already had a stored mapping.

You can plug in your own metrics export by implementing `IShardisMetrics`. A production-ready default using `System.Diagnostics.Metrics` is provided as `MetricShardisMetrics` (register it in DI to enable counters):

```csharp
services.AddSingleton<IShardisMetrics, MetricShardisMetrics>();
```

Exposed counters (names subject to refinement before first NuGet release):

| Counter | Description |
|---------|-------------|
| shardis.route.hits | Total route resolutions (both new + existing assignments) |
| shardis.route.misses | Keys seen for the first time before assignment |
| shardis.route.assignments.existing | Route hits where mapping already existed |
| shardis.route.assignments.new | Route hits that resulted in a new persisted assignment |

Attach these to OpenTelemetry via the .NET Metrics provider or scrape via Prometheus exporters.

---

## 📄 License

**MIT License** — free for personal and commercial use.

---

## 🔢 Versioning & Release Policy (Pre-NuGet Draft)

- Semantic Versioning will be used once packages are published.
- Until the first stable `1.0.0`, minor version bumps (`0.x`) may introduce breaking changes with clear CHANGELOG entries.
- Public APIs with XML docs are considered part of the contract; anything `internal` or undocumented may change.
- Experimental features are tagged in docs and may be excluded from backward compatibility guarantees until stabilized.

Planned publication sequence:

1. `Shardis` (core) – routing, hashing, map stores, metrics.
2. `Shardis.Redis` – Redis map store.
3. `Shardis.Marten` – query executor adapter (post fluent API MVP).
4. Migration utilities (once copy + verify pipeline complete).

---

> _"Because scaling your domain shouldn’t mean scaling your pain."_ 🚀
