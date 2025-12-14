# Shard Health & Resilience Runtime

This document describes Shardis's production-grade health monitoring and resilience capabilities that enable detection, routing around, and recovery from unhealthy shards without custom scaffolding.

## Overview

The health & resilience runtime provides:

1. **Health Monitoring**: Periodic and reactive health probes to track shard availability
2. **Smart Query Routing**: Health-aware query execution with configurable availability requirements
3. **Failure Strategies**: Best-effort (partial results), strict (fail-fast), and custom threshold modes
4. **Recovery Tracking**: Automatic detection of shard recovery with metrics/tracing
5. **Provider Integration**: Pluggable health probes for EF Core, Marten, Redis, etc.

## Core Abstractions

### Health Status (`ShardHealthStatus`)

Four-state enum representing shard health:

| Status | Meaning | Query Behavior |
|--------|---------|----------------|
| `Unknown` | Initial state, not yet probed | Treated as healthy (optimistic default) |
| `Healthy` | Shard is available and responding | Included in queries |
| `Degraded` | Shard is responding but with issues | Included (future: may support degraded filtering) |
| `Unhealthy` | Shard is unavailable or failing | Excluded based on policy |

### Health Report (`ShardHealthReport`)

Immutable snapshot of shard health at a point in time:

```csharp
public sealed record ShardHealthReport
{
    public required ShardId ShardId { get; init; }
    public required ShardHealthStatus Status { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? Description { get; init; }
    public Exception? Exception { get; init; }
    public double? ProbeDurationMs { get; init; }
}
```

### Health Policy (`IShardHealthPolicy`)

Central interface for monitoring and querying shard health:

```csharp
public interface IShardHealthPolicy
{
    ValueTask<ShardHealthReport> GetHealthAsync(ShardId shardId, CancellationToken ct = default);
    IAsyncEnumerable<ShardHealthReport> GetAllHealthAsync(CancellationToken ct = default);
    ValueTask RecordSuccessAsync(ShardId shardId, CancellationToken ct = default);
    ValueTask RecordFailureAsync(ShardId shardId, Exception exception, CancellationToken ct = default);
    ValueTask<ShardHealthReport> ProbeAsync(ShardId shardId, CancellationToken ct = default);
}
```

**Implementations:**

- `NoOpShardHealthPolicy` (default): Always reports healthy, zero overhead
- `PeriodicShardHealthPolicy`: Full-featured with background probing and reactive tracking

## Periodic Health Policy

The default implementation provides comprehensive health tracking with configurable behavior.

### Configuration Options

```csharp
public sealed record ShardHealthPolicyOptions
{
    public TimeSpan ProbeInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public int UnhealthyThreshold { get; init; } = 3;
    public int HealthyThreshold { get; init; } = 2;
    public TimeSpan CooldownPeriod { get; init; } = TimeSpan.FromSeconds(60);
    public bool ReactiveTrackingEnabled { get; init; } = true;
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `ProbeInterval` | 30s | Time between periodic background probes |
| `ProbeTimeout` | 5s | Maximum time to wait for a probe response |
| `UnhealthyThreshold` | 3 | Consecutive failures before marking unhealthy |
| `HealthyThreshold` | 2 | Consecutive successes before marking healthy |
| `CooldownPeriod` | 60s | Time to wait before re-probing unhealthy shards |
| `ReactiveTrackingEnabled` | true | Track operation success/failure for faster detection |

### State Transitions

```
Unknown ──(first success)──> Healthy
         └(N failures)─────> Unhealthy

Healthy ──(N failures)────> Unhealthy

Unhealthy ─(cooldown)────> [probe retry]
          └(N successes)─> Healthy
```

Where `N` is the configured threshold.

### Health Probes (`IShardHealthProbe`)

Provider-specific probe implementations:

```csharp
public interface IShardHealthProbe
{
    ValueTask<ShardHealthReport> ExecuteAsync(ShardId shardId, CancellationToken ct = default);
}
```

**Built-in Providers:**

- `EfCoreShardHealthProbe<TContext>`: Uses `DbContext.Database.CanConnectAsync()`
- Custom probes for Marten, Redis, or any backend can implement this interface

### Usage Example

```csharp
// Setup health monitoring
var probe = new EfCoreShardHealthProbe<MyDbContext>(shardFactory);
var policy = new PeriodicShardHealthPolicy(
    probe,
    new ShardHealthPolicyOptions 
    { 
        ProbeInterval = TimeSpan.FromSeconds(30),
        UnhealthyThreshold = 3,
        HealthyThreshold = 2,
        CooldownPeriod = TimeSpan.FromSeconds(60)
    },
    shardIds,  // Optional: pre-register shards
    recordProbeLatency: (ms, id, status) => metrics.RecordHealthProbeLatency(ms, id, status),
    recordRecovery: id => metrics.RecordShardRecovered(id)
);
```

## Health-Aware Query Execution

### Query Options (`HealthAwareQueryOptions`)

Three predefined modes plus custom factories:

```csharp
// Default: no health filtering
HealthAwareQueryOptions.Default

