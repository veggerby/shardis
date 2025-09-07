using Shardis.Factories;
using Shardis.Model;

using StackExchange.Redis;

namespace Shardis.Redis.Factories;

/// <summary>
/// Redis shard factory returning logical <see cref="IDatabase"/> instances per shard using pre-configured multiplexers.
/// </summary>
public sealed class RedisShardFactory : IShardFactory<IDatabase>
{
    private readonly IReadOnlyDictionary<ShardId, IConnectionMultiplexer> _muxes;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisShardFactory"/> class.
    /// </summary>
    /// <param name="muxes">Connection multiplexers keyed by shard id.</param>
    public RedisShardFactory(IReadOnlyDictionary<ShardId, IConnectionMultiplexer> muxes)
    {
        _muxes = muxes;
    }

    /// <inheritdoc />
    public ValueTask<IDatabase> CreateAsync(ShardId shard, CancellationToken ct = default)
    {
        var db = _muxes[shard].GetDatabase();
        return new ValueTask<IDatabase>(db);
    }
}