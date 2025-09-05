using Shardis.Model;

namespace Shardis.Factories;

/// <summary>
/// Simple in-memory shard map backed by a dictionary of <see cref="ShardId"/> to connection string.
/// </summary>
public sealed class InMemoryShardMap : IShardMap
{
    private readonly IReadOnlyDictionary<ShardId, string> _map;

    /// <summary>
    /// Initializes a new instance of <see cref="InMemoryShardMap"/>.
    /// </summary>
    /// <param name="map">Mapping from shard id to connection string.</param>
    public InMemoryShardMap(IReadOnlyDictionary<ShardId, string> map)
    {
        _map = map?.Count == 0 ? Array.Empty<KeyValuePair<ShardId, string>>().ToDictionary(k => k.Key, v => v.Value) : new Dictionary<ShardId, string>(map!);
    }

    /// <inheritdoc />
    public IEnumerable<ShardId> Shards => _map.Keys;

    /// <inheritdoc />
    public string GetConnectionString(ShardId shard) => _map.TryGetValue(shard, out var cs) ? cs : throw new KeyNotFoundException($"No connection string configured for shard '{shard}'.");
}