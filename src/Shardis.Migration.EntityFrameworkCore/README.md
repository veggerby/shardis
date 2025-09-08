# Shardis.Migration.EntityFrameworkCore

Entity Framework Core migration provider for Shardis. Supplies data mover and verification strategies (rowversion, checksum) for shard key migrations.

## Included

* `EntityFrameworkCoreDataMover` — per-key copy + upsert
* `RowVersionVerificationStrategy` — fast binary timestamp comparison
* `ChecksumVerificationStrategy` — canonical JSON + stable hash (see root `docs/canonicalization.md` for invariants)
* DI extensions for registration

## Quick start

```csharp
services.AddShardisMigration<Guid>()
        .AddEntityFrameworkCoreMigrationSupport<Guid, MyShardDbContext, MyEntity>();
```

Swap to checksum:

```csharp
services.AddEntityFrameworkCoreChecksumVerification<Guid, MyShardDbContext, MyEntity>();
```
