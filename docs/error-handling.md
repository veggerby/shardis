# Error Handling & Exception Contract

Shardis provides a structured exception hierarchy with rich diagnostic context to help you troubleshoot issues quickly and build resilient sharding infrastructure.

## Exception Hierarchy

All Shardis framework exceptions derive from `ShardisException`, which provides:

- **Consistent base type** for catching all framework errors
- **Diagnostic context** as a read-only dictionary with failure details
- **Inner exception support** for wrapping underlying causes

```csharp
public class ShardisException : Exception
{
    public IReadOnlyDictionary<string, object?> DiagnosticContext { get; }
    // ...
}
```

### Specialized Exception Types

Shardis provides domain-specific exceptions for different subsystems:

| Exception Type | Thrown When | Key Diagnostic Fields |
|----------------|-------------|----------------------|
| `ShardRoutingException` | Routing failures (duplicate shard IDs, empty ring) | `ShardId`, `KeyHash`, `ShardCount` |
| `ShardStoreException` | Persistence/map store failures | `Operation`, `ShardId`, `AttemptCount` |
| `ShardQueryException` | Query execution failures | `Phase`, `ShardId`, `TargetedShardCount` |
| `ShardTopologyException` | Topology validation failures | `TopologyVersion`, `KeyCount`, `MaxKeyCount` |
| `ShardMigrationException` | Migration operation failures | `Phase`, `SourceShardId`, `TargetShardId`, `AttemptCount`, `PlanId` |
| `InsufficientHealthyShardsException` | Insufficient healthy shards for query | `TotalShards`, `HealthyShards`, `UnhealthyShardIds` |

## Using Diagnostic Context

Each exception includes a `DiagnosticContext` property containing structured failure information:

```csharp
try
{
    var shard = router.RouteToShard(shardKey);
}
catch (ShardRoutingException ex)
{
    // Access typed properties
    var shardId = ex.ShardId;
    var keyHash = ex.KeyHash;
    var shardCount = ex.ShardCount;
    
    // Or access the diagnostic context dictionary
    foreach (var (key, value) in ex.DiagnosticContext)
    {
        logger.LogError("Diagnostic: {Key} = {Value}", key, value);
    }
    
    // Re-throw or handle
    throw;
}
```

### Example: Routing Failure with Diagnostic Context

```csharp
try
{
    var router = new ConsistentHashShardRouter<MyShard, string, MySession>(
        mapStore,
        shards,
        keyHasher,
        replicationFactor: 15_000 // Too high!
    );
}
catch (ShardRoutingException ex)
{
    // ex.Message: "ReplicationFactor greater than 10,000 is not supported (pathological ring size)."
    // ex.DiagnosticContext["ReplicationFactor"]: 15000
}
```

### Example: Topology Validation

```csharp
try
{
    var snapshot = await store.ToSnapshotAsync(maxKeys: 100);
}
catch (ShardTopologyException ex)
{
    // ex.Message: "Snapshot key cap 100 exceeded (observed 150). Configure a higher limit if intentional."
    // ex.KeyCount: 150
    // ex.MaxKeyCount: 100
}
```

### Example: Query Execution

```csharp
try
{
    var result = await shardQuery.FirstAsync();
}
catch (ShardQueryException ex)
{
    // ex.Message: "Sequence contains no elements."
    // ex.Phase: "Execution"
}
```

### Example: Migration Failure

```csharp
try
{
    var summary = await migrationExecutor.ExecuteAsync(plan, progress, ct);
}
catch (ShardMigrationException ex)
{
    // ex.Phase: "Copy", "Verify", "Swap", or "Checkpoint"
    // ex.SourceShardId: The source shard
    // ex.TargetShardId: The target shard
    // ex.AttemptCount: Number of retries attempted
    // ex.PlanId: Migration plan identifier
}
```

## Error Handling Guarantees

### Determinism

Shardis exceptions are **deterministic** — the same input conditions always produce the same exception type with the same message format. This makes error handling predictable and testable.

### Thread Safety

All exception types are **thread-safe** and **immutable** after construction. The `DiagnosticContext` is a read-only dictionary that cannot be modified after the exception is created.

### No Data Leakage

Diagnostic context **never contains sensitive data** like passwords, keys, or internal state. It includes only metadata needed for troubleshooting (e.g., shard IDs, counts, phase names).

## Best Practices

### 1. Catch Specific Exception Types

Prefer catching specific exception types over the base `ShardisException`:

