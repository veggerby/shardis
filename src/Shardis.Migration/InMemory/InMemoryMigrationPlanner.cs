namespace Shardis.Migration.InMemory;

using System.Security.Cryptography;
using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;
using Shardis.Model;

/// <summary>
/// In-memory planner computing key moves by diffing two topology snapshots.
/// Ordering: Source, Target, KeyHash (stable hash of key string representation via SHA256 first 8 bytes).
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
        // Deterministic, not for crypto security (already using SHA256 truncated for uniformity).
        var str = key.Value.ToString() ?? string.Empty;
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        var hash = SHA256.HashData(bytes);
        return BitConverter.ToUInt64(hash, 0);
    }
}