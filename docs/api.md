# 🌟 Shardis Fluent Developer Experience (DX) Vision

---

## 🎯 Goal

Design a **powerful**, **fluent**, and **intuitive** API that allows developers to query and interact with sharded data **as easily as LINQ**, while benefiting from:

- ✅ Automatic sharding
- ✅ Global ordering
- ✅ Streamed results
- ✅ Built-in fault-tolerance
- ✅ Observability
- ✅ Customizability

---

## 🧱 Design Principles

| Principle           | Description                                                                 |
|---------------------|-----------------------------------------------------------------------------|
| **Fluent**          | LINQ-style chainable API, no awkward plumbing                                |
| **Shard-Aware**     | Transparently spans and merges data across all shards                       |
| **Composable**      | Works with `Where`, `OrderBy`, `Select`, etc.                                |
| **Pluggable**       | Easily add telemetry, health checks, fallback rules                          |
| **Async-First**     | All methods return `IAsyncEnumerable<T>` or `Task<T>`                        |
| **Safe**            | Strongly typed; no dynamic strings                                           |
| **Customizable**    | Per-query tuning (e.g. max parallelism, timeout, shard limits)              |

---

## 🔧 Fluent API Example

```csharp
var activeUsers = await shardis
    .For<User>()
    .Where(u => u.IsActive)
    .OrderBy(u => u.CreatedAt)
    .WithOptions(opts => opts
        .WithCancellation(ct)
        .WithMaxShardConcurrency(8)
        .WithShardHealthFallback()
        .TraceWith(metrics)
    )
    .ToListAsync();
```

---

## 🔮 Real-world Use Cases

### 🔍 Streamed Ordered Query

```csharp
await foreach (var user in shardis
    .For<User>()
    .Where(u => u.IsActive)
    .OrderBy(u => u.CreatedAt)
    .AsAsyncEnumerable())
{
    Console.WriteLine(user);
}
```

---

### ⚖️ Aggregate Operations

```csharp
var count = await shardis
    .For<Order>()
    .Where(o => o.Status == OrderStatus.Completed)
    .CountAsync();
```

---

### 🚑 Fault-Tolerant Query with Health Fallback

```csharp
var logs = await shardis
    .For<LogEntry>()
    .WithOptions(o => o.WithShardHealthFallback())
    .ToListAsync();
```

---

### 🌎 Cross-Shard Action Broadcast

```csharp
await shardis
    .For<User>()
    .Where(u => u.NeedsPasswordReset)
    .BroadcastAsync(user => emailService.SendResetEmail(user));
```

---

### 🧩 Provider Agnostic (EF / Marten / Dapper)

```csharp
services.AddShardis<MartenSession>(opts => { ... });
// or
services.AddShardis<DbContext>(opts => { ... });
```

Then:

```csharp
await shardis
    .For<Product>()
    .Using(context => context.Products)
    .Where(p => p.Stock <= 0)
    .ToListAsync();
```

---

## 🛠 Internals Required to Support This

| Component                    | Purpose |
|-----------------------------|---------|
| `IShardQueryable<T>`        | Fluent LINQ-style interface over broadcaster |
| Expression translation       | Compile and dispatch `Where`, `OrderBy`, etc. to shards |
| Query planner                | Determine if query is shard-local or needs merge |
| Merge operators              | Streamed merge via `IShardisAsyncOrderedEnumerator` |
| `WithOptions()` support      | Structured config for parallelism, timeouts, health, telemetry |
| Async extension methods      | `ToListAsync()`, `CountAsync()`, `AnyAsync()`, etc. |
| Diagnostics hooks            | Tracing, Prometheus integration, slow shard detection |

---

## 💡 Summary

> “You should never know you're working on 10+ databases.
> The API should look like LINQ, feel like magic, and scale like Google.”

This vision brings **clarity**, **power**, and **developer delight** to distributed systems in .NET.

---

📌 This document is the **canonical reference for the Shardis DX roadmap**.
