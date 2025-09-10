namespace Shardis.Migration.Topology;

using Shardis.Model;
using Shardis.Persistence;

/// <summary>
/// Utility for validating an authoritative topology enumeration. Detects duplicate keys and returns basic statistics.
/// </summary>
public static class TopologyValidator
{
    /// <summary>
    /// Validates that no duplicate keys exist in the provided enumeration store.
    /// Throws <see cref="InvalidOperationException"/> on duplicate. Returns per-shard counts.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <param name="store">Enumeration-capable shard map store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of total key count and per-shard counts.</returns>
    public static async Task<(int Total, IReadOnlyDictionary<ShardId, int> Counts)> ValidateAsync<TKey>(
        IShardMapEnumerationStore<TKey> store,
        CancellationToken cancellationToken = default)
        where TKey : notnull, IEquatable<TKey>
    {
        var seen = new HashSet<ShardKey<TKey>>();
        var counts = new Dictionary<ShardId, int>();

        await foreach (var map in store.EnumerateAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(map.ShardKey))
            {
                throw new InvalidOperationException($"Duplicate key detected: {map.ShardKey}");
            }
            counts[map.ShardId] = counts.GetValueOrDefault(map.ShardId) + 1;
        }

        return (seen.Count, counts);
    }
}
