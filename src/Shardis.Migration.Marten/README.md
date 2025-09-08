# Shardis.Migration.Marten

Marten migration provider for Shardis supplying a document copy mover plus checksum-based verification strategy.

> Early preview: API surface may evolve before 1.0. Expect stabilization once API approval snapshot is published for this package.

## Install

```bash
dotnet add package Shardis.Migration.Marten --version 0.2.*
```

## When to use

* You store tenant / customer documents in Marten (PostgreSQL) and need to rebalance shard ownership.
* You want deterministic, content-based verification (canonical JSON checksum) instead of rowversion/timestamp semantics.
* You require a pluggable projection step to reshape documents during migration (e.g., field normalization) before verification.
* You already use the core `Shardis.Migration` executor and just need a Marten adapter.

## What’s included

* `IMartenSessionFactory` – abstraction for per-shard query & document sessions (schema-per-shard or database-per-shard patterns).
* `MartenDataMover<TKey>` – copy-only mover (delegates verification to a separate strategy to avoid duplication).
* `DocumentChecksumVerificationStrategy<TKey>` – canonical JSON + stable hash verification (FNV-1a 64 by default when registered globally).
* `AddMartenMigrationSupport<TKey>()` – DI extension that wires mover + checksum verification strategy (does not override existing projection / canonicalizer / hasher if already registered).

## Quick start

```csharp
// 1. Register Shardis core + migration + Marten adapter
services.AddShardis<string>()
                .AddShardisMigration<string>(o =>
                {
                        o.CopyConcurrency = 8;
                        o.VerifyConcurrency = 4;
                        o.SwapBatchSize = 32;
                })
                .AddMartenMigrationSupport<string>();

// 2. Provide an IMartenSessionFactory (schema-per-shard example)
services.AddSingleton<IMartenSessionFactory>(sp => new MySessionFactory(connectionString));

// 3. Execute a plan (simplified)
var executor = provider.GetRequiredService<ShardMigrationExecutor<string>>();
MigrationSummary summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
Console.WriteLine($"Migrated {summary.Done} keys");
```

## IMartenSessionFactory responsibilities

* Map a `ShardId` to an `IDocumentStore` (schema name, or separate database).
* Return lightweight sessions (`LightweightSession()` or `QuerySession()`) for copy / verify phases.
* Must be thread-safe: concurrent calls during copy/verify.
* Should cache `IDocumentStore` instances (expensive to create).

## Canonical checksum verification

`DocumentChecksumVerificationStrategy<TKey>` loads the source & target documents, applies the configured `IEntityProjectionStrategy`, then:

1. Canonicalizes the projected object graph to deterministic UTF-8 JSON (stable ordering; invariant number formatting; excludes runtime-only fields if your projection removes them).
2. Hashes the bytes via `IStableHasher` (default FNV-1a 64).
3. Compares the two hash values; a mismatch => verification failure.

You can replace any of these components by registering your own implementations before calling `AddMartenMigrationSupport<TKey>()`:

```csharp
services.AddSingleton<IStableCanonicalizer, MyCanonicalizer>();
services.AddSingleton<IStableHasher, XxHash64Hasher>();
services.AddSingleton<IEntityProjectionStrategy, MyProjection>();
services.AddMartenMigrationSupport<string>();
```

## Strategy matrix

| Capability | Supported | Notes |
|-----------|-----------|-------|
| RowVersion / ETAG | N/A | Marten documents do not expose a universal rowversion; checksum used instead. |
| Canonical JSON checksum | Yes | Default strategy. |
| Custom projection | Yes | Replace `IEntityProjectionStrategy`. |
| Partial-field hashing | Yes (custom canonicalizer / projection) | Provide filtered projection that omits large blobs. |
| Adaptive re-copy on mismatch | Executor-level retries | Fails verification → copy retried by executor (up to MaxRetries). |

## Testing locally

Set a Postgres connection string (created database user must have schema create rights):

```bash
export SHARDIS_TEST_PG="Host=localhost;Port=5432;Database=shardis_mig;Username=postgres;Password=postgres"
dotnet test --filter MartenExecutorIntegrationTests
```

If the env var is missing the Marten tests no-op (fast CI path).

## Projection guidance

Use projection to:

* Normalize casing or culture-sensitive fields.
* Drop volatile fields (timestamps, last-access). Ensure both sides apply the same transformation.
* Map legacy field names during phased migrations.

Projection must be deterministic and free of randomness or time-based values; otherwise checksum drift will cause verification failures.

## Telemetry tags

When telemetry integration is enabled (custom `IShardMigrationMetrics` / tracing observer), add tags:

* `backend = Marten`
* `provider = Shardis.Migration.Marten`

Implementation of tag emission lives outside this package (metrics implementation) but these are the canonical keys.

## Limitations

* No built-in bulk/batch fetch; documents copied one at a time (optimize by supplying a mover variant if needed).
* Large binary fields inflate canonicalization cost—exclude them via projection.
* Checksum strategy loads full documents; future optimization may stream JSON tokens directly.

## Samples & references

* Integration tests (`MartenExecutorIntegrationTests`) demonstrate end-to-end copy → verify → swap flow.
* See `docs/migration-usage.md` for executor configuration details common across providers.

## Contributing

Open issues with: desired optimization, alternate hashing strategies, or projection helpers. PRs should include unit tests and (if public surface changes) updated API approval snapshots.

## License

MIT – see root repository license.
