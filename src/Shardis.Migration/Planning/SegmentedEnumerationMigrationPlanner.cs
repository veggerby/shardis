namespace Shardis.Migration.Planning;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;
using Shardis.Model;
using Shardis.Persistence;

/// <summary>
/// Migration planner that streams the <c>from</c> topology from an <see cref="IShardMapEnumerationStore{TKey}"/> in sized segments
/// instead of materializing the full snapshot in memory. Suitable for large key counts where the target snapshot can be precomputed or cheaply constructed.
/// </summary>
/// <remarks>
/// This implementation enumerates the authoritative mapping once and compares each key to the provided target snapshot.
/// Memory usage is O(segmentSize + |moves|). Target snapshot is still fully materialized (optimizing target derivation is outside this scope).
/// Ordering mirrors <see cref="InMemory.InMemoryMigrationPlanner{TKey}"/>: Source, Target, StableKeyHash.
/// </remarks>
internal sealed class SegmentedEnumerationMigrationPlanner<TKey> : IShardMigrationPlanner<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IShardMapEnumerationStore<TKey> _store;
    private readonly int _segmentSize;

    public SegmentedEnumerationMigrationPlanner(IShardMapEnumerationStore<TKey> store, int segmentSize = 10_000)
    {
        if (segmentSize <= 0) throw new ArgumentOutOfRangeException(nameof(segmentSize));
        _store = store;
        _segmentSize = segmentSize;
    }

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