# 📋 Developer API Implementation Plan

This document outlines the key steps and modules required to make the Fluent Developer API in Shardis fully functional, expressive, and safe.

---

## ✅ Requirements for Realizing the Fluent API Vision

### 1. `IShardQueryable<T>` Interface & Implementation

- Build an internal abstraction similar to `IQueryable<T>` to represent fluent shard-aware queries.
- Track expression tree or delegate pipelines (`Where`, `Select`, `OrderBy`, etc.).
- Capture session context and query execution logic.

---

### 2. Query Expression Parsing

- Parse/compile `Expression<Func<T, bool>>`, `Expression<Func<T, TKey>>`, etc.
- Translate to LINQ or delegate suitable for execution inside:
  - EntityFrameworkCore queries
  - Marten/Linq queries
  - Dapper or raw async delegates

> 🔧 Required: Expression visitor + a strategy per backend (e.g., LINQ vs. Marten vs. raw delegate)

---

### 3. Shard-Aware Execution Engine

- Dispatch the query to all shards **only once expression is complete**.
- If `.OrderBy()` is included:
  - Instruct each shard to locally sort by `keySelector`.
  - Use `IShardisAsyncOrderedEnumerator<T>` to perform k-way streaming merge.

---

### 4. Result Operators

Implement common LINQ-style terminal operators:

| Operator        | Behavior                                      |
|----------------|-----------------------------------------------|
| `ToListAsync()` | Executes query, gathers all results           |
| `AnyAsync()`    | Short-circuits if any shard returns true      |
| `CountAsync()`  | Reduces counts across shards                  |
| `FirstAsync()`  | Uses ordering or returns first from any shard |
| `BroadcastAsync()` | Executes an action across all shards     |

---

### 5. `WithOptions()` Binding

Enable per-query options:

```csharp
.WithOptions(opts => opts
    .WithCancellation(token)
    .WithMaxShardConcurrency(8)
    .WithShardHealthFallback()
    .TraceWith(metrics)
)
```

- Define `ShardQueryOptions` record
- Thread this through the entire pipeline:
  - Session creation
  - Broadcaster
  - Enumerator
  - Health fallback logic

---

### 6. Streamed Merge Infrastructure

- Finalize and generalize `IShardisAsyncOrderedEnumerator<ShardItem<T>>`.
- Allow pluggable sorting, buffering strategies.
- Support both eager (`MergeSortedBy`) and lazy (`StreamingMerge`) paths.

---

### 7. Provider Adapters

Each backing store (EF Core, Marten, Dapper, etc.) needs:

- An `IShardFactory<TSession>` implementation (unified resource creation)
- A way to bind the LINQ/Expression query to the storage engine
- Optional support for ordering inside query delegate

---

### 8. Diagnostics & Telemetry Hooks

Add observer interfaces and default implementations:

| Hook                          | Purpose                          |
|-------------------------------|----------------------------------|
| `IShardQueryTracer`           | Record timings, shard execution  |
| `IShardQueryMetricsCollector` | Aggregate slow shards, errors    |
| `IShardQueryLogger`           | Structured logging for debugging |

---

## 🧱 Work Breakdown Structure

| Area                       | Estimated Effort | Required? |
|----------------------------|------------------|-----------|
| `IShardQueryable<T>`       | ⭐⭐⭐⭐             | ✅        |
| Expression parsing         | ⭐⭐⭐⭐             | ✅        |
| Merge infrastructure       | ⭐⭐⭐              | ✅        |
| Terminal operators         | ⭐⭐⭐              | ✅        |
| `WithOptions()` support    | ⭐⭐               | ✅        |
| Provider adapters          | ⭐⭐⭐⭐             | ✅        |
| Tracing & telemetry        | ⭐⭐               | Optional  |
| Health fallback & retries  | ⭐⭐⭐              | Optional  |

---

## 🚀 Timeline Suggestion (MVP)

| Phase        | Goals                                     |
|--------------|-------------------------------------------|
| **Phase 1**  | `IShardQueryable<T>`, `Where`, `ToListAsync`, `CountAsync` |
| **Phase 2**  | `OrderBy`, merge sorted streaming, `Select` |
| **Phase 3**  | `WithOptions`, telemetry hooks, health fallback |
| **Phase 4**  | `BroadcastAsync`, `GroupBy`, custom joins (advanced) |

---

## 🧪 Example Roadmap Test Case

```csharp
await shardis
    .For<Order>()
    .Where(o => o.Status == OrderStatus.Paid)
    .OrderBy(o => o.CreatedAt)
    .WithOptions(opts => opts.WithMaxShardConcurrency(6))
    .ToListAsync();
```

- ✅ Query parsed to expression tree
- ✅ Routed to all shards
- ✅ Shard streams ordered and merged
- ✅ Results streamed back in correct order

---

## 📌 Notes

