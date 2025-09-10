namespace Shardis.Migration.Planning;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;
using Shardis.Model;
using Shardis.Persistence;

/// <summary>
/// Migration planner that streams the authoritative <c>from</c> topology directly from an
/// <see cref="IShardMapEnumerationStore{TKey}"/> in fixed-size segments instead of requiring the caller to materialize
/// the complete source <see cref="TopologySnapshot{TKey}"/> in memory up front.
/// </summary>
/// <remarks>
/// Usage: register an <see cref="IShardMapEnumerationStore{TKey}"/> implementation and call
/// <c>services.UseSegmentedEnumerationPlanner(segmentSize)</c> after <c>AddShardisMigration</c>.
///
/// Characteristics:
///  * Single full enumeration pass over the authoritative mapping.
///  * Memory complexity: O(segmentSize + |moves|) (the target snapshot must still be materialized – optimising target construction is out of scope).
///  * Move ordering matches <see cref="InMemory.InMemoryMigrationPlanner{TKey}"/> (Source, Target, StableKeyHash) to preserve determinism.
///  * Suitable for very large key counts (hundreds of thousands to millions) where holding the entire source snapshot doubles memory pressure.
///  * Cancellation is honoured between segments and per item.
///  * Segment size trades off enumeration batching overhead vs. transient working set; values between 5k – 50k are typically reasonable.
/// </remarks>
public sealed class SegmentedEnumerationMigrationPlanner<TKey> : IShardMigrationPlanner<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IShardMapEnumerationStore<TKey> _store;
    private readonly int _segmentSize;

    /// <summary>
    /// Creates a new segmented enumeration planner.
    /// </summary>
    /// <param name="store">Enumeration-capable shard map store representing the authoritative current assignments.</param>
    /// <param name="segmentSize">Maximum number of mappings buffered before diffing against the target snapshot.</param>
    public SegmentedEnumerationMigrationPlanner(IShardMapEnumerationStore<TKey> store, int segmentSize = 10_000)
    {
        if (segmentSize <= 0) throw new ArgumentOutOfRangeException(nameof(segmentSize));
        _store = store;
        _segmentSize = segmentSize;
    }

    /// <inheritdoc />
    public async Task<MigrationPlan<TKey>> CreatePlanAsync(TopologySnapshot<TKey> from, TopologySnapshot<TKey> to, CancellationToken ct)
    {
        // Ignore provided 'from' snapshot (caller may pass lightweight placeholder) and enumerate authoritative store instead.
        var moves = new List<KeyMove<TKey>>();
        var batch = new List<ShardMap<TKey>>(_segmentSize);

        await foreach (var map in _store.EnumerateAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            batch.Add(map);
            if (batch.Count == _segmentSize)
            {
                Diff(batch, to, moves, ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            Diff(batch, to, moves, ct);
        }

        var ordered = moves
            .OrderBy(m => m.Source.Value, StringComparer.Ordinal)
            .ThenBy(m => m.Target.Value, StringComparer.Ordinal)
            .ThenBy(m => StableKeyHash(m.Key))
            .ToArray();

        return new MigrationPlan<TKey>(Guid.NewGuid(), DateTimeOffset.UtcNow, ordered);
    }

    private static void Diff(List<ShardMap<TKey>> segment, TopologySnapshot<TKey> to, List<KeyMove<TKey>> moves, CancellationToken ct)
    {
        foreach (var map in segment)
        {
            ct.ThrowIfCancellationRequested();
            if (to.Assignments.TryGetValue(map.ShardKey, out var newShard) && newShard != map.ShardId)
            {
                moves.Add(new KeyMove<TKey>(map.ShardKey, map.ShardId, newShard));
            }
        }
    }

    private static ulong StableKeyHash(ShardKey<TKey> key)
    {
        var str = key.Value?.ToString() ?? string.Empty;
        ReadOnlySpan<char> chars = str.AsSpan();
        Span<byte> utf8 = chars.Length <= 128 ? stackalloc byte[chars.Length * 4] : new byte[System.Text.Encoding.UTF8.GetMaxByteCount(chars.Length)];
        var count = System.Text.Encoding.UTF8.GetBytes(chars, utf8);
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        for (int i = 0; i < count; i++)
        {
            hash ^= utf8[i];
            hash *= prime;
        }
        return hash;
    }
}