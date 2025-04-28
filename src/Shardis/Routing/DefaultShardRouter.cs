using System.Security.Cryptography;
using System.Text;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Routing;

/// <summary>
/// Provides a default shard routing implementation using a simple hash-based strategy.
/// </summary>
public class DefaultShardRouter<TSession> : IShardRouter<TSession>
{
    /// <summary>
    /// The list of available shards.
    /// </summary>
    private readonly List<IShard<TSession>> _availableShards;

    /// <summary>
    /// The shard map store for managing shard assignments.
    /// </summary>
    private readonly IShardMapStore _shardMapStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShardRouter{TSession}"/> class.
    /// </summary>
    /// <param name="shardMapStore">The shard map store for managing shard assignments.</param>
    /// <param name="availableShards">The collection of available shards.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shardMapStore"/> or <paramref name="availableShards"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="availableShards"/> is empty.</exception>
    public DefaultShardRouter(IShardMapStore shardMapStore, IEnumerable<IShard<TSession>> availableShards)
    {
        _shardMapStore = shardMapStore ?? throw new ArgumentNullException(nameof(shardMapStore));
        _availableShards = availableShards?.ToList() ?? throw new ArgumentNullException(nameof(availableShards));

        if (!_availableShards.Any())
        {
            throw new InvalidOperationException("At least one shard must be available.");
        }
    }

    /// <summary>
    /// Routes a given shard key to the appropriate shard using a hash-based strategy.
    /// </summary>
    /// <param name="shardKey">The shard key representing an aggregate instance.</param>
    /// <returns>The shard that should handle the given key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shardKey"/> is null.</exception>
    public IShard<TSession> RouteToShard(ShardKey shardKey)
    {
        if (shardKey.Value == null) throw new ArgumentNullException(nameof(shardKey));

        // First check if the key has already been assigned
        if (_shardMapStore.TryGetShardIdForKey(shardKey, out var assignedShardId))
        {
            var existingShard = _availableShards.FirstOrDefault(s => s.ShardId == assignedShardId);
            if (existingShard != null)
            {
                return existingShard;
            }
        }

        // If no assignment exists, hash it and assign
        var shardIndex = CalculateShardIndex(shardKey.Value, _availableShards.Count);
        var selectedShard = _availableShards[shardIndex];

        // Store the assignment
        _shardMapStore.AssignShardToKey(shardKey, selectedShard.ShardId);

        return selectedShard;
    }

    /// <summary>
    /// Calculates the index of the shard for a given key value.
    /// </summary>
    /// <param name="keyValue">The key value to hash.</param>
    /// <param name="shardCount">The total number of available shards.</param>
    /// <returns>The index of the shard.</returns>
    private static int CalculateShardIndex(string keyValue, int shardCount)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyValue));

        // Take the first 4 bytes and turn into an int
        var hashInt = BitConverter.ToInt32(hashBytes, 0);

        // Ensure positive integer
        hashInt = Math.Abs(hashInt);

        return hashInt % shardCount;
    }
}
