# Shardis

> **Shardis**: _Bigger on the inside. Smarter on the outside._

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
- 🔄 **Consistent Hashing Option**
  Choose between simple sticky routing and a consistent hashing ring with configurable replication factor & pluggable ring hashers.

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

Use these to compare:

- Default vs Consistent hash routing cost
- Different replication factors
- Default vs FNV-1a ring hashing
- Fast vs slow shard streaming (see `BroadcasterStreamBenchmarks`) for throughput / fairness analysis

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

---

## 🔄 Migration (Scaffolding)

Early migration primitives are present (`IShardMigrator`, `DefaultShardMigrator`, `ShardMigrationPlan`). These currently support:

- Planning: build a plan between source and target shard for a set of keys.
- Execution skeleton: iterate keys (hook for data copy + re-assignment logic).

Planned next steps before enabling in production:

1. Data copy orchestration abstraction (`IShardDataTransfer` per domain).
2. Idempotent re-assignment update in map store with optional optimistic lock.
3. Dry-run verification mode with metrics + diff reporting.

---

## 📡 Broadcasting & Streaming Queries

Two broadcaster abstractions exist today:

- `ShardBroadcaster` – dispatches a synchronous / Task-returning delegate to every shard and aggregates materialized results.
- `ShardStreamBroadcaster` – dispatches async streaming queries (`IAsyncEnumerable<T>` per shard) and yields a merged asynchronous stream without buffering entire shard result sets.

Utility enumerators:

- `ShardisAsyncOrderedEnumerator` – k-way merge for globally ordered streams.
- `ShardisAsyncCombinedEnumerator` – simple interleaving without ordering guarantees.

Higher-level fluent query API (LINQ-like) is under active design (see `docs/api.md` & `docs/linq.md`).

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