- The entire pipeline must remain fully async and streaming where possible.
- Merge logic must never buffer full shard results unless explicitly configured.
- The internal API must remain pluggable: testable without needing storage.

---

Implementation suggestion

---

## ✅ Interface: `IShardQueryable<T>`

```csharp
using System.Linq.Expressions;

namespace Shardis.Querying;

/// <summary>
/// Represents a fluent, composable query across multiple shards.
/// </summary>
public interface IShardQueryable<T>
{
    IShardQueryable<T> Where(Expression<Func<T, bool>> predicate);
    IShardQueryable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
    IShardQueryable<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        where TKey : IComparable<TKey>;

    IShardQueryable<T> WithOptions(Action<ShardQueryOptions> configure);

    Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    Task<T> FirstAsync(CancellationToken cancellationToken = default);
}
```

---

## 🔧 Model: `ShardQueryOptions`

```csharp
public sealed record ShardQueryOptions
{
    public int? MaxShardConcurrency { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public bool EnableTracing { get; init; }
    public bool AllowPartialFailures { get; init; }
}
```

---

## 🧱 Class: `ShardQuery<TSession, T>`

```csharp
using System.Linq.Expressions;

namespace Shardis.Querying;

/// <summary>
/// Internal builder that wraps the composed shard query and executes it via the broadcaster.
/// </summary>
internal sealed class ShardQuery<TSession, T> : IShardQueryable<T>
{
    private readonly IShardStreamBroadcaster<TSession> _broadcaster;
    private readonly Func<TSession, IQueryable<T>> _query;
    private readonly ShardQueryOptions _options = new();

    private Expression<Func<T, bool>>? _where;
    private LambdaExpression? _selector;
    private LambdaExpression? _orderBy;

    public ShardQuery(IShardStreamBroadcaster<TSession> broadcaster, Func<TSession, IQueryable<T>> query)
    {
        _broadcaster = broadcaster;
        _query = query;
    }

    public IShardQueryable<T> Where(Expression<Func<T, bool>> predicate)
    {
        _where = predicate;
        return this;
    }

    public IShardQueryable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector)
    {
        _selector = selector;
        return new ShardQuery<TSession, TResult>(
            _broadcaster,
            session => _query(session).Select(selector));
    }

    public IShardQueryable<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        where TKey : IComparable<TKey>
    {
        _orderBy = keySelector;
        return this;
    }

    public IShardQueryable<T> WithOptions(Action<ShardQueryOptions> configure)
    {
        configure(_options);
        return this;
    }

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var effectiveToken = MergeToken(cancellationToken);

        Func<TSession, IAsyncEnumerable<T>> asyncQuery = session =>
        {
            var queryable = _query(session);

            if (_where != null) queryable = queryable.Where(_where);
            if (_orderBy is LambdaExpression orderExpr)
            {
                queryable = (IQueryable<T>)Queryable.OrderBy(queryable, (dynamic)orderExpr);
            }

            return queryable.AsAsyncEnumerable();
        };

        if (_orderBy is LambdaExpression orderExprCompiled)
        {
            // Use merge-sort enumerator for global ordering
            var keySelector = (dynamic)orderExprCompiled;
            return await _broadcaster
                .QueryAndMergeSortedByAsync(asyncQuery, keySelector, effectiveToken)
                .ToListAsync(effectiveToken);
        }

        return await _broadcaster
            .QueryAllShardsAsync(asyncQuery, effectiveToken)
            .ToListAsync(effectiveToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var token = MergeToken(cancellationToken);

        var query = _query;
        if (_where != null)
        {
            query = session => _query(session).Where(_where);
        }

        var totals = await _broadcaster
            .QueryAllShardsAsync(session => query(session).Select(_ => 1), token)
            .ToListAsync(token);

        return totals.Count;
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        var token = MergeToken(cancellationToken);

        var query = _query;
        if (_where != null)
        {
            query = session => _query(session).Where(_where);
        }

        await foreach (var _ in _broadcaster.QueryAllShardsAsync(session => query(session), token))
        {
            return true;
        }

        return false;
    }

    public async Task<T> FirstAsync(CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(cancellationToken);
        return results.FirstOrDefault()
            ?? throw new InvalidOperationException("Sequence contains no elements.");
    }

    private CancellationToken MergeToken(CancellationToken external)
    {
        if (_options.CancellationToken != default && external != default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(external, _options.CancellationToken);
            return cts.Token;
        }

        return external != default ? external : _options.CancellationToken;
    }
}
```

---

## 🚀 Example Usage (Post-Integration)

```csharp
await shardis
    .For<User>()
    .Where(u => u.IsActive && u.Country == "DK")
    .OrderBy(u => u.LastLogin)
    .WithOptions(opts => opts with { MaxShardConcurrency = 4 })
    .ToListAsync();
```