```csharp
try
{
    var shard = router.RouteToShard(shardKey);
}
catch (ShardRoutingException ex)
{
    // Handle routing-specific failures
    logger.LogError(ex, "Routing failed for key {Key}", shardKey);
}
catch (ShardisException ex)
{
    // Catch-all for other Shardis failures
    logger.LogError(ex, "Shardis operation failed");
}
```

### 2. Log Diagnostic Context

Always log the diagnostic context for production troubleshooting:

```csharp
catch (ShardisException ex)
{
    logger.LogError(ex, "Shardis error: {Message}. Context: {@DiagnosticContext}", 
        ex.Message, 
        ex.DiagnosticContext);
}
```

### 3. Use Typed Properties When Available

Specialized exceptions provide typed properties for common diagnostic fields:

```csharp
catch (ShardRoutingException ex) when (ex.ShardId != null)
{
    // Typed access is better than dictionary lookup
    var shardId = ex.ShardId.Value; // string
    var keyHash = ex.KeyHash?.ToString("X8"); // hex format
}
```

### 4. Don't Swallow Exceptions

Shardis exceptions indicate real problems that need attention. Avoid empty catch blocks:

```csharp
// ❌ Bad
try
{
    var shard = router.RouteToShard(shardKey);
}
catch (ShardRoutingException)
{
    // Silent failure - debugging nightmare!
}

// ✅ Good
try
{
    var shard = router.RouteToShard(shardKey);
}
catch (ShardRoutingException ex)
{
    logger.LogError(ex, "Routing failed");
    // Optionally re-throw, return default, or take corrective action
    throw;
}
```

### 5. Handle Health Exceptions Gracefully

Query health exceptions are expected in degraded scenarios:

```csharp
try
{
    var results = await queryClient.QueryAsync<MyEntity>(query);
}
catch (InsufficientHealthyShardsException ex)
{
    logger.LogWarning(
        "Query aborted: {Healthy}/{Total} shards healthy. Unhealthy: {Unhealthy}",
        ex.HealthyShards,
        ex.TotalShards,
        string.Join(", ", ex.UnhealthyShardIds.Select(id => id.Value)));
    
    // Return partial results, retry, or fail gracefully
}
```

## Integration Samples

### Example: Resilient Routing with Retry

```csharp
public async Task<IShard<TSession>> RouteWithRetryAsync<TSession>(
    IShardRouter<string, TSession> router,
    ShardKey<string> key,
    int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            return router.RouteToShard(key);
        }
        catch (ShardRoutingException ex) when (attempt < maxRetries)
        {
            logger.LogWarning(ex, 
                "Routing attempt {Attempt}/{Max} failed. Context: {@Context}",
                attempt, maxRetries, ex.DiagnosticContext);
            
            await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
        }
    }
    
    throw new InvalidOperationException($"Routing failed after {maxRetries} attempts");
}
```

### Example: Circuit Breaker for Query Health

```csharp
public class HealthCircuitBreaker
{
    private int _consecutiveFailures;
    private DateTime? _circuitOpenUntil;
    
    public async Task<IEnumerable<T>> QueryWithCircuitBreakerAsync<T>(
        Func<Task<IEnumerable<T>>> queryFunc)
    {
        if (_circuitOpenUntil.HasValue && DateTime.UtcNow < _circuitOpenUntil.Value)
        {
            throw new InvalidOperationException("Circuit breaker is open");
        }
        
        try
        {
            var results = await queryFunc();
            _consecutiveFailures = 0;
            _circuitOpenUntil = null;
            return results;
        }
        catch (InsufficientHealthyShardsException ex)
        {
            _consecutiveFailures++;
            
            if (_consecutiveFailures >= 3)
            {
                _circuitOpenUntil = DateTime.UtcNow.AddMinutes(5);
                logger.LogError(ex, "Circuit breaker opened after {Failures} failures", _consecutiveFailures);
            }
            
            throw;
        }
    }
}
```

## Observability Integration

Diagnostic context integrates seamlessly with structured logging and APM tools:

```csharp
// Serilog
catch (ShardisException ex)
{
    Log.Error(ex, "Shardis error in {Operation}. Context: {@DiagnosticContext}",
        operationName, ex.DiagnosticContext);
}

// Application Insights
catch (ShardisException ex)
{
    var telemetry = new ExceptionTelemetry(ex);
    foreach (var (key, value) in ex.DiagnosticContext)
    {
        telemetry.Properties[key] = value?.ToString();
    }
    telemetryClient.TrackException(telemetry);
}
```

## Related Documentation

- [Health & Resilience](health-resilience.md) — Health probes and query availability requirements
- [Migration Usage](migration-usage.md) — Migration error handling and checkpointing
- [Consistency Contract](consistency-contract.md) — Consistency guarantees and error recovery
