# Shardis.Migration.Durable.Sample

Demonstrates a durable migration with:

* Persistent checkpoint storage (SQL table)
* Durable shard map store (SQL)
* EF Core-based checksum verification (canonical JSON + FNV1A64 hashing)
* Resume behavior (run twice, second run should be fast / no-op)

## Prerequisites

Devcontainer Postgres instance (already running) with connection string:

```text
Host=db;Port=5432;Username=postgres;Password=postgres;Database=shardis
```

## Run

First run seeds data and executes migration:

```bash
dotnet run --project samples/Shardis.Migration.Durable.Sample
```

Second run demonstrates resume (no work left):

```bash
dotnet run --project samples/Shardis.Migration.Durable.Sample
```

## What it shows

1. Keys start on logical shard `source` (discriminator column `Shard`)
2. Plan moves them to shard `target`
3. Copy + checksum verify interleaved
4. Progress persisted to `migration_checkpoints` table
5. Shard map persisted to `migration_shard_map` table

## Tables created

* `user_profiles` – source + target rows distinguished by `Shard` column
* `migration_checkpoints` – JSON payload storing per-step state
* `migration_shard_map` – durable key→shard mapping

## Reset

To start over:

```bash
psql "$CONN" -c "TRUNCATE TABLE migration_checkpoints; TRUNCATE TABLE migration_shard_map; TRUNCATE TABLE user_profiles;"
```

(Adjust `$CONN` accordingly.)
