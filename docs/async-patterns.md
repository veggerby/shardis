# Async Patterns in Shardis

## Overview

Shardis follows async-first principles for all I/O operations while maintaining synchronous hot paths where appropriate for performance.

## Store Interfaces

### `IShardMapStore<TKey>` (Synchronous)

Used for truly synchronous implementations or when called from synchronous hot paths (routing).

```csharp
public interface IShardMapStore<TKey> where TKey : notnull, IEquatable<TKey>
{
    bool TryGetShardIdForKey(ShardKey<TKey> shardKey, out ShardId shardId);
    ShardMap<TKey> AssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId);
    bool TryAssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId, out ShardMap<TKey> shardMap);
    bool TryGetOrAdd(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, out ShardMap<TKey> shardMap);
}
```

**When to implement:**
- In-memory implementations (no I/O)
- When called from router hot paths
- When wrapping sync-safe APIs (e.g., StackExchange.Redis sync methods)

**Implementations:**
- `InMemoryShardMapStore<TKey>` - concurrent dictionary, no I/O
- `RedisShardMapStore<TKey>` - uses StackExchange.Redis sync methods (internally async-safe)

### `IShardMapStoreAsync<TKey>` (Asynchronous)

**Preferred interface** for implementations that perform I/O operations.

```csharp
public interface IShardMapStoreAsync<TKey> where TKey : notnull, IEquatable<TKey>
{
    ValueTask<ShardId?> TryGetShardIdForKeyAsync(ShardKey<TKey> shardKey, CancellationToken cancellationToken = default);
    ValueTask<ShardMap<TKey>> AssignShardToKeyAsync(ShardKey<TKey> shardKey, ShardId shardId, CancellationToken cancellationToken = default);
    ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryAssignShardToKeyAsync(ShardKey<TKey> shardKey, ShardId shardId, CancellationToken cancellationToken = default);
    ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryGetOrAddAsync(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, CancellationToken cancellationToken = default);
}
```

**Key differences from sync interface:**
- Returns `ValueTask` / `ValueTask<T>` for allocation-free synchronous completion when possible
- No `out` parameters - uses tuples for better async ergonomics
- All methods accept `CancellationToken`
- `TryGetShardIdForKeyAsync` returns `ShardId?` instead of using an out parameter

**When to implement:**
- Database-backed stores (SQL, NoSQL)
- Distributed cache stores requiring network I/O
- Any implementation with inherent async operations

**Implementations:**
- `SqlShardMapStore<TKey>` - async-only (sync methods throw `NotSupportedException`)
- `RedisShardMapStore<TKey>` - implements both interfaces for flexibility
- `InMemoryShardMapStore<TKey>` - implements both, async returns completed `ValueTask`

## Factory Interfaces

### `IShardFactory<T>`

```csharp
public interface IShardFactory<T>
{
    T Create(ShardId shard) => throw new NotSupportedException("Synchronous Create not supported - use CreateAsync.");
    ValueTask<T> CreateAsync(ShardId shard, CancellationToken ct = default);
}
```

**Default behavior:**
- `Create()` throws `NotSupportedException` by default
- Implementations should only override `Create()` if resource creation is truly synchronous

**DI Integration:**
- `DependencyInjectionShardFactory<T>` only implements `CreateAsync`
- Forces async resource creation through DI

## Routing

Routers use the synchronous `IShardMapStore<TKey>` interface for performance:

```csharp
public class DefaultShardRouter<TKey, TSession> : IShardRouter<TKey, TSession>
{
    private readonly IShardMapStore<TKey> _shardMapStore;
    
    public IShard<TSession> RouteToShard(ShardKey<TKey> shardKey)
    {
        // Hot path - synchronous store access
        if (_shardMapStore.TryGetShardIdForKey(shardKey, out var shardId))
        {
            // ...
        }
    }
}
```

**Why synchronous?**
- Routing is a hot path called on every data access
- Store lookups should be fast (in-memory cache, local Redis)
- Avoids async state machine overhead in critical path

**Recommended stores for routing:**
- `InMemoryShardMapStore<TKey>` (development/testing)
- `RedisShardMapStore<TKey>` (production - local Redis, sync methods safe)

## Migration Pipeline

Migration operations are async throughout:

```csharp
public interface IShardMapSwapper<TKey>
{
    Task SwapAsync(IReadOnlyList<KeyMove<TKey>> verifiedBatch, CancellationToken ct);
}
```

All migration components accept `CancellationToken` and use async I/O.

## Best Practices

### ✅ DO

