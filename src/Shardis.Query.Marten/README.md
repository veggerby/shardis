# Shardis.Query.Marten

Marten query execution components for Shardis fluent query MVP.

## Features

- Streams documents per shard using Marten sessions
- Supports unordered and ordered streaming merge helpers
- Integrates adaptive paging (Marten paging strategy)
- Metrics via query observers

Use alongside `Shardis.Marten` for shard/session management.
