# Shardis EF Core Multi-Database Sample

Demonstrates Shardis querying across multiple PostgreSQL databases (one physical database per shard) using Entity Framework Core.

## What it shows

* Database-per-shard provisioning (simple fan-out strategy)
* Seeding distinct data per shard
* Unordered merged query across shards (streaming)
* Global ordering performed client-side after merge
* Manual per-shard paging example (simulated adaptive concept)
* Bounded concurrent merge using channel capacity for backpressure

## Running

Ensure a PostgreSQL server is reachable (devcontainer service name often `db`).

Environment variables (optional):

* `POSTGRES_HOST` (default `localhost`)
* `POSTGRES_PORT` (default `5432`)
* `POSTGRES_USER` (default `postgres`)
* `POSTGRES_PASSWORD` (default `postgres`)
* `POSTGRES_DB_PREFIX` (default `shardis_ef_shard`)

Run:

```bash
POSTGRES_HOST=db dotnet run --project samples/Shardis.Query.Samples.EntityFrameworkCore
```

You should see provisioning logs, seeded databases, merged results (age >= 30), global ordering, and a manual paging demonstration.

To see bounded concurrent merge (channel backpressure) the sample also runs a merge with capacity=8.

## Notes

* Each run recreates shard databases to ensure schema consistency; prefer migrations in real systems.
* Unordered merge uses `UnorderedMergeHelper` which interleaves shard results as they arrive.
* Global ordering requires materializing results (memory vs determinism trade-off).
* Manual paging illustrates per-shard paging mechanics; adaptive strategies could adjust page size at runtime.
* Bounded merge limits in-flight buffered items to control memory/backpressure during large fan-out queries.
