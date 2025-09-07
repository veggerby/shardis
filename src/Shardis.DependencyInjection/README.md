# Shardis.DependencyInjection

Lightweight fluent registration helpers for per-shard resource factories (e.g. DbContext, IDocumentSession, Redis connections) integrated with Microsoft.Extensions.DependencyInjection.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.DependencyInjection --version 0.1.*
```

## When to use

* You need to provision one resource instance per logical shard (EF Core DbContext, Marten session, etc.)
* You want strongly-typed `IShardFactory<T>` resolution via DI
* You prefer fluent bulk shard registration rather than manual loops
* You want deterministic shard id enumeration at runtime

## What’s included

* `IShardFactory<T>` resolution (implemented by `DependencyInjectionShardFactory<T>`)
* `AddShard` / `AddShards` (sync + async variants) fluent registration methods
* `AddShardInstance` for supplying a pre-created singleton-per-shard instance
* `GetRegisteredShards<T>()` enumeration helper
* `UseAsync` helpers for safe create/use/dispose of `IAsyncDisposable` shard resources

## Quick start

```csharp
var services = new ServiceCollection();

// Register 4 shards with synchronous factory
services.AddShards<MyDbContext>(4, shard => new MyDbContext(BuildOptionsFor(shard)));

// Or async creation per shard
services.AddShard<MyDbContext>(new ShardId("primary"), async (sp, shard) =>
{
    await Task.Yield();
    return new MyDbContext(BuildOptionsFor(shard));
});

var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IShardFactory<MyDbContext>>();

await using var ctx = await factory.CreateAsync(new ShardId("2"));
```

## Configuration / Options

All registration methods are additive; duplicate shard ids throw `InvalidOperationException`.

Patterns:

* Sync delegate: `Func<ShardId, T>` (wrapped in completed ValueTask)
* Async delegate: `Func<IServiceProvider, ShardId, ValueTask<T>>`
* Instance: constant per shard

## Integration notes

* Depends only on core `Shardis` abstractions + `Microsoft.Extensions.DependencyInjection`
* Works alongside `Shardis.Query.*` packages — provision sessions/contexts, then plug into query executors
* Deterministic: no randomness in mapping; enumeration order is registration order (by shard id natural ordering)

## Capabilities & limits

* ✅ Thread-safe registration (startup only) & lookup
* ✅ Per-type isolation (registries are generic)
* ⚠️ No dynamic shard add/remove post-build (scope: startup time)
* 🧩 Compatibility: .NET 8.0, .NET 9.0

## Samples & tests

Samples use standard service registration patterns (see repository samples folder). Add specific DI usage samples in future iterations.

## Versioning & compatibility

* Target frameworks: `net8.0`, `net9.0`
* Follows parent repo semantic versioning schedule

## Contributing

PRs welcome. See contribution guidelines: [CONTRIBUTING.md](https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md)

## License

MIT — see [LICENSE](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Links

* Repository: <https://github.com/veggerby/shardis>
* Issues: <https://github.com/veggerby/shardis/issues>
* Changelog: <https://github.com/veggerby/shardis/blob/main/CHANGELOG.md>
