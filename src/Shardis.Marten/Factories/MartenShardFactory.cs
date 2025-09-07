using Marten;

using Shardis.Factories;
using Shardis.Model;

namespace Shardis.Marten.Factories;

/// <summary>
/// Marten shard factory creating lightweight sessions for each shard from pre-configured <see cref="IDocumentStore"/> instances.
/// </summary>
public sealed class MartenShardFactory : IShardFactory<IDocumentSession>
{
    private readonly IReadOnlyDictionary<ShardId, IDocumentStore> _stores;

    /// <summary>
    /// Initializes a new instance of the <see cref="MartenShardFactory"/> class.
    /// </summary>
    /// <param name="stores">Store instances keyed by shard id.</param>
    public MartenShardFactory(IReadOnlyDictionary<ShardId, IDocumentStore> stores)
    {
        _stores = stores;
    }

    /// <inheritdoc />
    public ValueTask<IDocumentSession> CreateAsync(ShardId shard, CancellationToken ct = default)
    {
        var session = _stores[shard].LightweightSession();
        return new ValueTask<IDocumentSession>(session);
    }
}