1. **Use `IShardMapStoreAsync<TKey>` for I/O-bound stores**
   ```csharp
   public class MyDbStore<TKey> : IShardMapStoreAsync<TKey>
   {
       public async ValueTask<ShardId?> TryGetShardIdForKeyAsync(...)
       {
           await using var conn = await _db.OpenAsync(cancellationToken);
           // ...
       }
   }
   ```

2. **Implement both interfaces when appropriate**
   ```csharp
   // RedisShardMapStore implements both for flexibility
   public class RedisShardMapStore<TKey> : IShardMapStoreAsync<TKey>, IShardMapStore<TKey>
   {
       // Async methods for non-hot-path scenarios
       public async ValueTask<ShardId?> TryGetShardIdForKeyAsync(...) { }
       
       // Sync methods for router hot path (StackExchange.Redis sync is safe)
       public bool TryGetShardIdForKey(...) { }
   }
   ```

3. **Use `ValueTask` for potential sync completion**
   ```csharp
   // In-memory operations can return completed ValueTask
   public ValueTask<ShardId?> TryGetShardIdForKeyAsync(...)
   {
       return _dict.TryGetValue(key, out var id) 
           ? ValueTask.FromResult<ShardId?>(id)
           : ValueTask.FromResult<ShardId?>(null);
   }
   ```

4. **Propagate cancellation tokens**
   ```csharp
   public async ValueTask<ShardMap<TKey>> AssignShardToKeyAsync(
       ShardKey<TKey> shardKey, 
       ShardId shardId, 
       CancellationToken cancellationToken = default)
   {
       await _db.ExecuteAsync(sql, parameters, cancellationToken);
   }
   ```

### ❌ DON'T

1. **Never use sync-over-async patterns**
   ```csharp
   // ❌ WRONG - deadlock risk
   var result = SomeAsyncMethod().GetAwaiter().GetResult();
   var result = SomeAsyncMethod().Result;
   SomeAsyncMethod().Wait();
   
   // ✅ CORRECT
   var result = await SomeAsyncMethod();
   ```

2. **Don't implement sync wrapper for inherently async operations**
   ```csharp
   // ❌ WRONG - SqlShardMapStore
   public bool TryGetShardIdForKey(...)
   {
       // This would be sync-over-async!
       return TryGetShardIdForKeyAsync(...).GetAwaiter().GetResult();
   }
   
   // ✅ CORRECT
   public bool TryGetShardIdForKey(...)
   {
       throw new NotSupportedException("Use TryGetShardIdForKeyAsync");
   }
   ```

3. **Don't ignore cancellation tokens**
   ```csharp
   // ❌ WRONG
   public async ValueTask DoWorkAsync(CancellationToken ct = default)
   {
       await _db.QueryAsync(sql); // Missing ct!
   }
   
   // ✅ CORRECT
   public async ValueTask DoWorkAsync(CancellationToken ct = default)
   {
       await _db.QueryAsync(sql, ct);
   }
   ```

## Testing

Automated tests detect sync-over-async patterns in source code:

```csharp
[Fact]
public void No_GetAwaiter_GetResult_In_Source_Code() { }

[Fact]
public void No_Task_Result_In_Source_Code() { }

[Fact]
public void No_Task_Wait_In_Source_Code() { }
```

These tests run on every build to prevent regressions.

## Exception: Completed Tasks

The only acceptable use of `.Result` is on tasks that are **already completed** after being awaited:

```csharp
// ✅ ACCEPTABLE - tasks are completed after Task.WhenAll
await Task.WhenAll(firstFetchTasks).ConfigureAwait(false);

for (var i = 0; i < firstFetchTasks.Length; i++)
{
    var r = firstFetchTasks[i].Result; // already completed ← comment required!
    // ...
}
```

**Requirements:**
- Must have explicit comment: `// already completed` or `// safe: already completed`
- Task must be demonstrably completed (e.g., after `Task.WhenAll`)

## Performance Considerations

- **Routing hot path**: Synchronous for minimal overhead
- **Store implementations**: 
  - In-memory: Completed `ValueTask` (no allocation)
  - Redis sync methods: Safe, no actual blocking
  - SQL: Must be async, throws on sync calls
- **Migration**: Fully async, not latency-sensitive

## Migration Guide

If you have existing code using sync patterns:

1. **Check if operation is truly sync**
   - In-memory? Keep sync, add async wrapper
   - I/O (DB, network)? Make async-only

2. **Update interface implementation**
   - Add `IShardMapStoreAsync<TKey>`
   - Implement async methods with `ValueTask`
   - Either keep sync methods or throw `NotSupportedException`

3. **Update callers**
   - Router: Can keep using sync interface (if store supports it)
   - Migration/background: Use async methods
   - Application code: Prefer async

4. **Test**
   - Run `SyncOverAsyncTests` to verify no violations
   - Test both hot paths and I/O paths