// Best-effort: skip unhealthy shards, continue with healthy ones
HealthAwareQueryOptions.BestEffort

// Strict: fail if any shard unhealthy
HealthAwareQueryOptions.Strict

// Custom: require minimum count
HealthAwareQueryOptions.RequireMinimum(2)

// Custom: require minimum percentage
HealthAwareQueryOptions.RequirePercentage(0.75)
```

### Unhealthy Shard Behaviors

| Behavior | Description | Use Case |
|----------|-------------|----------|
| `Include` | Query all shards regardless of health | Default, no filtering |
| `Skip` | Exclude unhealthy shards, continue with healthy | Best-effort partial results |
| `Quarantine` | Fail immediately if any shard unhealthy | Strict consistency requirements |
| `Degrade` | Mark results as partial when shards skipped | Future: explicit degradation signaling |

### Availability Requirements

Fine-grained control over minimum shard availability:

```csharp
public sealed record ShardAvailabilityRequirement
{
    public int MinimumHealthyShards { get; init; }        // e.g., 2
    public double? MinimumHealthyPercentage { get; init; } // e.g., 0.75
    public bool RequireAllHealthy { get; init; }           // strict mode
}
```

### Health-Aware Executor

Wraps any `IShardQueryExecutor` with health filtering:

```csharp
var executor = new HealthAwareQueryExecutor(
    innerExecutor,
    healthPolicy,
    HealthAwareQueryOptions.BestEffort,
    metrics  // optional
);

// Query automatically filters to healthy shards
var results = await executor
    .Query<Product>()
    .Where(p => p.Price > 100)
    .ToListAsync();
```

### Exception on Insufficient Availability

When requirements aren't met, throws detailed exception:

```csharp
try
{
    await foreach (var item in strictExecutor.ExecuteAsync<Product>(query))
    {
        // process
    }
}
catch (InsufficientHealthyShardsException ex)
{
    Console.WriteLine($"Total: {ex.TotalShards}");
    Console.WriteLine($"Healthy: {ex.HealthyShards}");
    Console.WriteLine($"Unhealthy: {string.Join(", ", ex.UnhealthyShardIds)}");
}
```

## Metrics & Tracing

### Health Metrics (`IShardisQueryMetrics` Extensions)

Four new methods track health events:

```csharp
void RecordHealthProbeLatency(double milliseconds, string shardId, string status);
void RecordUnhealthyShardCount(int count);
void RecordShardSkipped(string shardId, string reason);
void RecordShardRecovered(string shardId);
```

### OpenTelemetry Instruments

When using `MetricShardisQueryMetrics`:

| Instrument | Type | Name | Tags |
|------------|------|------|------|
| Probe Latency | Histogram | `shardis.health.probe.latency` | `shard.id`, `health.status` |
| Unhealthy Count | Counter | `shardis.health.unhealthy.count` | none |
| Shard Skipped | Counter | `shardis.health.shard.skipped` | `shard.id`, `reason` |
| Shard Recovered | Counter | `shardis.health.shard.recovered` | `shard.id` |

### Recommended Views

- **Probe success rate**: `sum(rate(shardis.health.probe.latency{health.status="Healthy"})) / sum(rate(shardis.health.probe.latency))`
- **Mean probe latency by shard**: `avg(shardis.health.probe.latency) by (shard.id)`
- **Active unhealthy count**: `shardis.health.unhealthy.count` (latest value)
- **Recovery rate**: `rate(shardis.health.shard.recovered[5m])`

## Design Patterns

### Reactive + Proactive Tracking

The policy supports two modes of health detection:

1. **Proactive (Background Probes)**: Timer-driven periodic checks
2. **Reactive (Operation Tracking)**: Record success/failure from actual queries

Enable both for fastest detection:

```csharp
// In your query executor wrapper:
try
{
    var result = await ExecuteQueryAsync(...);
    await healthPolicy.RecordSuccessAsync(shardId);
    return result;
}
catch (Exception ex)
{
    await healthPolicy.RecordFailureAsync(shardId, ex);
    throw;
}
```

### Circuit Breaker Pattern

The health policy implements a circuit breaker:

- **Closed** (Healthy): Normal operation
- **Open** (Unhealthy): Requests blocked (queries skip shard)
- **Half-Open** (Cooldown): After cooldown, single probe attempted

### Memory Management

**State Dictionary Growth:**

The `PeriodicShardHealthPolicy` maintains a `ConcurrentDictionary<ShardId, ShardHealthState>` that grows as new shards are discovered. For typical deployments with fixed shard counts (tens to hundreds), this is acceptable and allows for dynamic shard discovery.

For very large or dynamic shard sets, consider:
- Pre-registering known shards via constructor
- Implementing a custom policy with LRU eviction
- Periodically recreating the policy instance

**Disposal:**

Always dispose the policy to stop background probes:

```csharp
using var policy = new PeriodicShardHealthPolicy(...);
// use policy
// automatic disposal stops timer and ongoing probes
```

## Sample Scenarios

See `samples/Shardis.Health.Sample` for complete working examples:

### Best-Effort Mode

```csharp
var executor = new HealthAwareQueryExecutor(
    baseExecutor,
    healthPolicy,
    HealthAwareQueryOptions.BestEffort
);

