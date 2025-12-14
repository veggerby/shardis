# Shardis Health & Resilience Sample

Demonstrates shard health monitoring and resilience capabilities, showing how Shardis can detect, route around, and recover from unhealthy shards.

## What it shows

* Configuring a health policy with periodic probing and failure thresholds
* Using EF Core health probe to check database connectivity
* Best-effort query execution (continues with healthy shards when some fail)
* Strict mode query execution (fails deterministically when any shard is unhealthy)
* Require N-of-M shard availability strategies
* Health metrics and recovery events
* Simulating shard failures and recoveries

## Scenarios

The sample demonstrates three key scenarios:

### 1. Best-Effort Mode
When a shard becomes unhealthy, queries continue using only healthy shards. Results are partial but operations succeed.

### 2. Strict Mode
When a shard becomes unhealthy, queries fail immediately with `InsufficientHealthyShardsException` containing detailed diagnostic information.

### 3. Custom Requirements
Configurable minimum shard availability (e.g., "require at least 2 of 3 shards" or "require 75% healthy").

## Running

```bash
dotnet run --project samples/Shardis.Health.Sample
```

## What to expect

The sample will:

1. Create 3 in-memory SQLite shards and seed them with data
2. Execute a successful query across all healthy shards
3. Simulate shard 1 becoming unhealthy (by corrupting its connection)
4. Demonstrate best-effort mode continuing with 2 of 3 shards
5. Demonstrate strict mode failing with detailed exception
6. Simulate recovery of shard 1
7. Show all shards becoming healthy again

## Key Concepts

* **Health Policy**: Monitors shard health with configurable probe cadence, failure thresholds, and cooldown periods
* **Health Probe**: Provider-specific check (e.g., `EfCoreShardHealthProbe` uses `CanConnectAsync()`)
* **Health-Aware Executor**: Filters shards by health status before query execution
* **Availability Requirements**: Flexible policies from best-effort to strict to custom thresholds
* **Metrics**: Track probe latency, unhealthy shard counts, skip reasons, and recovery events

## Code Highlights

```csharp
// Setup health monitoring
var probe = new EfCoreShardHealthProbe<MyDbContext>(shardFactory);
var policy = new PeriodicShardHealthPolicy(
    probe,
    new ShardHealthPolicyOptions { 
        ProbeInterval = TimeSpan.FromSeconds(5),
        UnhealthyThreshold = 2,
        HealthyThreshold = 1
    },
    shardIds
);

// Best-effort: skip unhealthy shards
var bestEffortExecutor = new HealthAwareQueryExecutor(
    innerExecutor,
    policy,
    HealthAwareQueryOptions.BestEffort
);

// Strict: fail if any shard unhealthy
var strictExecutor = new HealthAwareQueryExecutor(
    innerExecutor,
    policy,
    HealthAwareQueryOptions.Strict
);

// Custom: require at least N shards
var customExecutor = new HealthAwareQueryExecutor(
    innerExecutor,
    policy,
    HealthAwareQueryOptions.RequireMinimum(2)
);
```

## Notes

* Uses in-memory SQLite databases for simplicity (no external dependencies)
* Health checks run every 5 seconds (configurable via `ProbeInterval`)
* Failure threshold set to 2 consecutive failures before marking unhealthy
* Recovery threshold set to 1 consecutive success before marking healthy again
* In production, configure appropriate timeouts, thresholds, and cooldown periods based on your infrastructure
