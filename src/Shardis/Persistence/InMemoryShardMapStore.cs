using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Shardis.Model;

namespace Shardis.Persistence;

/// <summary>
/// Provides an in-memory implementation of the <see cref="IShardMapStore{TKey}"/> interface.
/// </summary>
public class InMemoryShardMapStore<TKey> : IShardMapStoreAsync<TKey>, IShardMapEnumerationStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Stores the key-to-shard assignments in memory.
    /// </summary>
    private readonly ConcurrentDictionary<ShardKey<TKey>, ShardId> _assignments = new();

    /// <summary>
    /// Attempts to retrieve the shard ID for a given shard key.
    /// </summary>
    /// <param name="shardKey">The shard key to look up.</param>
    /// <param name="shardId">When this method returns, contains the shard ID associated with the key, if found.</param>
    /// <returns><c>true</c> if the shard ID was found; otherwise, <c>false</c>.</returns>
    public bool TryGetShardIdForKey(ShardKey<TKey> shardKey, out ShardId shardId) => _assignments.TryGetValue(shardKey, out shardId);

    /// <inheritdoc />
    public ValueTask<ShardId?> TryGetShardIdForKeyAsync(ShardKey<TKey> shardKey, CancellationToken cancellationToken = default)
    {
        return _assignments.TryGetValue(shardKey, out var shardId)
            ? ValueTask.FromResult<ShardId?>(shardId)
            : ValueTask.FromResult<ShardId?>(null);
    }

    /// <summary>
    /// Assigns a shard ID to a given shard key.
    /// </summary>
    /// <param name="shardKey">The shard key to assign.</param>
    /// <param name="shardId">The shard ID to assign to the key.</param>
    /// <returns>A <see cref="ShardMap{TKey}"/> representing the key-to-shard assignment.</returns>
    public ShardMap<TKey> AssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId)
    {
        _assignments[shardKey] = shardId;
        return new ShardMap<TKey>(shardKey, shardId);
    }

    /// <inheritdoc />
    public ValueTask<ShardMap<TKey>> AssignShardToKeyAsync(ShardKey<TKey> shardKey, ShardId shardId, CancellationToken cancellationToken = default)
    {
        _assignments[shardKey] = shardId;
        return ValueTask.FromResult(new ShardMap<TKey>(shardKey, shardId));
    }

    /// <inheritdoc />
    public bool TryAssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId, out ShardMap<TKey> shardMap)
    {
        var added = _assignments.TryAdd(shardKey, shardId);
        var effective = _assignments[shardKey];
        shardMap = new ShardMap<TKey>(shardKey, effective);
        return added;
    }

    /// <inheritdoc />
    public ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryAssignShardToKeyAsync(ShardKey<TKey> shardKey, ShardId shardId, CancellationToken cancellationToken = default)
    {
        var added = _assignments.TryAdd(shardKey, shardId);
        var effective = _assignments[shardKey];
        var shardMap = new ShardMap<TKey>(shardKey, effective);
        return ValueTask.FromResult((added, shardMap));
    }

    /// <inheritdoc />
    public bool TryGetOrAdd(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, out ShardMap<TKey> shardMap)
    {
        ArgumentNullException.ThrowIfNull(valueFactory, nameof(valueFactory));

        if (_assignments.TryGetValue(shardKey, out var existing))
        {
            shardMap = new ShardMap<TKey>(shardKey, existing);
            return false;
        }

        // Compute candidate id outside add for deterministic hashing cost per contender.
        var candidate = valueFactory();
        if (_assignments.TryAdd(shardKey, candidate))
        {
            shardMap = new ShardMap<TKey>(shardKey, candidate);
            return true;
        }

        // Lost race: fetch existing (must succeed)
        var winner = _assignments[shardKey];
        shardMap = new ShardMap<TKey>(shardKey, winner);
        return false;
    }

    /// <inheritdoc />
    public ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryGetOrAddAsync(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(valueFactory, nameof(valueFactory));

        if (_assignments.TryGetValue(shardKey, out var existing))
        {
            return ValueTask.FromResult((false, new ShardMap<TKey>(shardKey, existing)));
        }

        // Compute candidate id outside add for deterministic hashing cost per contender.
        var candidate = valueFactory();
        if (_assignments.TryAdd(shardKey, candidate))
        {
            return ValueTask.FromResult((true, new ShardMap<TKey>(shardKey, candidate)));
        }

        // Lost race: fetch existing (must succeed)
        var winner = _assignments[shardKey];
        return ValueTask.FromResult((false, new ShardMap<TKey>(shardKey, winner)));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ShardMap<TKey>> EnumerateAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Take a moment-in-time snapshot of keys to avoid holding collection locks during async iteration.
        // Concurrent additions after the snapshot are not reflected (acceptable for point-in-time semantics).
        var snapshot = _assignments.ToArray();
        foreach (var kvp in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ShardMap<TKey>(kvp.Key, kvp.Value);
            // No back-pressure logic needed for in-memory enumeration; if needed, introduce pacing in future.
            await Task.CompletedTask; // keep method 'async' without allocation per item.
        }
    }
}