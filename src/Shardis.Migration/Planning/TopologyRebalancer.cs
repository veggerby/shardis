using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Planning;

/// <summary>
/// Helper utilities for deriving a target <see cref="TopologySnapshot{TKey}"/> from an existing snapshot
/// using a deterministic assignment function (e.g. derived from a router / new shard set).
/// </summary>
/// <remarks>
/// This does not perform side effects against a shard map store; it is a pure transformation over an existing
/// in-memory snapshot. Callers are expected to supply a deterministic assignment delegate when using the
/// Rebalance method variant.
/// </remarks>
public static class TopologyRebalancer
{
    /// <summary>
    /// Recomputes assignments for every key in <paramref name="from"/> using the supplied <paramref name="assignment"/>
    /// delegate, returning a new snapshot representing the prospective target topology.
    /// </summary>
    /// <typeparam name="TKey">Underlying key type.</typeparam>
    /// <param name="from">Source snapshot.</param>
    /// <param name="assignment">Deterministic function mapping a shard key to its target shard id.</param>
    /// <param name="onlyChanges">If true, the resulting snapshot only contains keys whose assignment changes.</param>
    /// <returns>New topology snapshot.</returns>
    /// <exception cref="ArgumentNullException">Thrown if arguments are null.</exception>
    public static TopologySnapshot<TKey> Rebalance<TKey>(
        TopologySnapshot<TKey> from,
        Func<ShardKey<TKey>, ShardId> assignment,
        bool onlyChanges = false)
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(from, nameof(from));
        ArgumentNullException.ThrowIfNull(assignment, nameof(assignment));

        if (from.Assignments.Count == 0)
        {
            return new TopologySnapshot<TKey>(Array.Empty<KeyValuePair<ShardKey<TKey>, ShardId>>().ToDictionary(k => k.Key, v => v.Value));
        }

        var result = new Dictionary<ShardKey<TKey>, ShardId>(onlyChanges ? Math.Min(16, from.Assignments.Count) : from.Assignments.Count);
        foreach (var kv in from.Assignments)
        {
            var target = assignment(kv.Key);
            if (onlyChanges)
            {
                if (!target.Equals(kv.Value))
                {
                    result[kv.Key] = target;
                }
            }
            else
            {
                result[kv.Key] = target;
            }
        }

        return new TopologySnapshot<TKey>(result);
    }

    /// <summary>
    /// Rebalances using a supplied shard list and a stable hashing function (e.g. consistent hash ring) without requiring
    /// the caller to manually build the assignment delegate.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <param name="from">Existing snapshot.</param>
    /// <param name="shards">Target ordered shard ids (ring already expanded / virtual nodes applied externally if desired).</param>
    /// <param name="hash">Stable 64-bit hash function over the shard key value.</param>
    /// <param name="onlyChanges">Emit only keys whose assignment changes.</param>
    /// <returns>Rebalanced snapshot.</returns>
    public static TopologySnapshot<TKey> RebalanceWithHash<TKey>(
        TopologySnapshot<TKey> from,
        IReadOnlyList<ShardId> shards,
        Func<ShardKey<TKey>, ulong> hash,
        bool onlyChanges = false)
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(shards, nameof(shards));
        ArgumentNullException.ThrowIfNull(hash, nameof(hash));

        if (shards.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shards), "At least one shard required.");
        }

        return Rebalance(from, k => shards[(int)(hash(k) % (ulong)shards.Count)], onlyChanges);
    }
}