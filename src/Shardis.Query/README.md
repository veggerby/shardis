# Shardis.Query

Core querying primitives for Shardis (internal abstractions + helpers). Not intended for direct consumption; use provider packages:

- `Shardis.Query.EFCore`
- `Shardis.Query.Marten`
- `Shardis.Query.InMemory`

Provides:

- Merge enumerators (ordered streaming, eager ordered, unordered interleave)
- LINQ MVP scaffolding (Where/Select only)
- Adaptive paging observers (provider-integrated)

Consumers should reference provider packages which depend on this internally.
