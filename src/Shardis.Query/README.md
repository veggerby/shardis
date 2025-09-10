# Shardis.Query

Primitives and abstractions for cross-shard query execution in Shardis: streaming enumerators, LINQ helpers, executor interfaces, and ergonomic client helpers.

[![NuGet](https://img.shields.io/nuget/v/Shardis.Query.svg)](https://www.nuget.org/packages/Shardis.Query/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Query.svg)](https://www.nuget.org/packages/Shardis.Query/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Query --version 0.1.*
```

## When to use

- You need streaming cross-shard queries with low memory overhead.
- You want a pluggable executor model across stores (in-memory, EF Core, Marten, …).
- You need merge modes (unordered/ordered) that work with `IAsyncEnumerable<T>`.

## What’s included

 - `IShardQueryExecutor` — low-level executor abstraction.
 - `IShardQueryClient` / `ShardQueryClient` — ergonomic entrypoint (DI friendly) providing `Query<T>()` and inline composition overloads.
 - Executor extensions: `Query<T>()`, `Query<T,TResult>(...)` for direct bootstrap without `ShardQuery.For<T>()`.
 - Terminal extensions: `FirstOrDefaultAsync`, `AnyAsync`, `CountAsync` (client-side aggregation helpers).
 - Streaming merge operators (unordered, ordered) and enumerators.
 - Failure handling strategies (`FailFastFailureStrategy`, `BestEffortFailureStrategy`) with DI decoration helper in EF Core provider.
 - LINQ adapter helpers to build shard-friendly query expressions.

## Quick start

```csharp
// Using the ergonomic client (recommended)
var client = provider.GetRequiredService<IShardQueryClient>();
var adults = client.Query<Person, string>(p => p.Age >= 18, p => p.Name);
var first = await adults.FirstOrDefaultAsync();
var count = await adults.CountAsync();

// Or directly from an executor
var exec = provider.GetRequiredService<IShardQueryExecutor>();
var q = exec.Query<Person>()
            .Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Name });
var any = await q.AnyAsync();
```

## Configuration / Options

 - Merge modes: unordered (fastest) and ordered (global key selector; buffered in EF Core factory `CreateOrdered` preview).
 - Failure handling: decorate an executor with a strategy (e.g. EF Core: `services.DecorateShardQueryFailureStrategy(BestEffortFailureStrategy.Instance)`).
 - Backpressure/channel capacity configurable (unordered path) via provider options (e.g. EF Core `EfCoreExecutionOptions.ChannelCapacity`).

### Cancellation & Timeouts

All async operators accept a `CancellationToken` propagated to underlying providers. Provider-specific timeouts (e.g. EF Core `PerShardCommandTimeout`) are applied per shard.

### Failure Behavior

Fail-fast by default (first exception cancels). Opt into best-effort via provider decorator registration (EF Core: `DecorateShardQueryFailureStrategy`).

### Backpressure

Unordered merge supports bounded buffering via provider options (channel capacity). Use to smooth producer spikes or reduce memory.

### Metrics

Shardis.Query emits both tracing Activities and an OpenTelemetry Histogram for end-to-end merge latency.

Latency histogram:

- Name: `shardis.query.merge.latency`
- Unit: `ms`
- Description: End-to-end duration of merged shard query enumeration (fan-out start to final consumer completion)
- Cardinality guard: recorded exactly once per query enumeration (success, cancellation, or failure)

Tag schema (stable):

- `db.system` – storage system (e.g. `postgresql`)
- `provider` – logical provider (e.g. `efcore`)
- `shard.count` – total configured shards in topology
- `target.shard.count` – shards actually targeted (respects `WhereShard`); equals `shard.count` when not targeted
- `merge.strategy` – `unordered` | `ordered`
- `ordering.buffered` – `true` when ordered path is a buffered/materialized variant
- `fanout.concurrency` – effective parallelism applied (may be lower than configured when targeted shard subset)
- `channel.capacity` – capacity for unordered merge channel (`-1` when unbounded / not applicable)
- `failure.mode` – `fail-fast` | `best-effort` (heuristic if strategy decoration not detectable)
- `result.status` – `ok` | `canceled` | `failed`
- `root.type` – short CLR type name for the query root / projection

Tracing:

- `ActivitySource` name: `Shardis.Query`
- Per-query activity includes overlapping tags (`shard.count`, `target.shard.count`, strategy, etc.) and timing spans.

Enabling (OpenTelemetry example):

```csharp
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("Shardis") // core
    .AddMeter("Shardis.Query") // query-specific
    .AddInMemoryExporter(out var exported) // or Prometheus / OTLP
    .Build();
```

Buckets: by default rely on your metrics backend’s dynamic bucketing; for explicit views apply `[5,10,20,50,100,200,500,1000,2000,5000]` (milliseconds) to the histogram instrument.

## Integration notes

- Pair with a concrete provider package (EF Core, Marten, InMemory) for session creation.
- Register `AddShardisQueryClient()` after configuring an executor to enable ergonomic helpers.
- For early adoption of ordered EF Core queries, use `EfCoreShardQueryExecutor.CreateOrdered` (materializes all results; suitable for bounded sets only).

## Capabilities & limits

- ✅ Streaming across shards with O(shards + channel capacity) memory
- ✅ Pluggable store providers
- ⚠️ Ordered (buffered) EF Core factory currently materializes all shard results (memory trades for simplicity). Avoid for unbounded result sets.
- ⚠️ Ordered streaming (fully streaming k-way merge) is planned; current ordered path is preview.
- 🧩 TFM: `net8.0`, `net9.0`; Shardis ≥ 0.1

## Samples & tests

- Samples: <https://github.com/veggerby/shardis/tree/main/samples>
- Tests: <https://github.com/veggerby/shardis/tree/main/test/Shardis.Query.Tests>

## Versioning & compatibility

- SemVer; see CHANGELOG: <https://github.com/veggerby/shardis/blob/main/CHANGELOG.md>

## Contributing

- See <https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md>

## License

- MIT — see <https://github.com/veggerby/shardis/blob/main/LICENSE>

## Links

- Issues: <https://github.com/veggerby/shardis/issues>
- Discussions: <https://github.com/veggerby/shardis/discussions>
