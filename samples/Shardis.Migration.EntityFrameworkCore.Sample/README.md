# Shardis.Migration.EFCore.Sample

Entity Framework Core migration sample demonstrating:

1. Rebalancing a skewed key distribution (90/10 across 2 shards) to an even distribution.
2. Adding a new shard (expanding from 2 to 3 shards) and migrating keys to include the new shard.
3. Removing a shard (decommission shard 1) and migrating its keys to remaining shards.

## Running

Requires PostgreSQL (devcontainer provides `db` host). Override with env vars: `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_USER`, `POSTGRES_PASSWORD`.

```bash
POSTGRES_HOST=db dotnet run --project samples/Shardis.Migration.EFCore.Sample/
```

## Notes

- Uses rowversion verification strategy (default) via `AddEntityFrameworkCoreMigrationSupport`.
- Seeds 10,000 `UserOrder` rows with intentional skew.
- Topology snapshots are constructed in-memory for clarity; production should derive from authoritative shard map.
- Each phase executes an independent migration plan; progress printed periodically.