// Query continues with healthy shards even if some are down
var products = await executor.Query<Product>().ToListAsync();
// Returns partial results from available shards
```

### Strict Mode

```csharp
var executor = new HealthAwareQueryExecutor(
    baseExecutor,
    healthPolicy,
    HealthAwareQueryOptions.Strict
);

try
{
    var products = await executor.Query<Product>().ToListAsync();
}
catch (InsufficientHealthyShardsException ex)
{
    // Handle failure with diagnostic info
    LogError($"Query failed: {ex.HealthyShards}/{ex.TotalShards} shards healthy");
}
```

### Custom Requirements

```csharp
// Require at least 3 of N shards
var executor = new HealthAwareQueryExecutor(
    baseExecutor,
    healthPolicy,
    HealthAwareQueryOptions.RequireMinimum(3)
);

// Or require 75% availability
var executor2 = new HealthAwareQueryExecutor(
    baseExecutor,
    healthPolicy,
    HealthAwareQueryOptions.RequirePercentage(0.75)
);
```

## Configuration Recommendations

### Development

```csharp
new ShardHealthPolicyOptions
{
    ProbeInterval = TimeSpan.FromSeconds(5),   // Fast detection
    UnhealthyThreshold = 2,                     // Quick fail
    HealthyThreshold = 1,                       // Quick recovery
    CooldownPeriod = TimeSpan.FromSeconds(10)  // Short cooldown
}
```

### Production

```csharp
new ShardHealthPolicyOptions
{
    ProbeInterval = TimeSpan.FromSeconds(30),   // Reduce probe load
    UnhealthyThreshold = 3,                      // Avoid false positives
    HealthyThreshold = 2,                        // Confirm recovery
    CooldownPeriod = TimeSpan.FromSeconds(60),  // Prevent probe storms
    ReactiveTrackingEnabled = true               // Fast detection via queries
}
```

### High-Availability

```csharp
new ShardHealthPolicyOptions
{
    ProbeInterval = TimeSpan.FromSeconds(10),   // Frequent checks
    ProbeTimeout = TimeSpan.FromSeconds(3),     // Fast failure detection
    UnhealthyThreshold = 2,                      // Quick quarantine
    HealthyThreshold = 3,                        // Conservative recovery
    CooldownPeriod = TimeSpan.FromSeconds(30)   // Moderate backoff
}
```

## Thread Safety & Concurrency

All health policy implementations are thread-safe:

- `PeriodicShardHealthPolicy` uses `ConcurrentDictionary` and lock-based state updates
- Background probes use `CancellationTokenSource` for coordinated disposal
- No fire-and-forget tasks; all background work is cancellation-aware

## Future Enhancements

Planned (see `backlog.md`):

- [ ] Integration with `Microsoft.Extensions.Diagnostics.HealthChecks`
- [ ] Marten-specific health probe implementation
- [ ] Redis health probe implementation
- [ ] Health check aggregation endpoint (ASP.NET Core)
- [ ] Partial result metadata (explicit degradation signaling)
- [ ] Adaptive probe intervals based on stability
- [ ] Health-based load shedding hints

## Related Documentation

- Query Merge Modes: `merge-modes.md`
- Query Latency Metrics: `query-latency.md`
- Migration Usage: `migration-usage.md`
- Sample Code: `samples/Shardis.Health.Sample/README.md`

---

For implementation details see:
- `src/Shardis/Health/` - Core abstractions
- `src/Shardis.Query/Health/` - Query integration
- `src/Shardis.Query.EntityFrameworkCore/Health/` - EF Core probe
- `test/Shardis.Tests/Health/` - Core tests
- `test/Shardis.Query.Tests/Health/` - Query tests
