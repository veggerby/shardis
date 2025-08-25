# 📘 Shardis Fluent Query API – Vision, Architecture & Roadmap

## ✨ Overview

This document captures the future design and implementation plan for a **sharded LINQ execution model** in **Shardis**. It introduces a fluent, LINQ-style API via `ShardQuery` and `IShardQueryable<T>`, allowing developers to execute distributed queries across multiple shards with full backend pluggability, ordering support, and deferred execution.

---

## 📐 Core Abstractions

### 🔹 `IShardQueryable<T>`

A LINQ-like queryable interface that **accumulates query expressions** without being tied to a specific shard/session:

```csharp
public interface IShardQueryable<T>
{
    IShardQueryable<T> Where(Expression<Func<T, bool>> predicate);
    IShardQueryable<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector);
    IShardQueryable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);

    Task<List<T>> ToListAsync(IShardQueryOrchestrator orchestrator, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> AsAsyncEnumerable(IShardQueryOrchestrator orchestrator, CancellationToken cancellationToken = default);
}
```

---

### 🔹 `ShardQuery`

Entry point for building shard-aware LINQ queries:

```csharp
public static class ShardQuery
{
    public static IShardQueryable<T> For<T>() => new ShardQueryable<T>();
}
```

---

### 🔹 `ShardQueryPlan<T>`

Captures the **accumulated query**:

* List of `Expression` trees (Where, OrderBy, Select, etc.)
* Flags for ordering, projections, pagination
* Used by orchestrators and executors

---

### 🔹 `IShardQueryOrchestrator`

Central coordinator of sharded query execution:

```csharp
public interface IShardQueryOrchestrator
{
    Task<List<T>> ExecuteToListAsync<T>(ShardQueryPlan<T> plan, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> ExecuteAsyncEnumerable<T>(ShardQueryPlan<T> plan, CancellationToken cancellationToken = default);
}
```

---

### 🔹 `IShard<TSession>`

Represents a single shard with its own session factory:

```csharp
public interface IShard<out TSession>
{
    ShardId ShardId { get; }
    TSession CreateSession();
}
```

---

### 🔹 `IShardQueryExecutor<TSession>`

Executes LINQ expressions against a backend-specific session:

```csharp
public interface IShardQueryExecutor<TSession>
{
    IQueryable<T> BuildQueryable<T>(TSession session);
}
```

---

## 🔁 Integration with Existing Components

| Type                                       | Status        | Notes                                                         |
| ------------------------------------------ | ------------- | ------------------------------------------------------------- |
| `ShardStreamBroadcaster<TShard, TSession>` | ✅ Still valid | May be used internally as a broadcaster base.                 |
| `IShardBroadcaster<TSession>`              | ✅ Still valid | Functional for low-level delegation or for raw async queries. |
| `IShardQueryOrchestrator`                  | 🌟 New        | Centralizes sharded LINQ execution logic.                     |
| `IShardQueryExecutor<TSession>`            | 🔁 Evolves    | Now handles LINQ-to-backend translation via IQueryable.       |
| `IShardQueryable<T>`                       | 🌟 New        | Unified user-facing API for fluent sharded querying.          |
| `ShardQueryPlan<T>`                        | 🌟 New        | Serves as the blueprint for deferred LINQ queries.            |

---

## 🧭 Query Lifecycle

```csharp
var results = await ShardQuery
    .For<User>()
    .Where(u => u.IsActive)
    .OrderBy(u => u.CreatedAt)
    .Select(u => new { u.Id, u.Name })
    .ToListAsync(orchestrator);
```

1. `ShardQuery` creates a `ShardQueryable<T>`, which accumulates expressions into a `ShardQueryPlan<T>`.
2. When executed, the orchestrator:

   * Retrieves all `IShard<TSession>` instances.
   * Creates sessions.
   * Uses `IShardQueryExecutor<TSession>` to convert the plan into `IQueryable<T>`.
   * Executes the queries per shard (async).
   * Optionally performs **k-way merge** across shards for ordered results.

---

## ⚙️ Execution Semantics

| Capability           | Description                                                                |
| -------------------- | -------------------------------------------------------------------------- |
| Expression capture   | Uses standard expression trees (Where, Select, OrderBy)                    |
| Ordering             | OrderBy expression is applied both per-shard and in merge (k-way strategy) |
| Streaming            | Query may yield results as `IAsyncEnumerable<T>`                           |
| Backend Pluggability | Executors can support EF Core, Marten, Dapper, RavenDB, etc.               |
| Exception Handling   | To support partial success, fail-fast, or retry-based policies             |

---

## ⚠️ Key Design Decisions & Refinements

| Area                      | Decision or Task                                                                             |
| ------------------------- | -------------------------------------------------------------------------------------------- |
| LINQ to Async             | Do not depend on `System.Linq.Async`. Executors are responsible for async conversion.        |
| Ordering                  | Use compiled expression (`Func<T, TKey>`) and extract expression for `OrderBy` at execution. |
| Merge Sort Logic          | Apply `ShardisOrderedEnumerator<T>` after shard-local ordering.                              |
| Executor Responsibilities | Each `IShardQueryExecutor` builds and executes query per session.                            |
| Fault Tolerance           | Orchestrator should support retry or soft failure configuration.                             |

---

## 📈 Future Extensions

* Paging (`Skip/Take`) across shards
* GroupBy and Aggregates (Count, Max, Min)
* Caching and query reuse
* Index hinting or metadata filtering
* Soft shard failover
* Projection analysis and pushdown

---

## ✅ Implementation Plan

1. Implement:

   * `IShardQueryable<T>`
   * `ShardQueryPlan<T>`
2. Build:

   * `ShardQuery`
   * Simple `ShardQueryOrchestrator`
3. Create:

   * `IShardQueryExecutor<TSession>` for one backend (e.g., Marten)
4. Wire up:

   * `ToListAsync`, `AsAsyncEnumerable`
   * Global merge logic
5. Validate:

   * Test ordering
   * Test exceptions
   * Validate expression-to-query translation

---

## 🎯 Final Developer API Vision

```csharp
await ShardQuery
    .For<Order>()
    .Where(o => o.Region == "EU")
    .OrderBy(o => o.Timestamp)
    .Select(o => new { o.Id, o.Total })
    .ToListAsync(orchestrator);
```

* 🔄 Fluent
* 💡 LINQ-based
* 🔌 Pluggable backends
* 🧠 Optimized ordering via k-way merge
* 🚀 No shard-specific knowledge needed by developer
