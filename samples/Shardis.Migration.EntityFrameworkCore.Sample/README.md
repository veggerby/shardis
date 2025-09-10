# Shardis.Migration.EFCore.Sample

Entity Framework Core migration sample demonstrating:

1. Rebalancing a skewed key distribution (90/10 across 2 shards) to an even distribution.
2. Adding a new shard (expanding from 2 to 3 shards) and migrating keys to include the new shard.
3. Removing a shard (decommission shard 1) and migrating its keys to remaining shards.

## Quick Flow

High-level migration cycle demonstrated per phase:

1. Capture current topology (enumerate authoritative store if supported).
2. Plan (generate migration plan from current -> target snapshot).
3. Execute (copy, verify, swap; progress logged periodically).
4. Validate (post-migration enumeration detects duplicates and logs shard counts).

Modulo target distribution is pedagogical; real systems weight shard targets by observed load / capacity to minimize key movement.

## Running

Requires PostgreSQL (devcontainer provides `db` host). Override with env vars: `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_USER`, `POSTGRES_PASSWORD`.

```bash
POSTGRES_HOST=db dotnet run --project samples/Shardis.Migration.EFCore.Sample/
```

## Notes

- Uses rowversion verification strategy (default) via `AddEntityFrameworkCoreMigrationSupport`.
- Seeds 10,000 `UserOrder` rows with intentional skew.
- Topology snapshots are fully materialized (OK at this scale). For >100k keys prefer a segmented / streaming planner to avoid large intermediate allocations.
- Each phase executes an independent migration plan; progress printed periodically.
- Post-migration validation enumerates the authoritative store (if supported) to ensure no duplicates and logs per-shard key counts.
- Drift detection: a simple hash of the topology (when enumeration available) is taken before planning and re-checked just before execution; if it changed the migration aborts to avoid acting on stale input.
- Optional metrics: set `SHARDIS_SAMPLE_METRICS=1` to enable a console metrics stub (`SampleConsoleMetrics`).

## What This Sample Does NOT Do (and why)

| Omitted | Reason | Where To Look Next |
|---------|--------|--------------------|
| Streaming / segmented planning | Added complexity; not needed for 10k keys | `samples/Shardis.Migration.Advanced.Sample` placeholder |
| Weighted / load-based balancing | Would obscure core flow | Advanced sample roadmap |
| Production-grade metrics & tracing | Avoid vendor lock / noise | Implement your own `IShardisMetrics` |
| Retry / abort policies | Keep executor usage minimal | Advanced sample roadmap |

## Scaling Guidance

| Key Count | Recommended Approach |
|-----------|----------------------|
| ≤ 100k | Full snapshot (this sample) |
| 100k – 5M | Segmented enumeration (batch hash + partial plan) |
| > 5M | Streaming planner + incremental verification |

## Validation Output

Each phase ends with a line like:

`[validate] phase:rebalance: 2 shards, total 10000 keys -> 0:5000,1:5000`

This ensures cardinality and uniqueness. If duplicates are found the run aborts early.
