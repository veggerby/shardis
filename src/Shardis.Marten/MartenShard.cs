using Marten;

using Shardis.Model;
using Shardis.Querying.Linq;

namespace Shardis.Marten;

public sealed class MartenShard(ShardId shardId, IDocumentStore store) : IShard<IDocumentSession>
{
    public ShardId ShardId { get; } = shardId;
    private readonly IDocumentStore _store = store;

    public IDocumentSession CreateSession() => _store.LightweightSession();

    public IShardQueryExecutor<IDocumentSession> QueryExecutor => MartenQueryExecutor.Instance;
}