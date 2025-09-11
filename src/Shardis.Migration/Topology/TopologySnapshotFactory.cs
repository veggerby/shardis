using System.Diagnostics;

using Shardis.Diagnostics;
using Shardis.Migration.Model;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Migration.Topology;

/// <summary>
/// Factory helpers for building <see cref="TopologySnapshot{TKey}"/> instances from enumeration-capable shard map stores.
/// </summary>
public static class TopologySnapshotFactory
{
    /// <summary>
    /// Builds a <see cref="TopologySnapshot{TKey}"/> by fully materializing the assignments from an <see cref="IShardMapEnumerationStore{TKey}"/>.
    /// </summary>
    /// <param name="store">Enumeration-capable shard map store.</param>
    /// <param name="maxKeys">Hard cap to guard memory. Throws if exceeded. Default 1,000,000.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <typeparam name="TKey">Shard key type.</typeparam>
    /// <exception cref="InvalidOperationException">Thrown when the number of enumerated keys exceeds <paramref name="maxKeys"/>.</exception>
    public static async Task<TopologySnapshot<TKey>> ToSnapshotAsync<TKey>(
        this IShardMapEnumerationStore<TKey> store,
        int maxKeys = 1_000_000,
        CancellationToken cancellationToken = default)
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(store);
        if (maxKeys <= 0) throw new ArgumentOutOfRangeException(nameof(maxKeys));

        using var activity = ShardisDiagnostics.ActivitySource.StartActivity("shardis.snapshot.enumerate", ActivityKind.Internal);
        activity?.SetTag("shardis.snapshot.max_keys", maxKeys);
        var sw = Stopwatch.StartNew();

        var dict = new Dictionary<ShardKey<TKey>, ShardId>();
        long count = 0;
        await foreach (var map in store.EnumerateAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            dict[map.ShardKey] = map.ShardId; // last write wins if duplicates (should not happen)
            count++;
            if (count > maxKeys)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "max_keys_exceeded");
                throw new InvalidOperationException($"Snapshot key cap {maxKeys} exceeded (observed {count}). Configure a higher limit if intentional.");
            }
        }

        sw.Stop();
        activity?.SetTag("shardis.snapshot.key_count", count);
        activity?.SetTag("shardis.snapshot.elapsed_ms", sw.Elapsed.TotalMilliseconds);

        return new TopologySnapshot<TKey>(dict);
    }
}