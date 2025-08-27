namespace Shardis.Migration.InMemory;


using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;
using Shardis.Model;

/// <summary>
/// In-memory planner computing key moves by diffing two topology snapshots.
/// Ordering: Source, Target, KeyHash (stable hash of key string representation via FNV-1a 64-bit).
/// </summary>
internal sealed class InMemoryMigrationPlanner<TKey> : IShardMigrationPlanner<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    public Task<MigrationPlan<TKey>> CreatePlanAsync(TopologySnapshot<TKey> from, TopologySnapshot<TKey> to, CancellationToken ct)
    {
        // Compute moves for keys whose assigned shard changes in target snapshot.
        var moves = new List<KeyMove<TKey>>();
        foreach (var kvp in from.Assignments)
        {
            ct.ThrowIfCancellationRequested();
            if (to.Assignments.TryGetValue(kvp.Key, out var newShard) && newShard != kvp.Value)
            {
                moves.Add(new KeyMove<TKey>(kvp.Key, kvp.Value, newShard));
            }
        }

        // Include newly introduced keys that did not exist (treat source as same as target? Skip: no move needed).

        var ordered = moves
            .OrderBy(m => m.Source.Value, StringComparer.Ordinal)
            .ThenBy(m => m.Target.Value, StringComparer.Ordinal)
            .ThenBy(m => StableKeyHash(m.Key))
            .ToArray();

        var plan = new MigrationPlan<TKey>(Guid.NewGuid(), DateTimeOffset.UtcNow, ordered);
        return Task.FromResult(plan);
    }

    private static ulong StableKeyHash(ShardKey<TKey> key)
    {
        // Deterministic, non-cryptographic FNV-1a 64-bit for ordering (uniform enough, much cheaper than SHA256).
        var str = key.Value?.ToString() ?? string.Empty;
        ReadOnlySpan<char> chars = str.AsSpan();
        // Worst-case UTF8 expansion is 4 bytes per char; typical ASCII keys should be common.
        // Use stackalloc for small keys to avoid allocations.
        Span<byte> utf8 = chars.Length <= 128 ? stackalloc byte[chars.Length * 4] : new byte[System.Text.Encoding.UTF8.GetMaxByteCount(chars.Length)];
        var count = System.Text.Encoding.UTF8.GetBytes(chars, utf8);
        const ulong offset = 14695981039346656037UL; // FNV offset basis
        const ulong prime = 1099511628211UL;          // FNV prime
        ulong hash = offset;
        for (int i = 0; i < count; i++)
        {
            hash ^= utf8[i];
            hash *= prime;
        }
        return hash;
    }
}