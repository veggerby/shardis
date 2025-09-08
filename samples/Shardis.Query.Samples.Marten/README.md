# Shardis Marten Multi-Database Sample

Demonstrates querying across multiple PostgreSQL databases (one physical database per shard) using Marten + Shardis abstractions with sequential fan-out and adaptive paging.

## What it shows

* Database-per-shard provisioning (automatic create if missing)
* Distinct per-shard document seeding
* Sequential fan-out query (unordered merge by shard order)
* Global in-memory ordering after merge
* Adaptive paging example (per-shard dynamic page size)

## Running

Ensure a PostgreSQL server is reachable (in devcontainer typically `db`). Optional environment variables:

* `POSTGRES_HOST` (default `localhost`)
* `POSTGRES_PORT` (default `5432`)
* `POSTGRES_USER` (default `postgres`)
* `POSTGRES_PASSWORD` (default `postgres`)
* `POSTGRES_DB_PREFIX` (default `shardis_marten_shard`)

Run:

```bash
POSTGRES_HOST=db dotnet run --project samples/Shardis.Query.Samples.Marten
```

You should see database provisioning, per-shard seeding, unordered (shard-order) results >=30, a global ordered list, and an adaptive paging pass.

## Notes

* Each shard uses its own Marten `DocumentStore`; sessions are opened sequentially for determinism.
* Adaptive paging uses `MartenQueryExecutor.WithAdaptivePaging` to tune page size within provided bounds.
* Concurrency: For simplicity this sample does not concurrently query shards; a bounded concurrent merge variant can be added similarly to the EF Core sample.
* Global ordering requires materializing results in memory (trade-off for deterministic ordering).
