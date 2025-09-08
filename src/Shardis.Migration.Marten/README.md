# Shardis.Migration.Marten

Marten migration provider for Shardis supplying a document data mover and checksum-based verification strategy.

> Early preview: API surface may evolve before 1.0.

## Included

* `IMartenSessionFactory` – per-shard session creation abstraction.
* `MartenDataMover<TKey>` – copies and verifies documents between shards.
* `DocumentChecksumVerificationStrategy<TKey>` – canonical JSON + stable hash verification.
* `AddMartenMigrationSupport<TKey>()` – DI extension registering the mover + verifier.

## Usage

```csharp
services.AddShardisMigration<string>(o => { /* options */ })
        .AddMartenMigrationSupport<string>();
```

Provide an implementation of `IMartenSessionFactory` that maps `ShardId` to Marten schemas/databases.
