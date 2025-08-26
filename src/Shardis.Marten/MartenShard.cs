using Marten;

using Shardis.Model;
using Shardis.Querying.Linq;

namespace Shardis.Marten;

/// <summary>
/// Concrete Marten-backed shard providing lightweight sessions per operation.
/// </summary>
/// <param name="shardId">Shard identifier.</param>
/// <param name="store">Underlying Marten document store.</param>
public sealed class MartenShard(ShardId shardId, IDocumentStore store) : IShard<IDocumentSession>
{
    /// <inheritdoc />
    public ShardId ShardId { get; } = shardId;
    private readonly IDocumentStore _store = store;

    /// <inheritdoc />
    public IDocumentSession CreateSession() => _store.LightweightSession();

    /// <inheritdoc />
    public IShardQueryExecutor<IDocumentSession> QueryExecutor => MartenQueryExecutor.Instance;
}