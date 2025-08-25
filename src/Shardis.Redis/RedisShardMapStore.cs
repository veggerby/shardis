using Shardis.Model;
using Shardis.Persistence;

using StackExchange.Redis;

namespace Shardis.Redis;

/// <summary>
/// Provides a Redis-backed implementation of the <see cref="IShardMapStore"/> interface.
/// </summary>
public class RedisShardMapStore<TKey> : IShardMapStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IDatabase _database;
    private const string ShardMapKeyPrefix = "shardmap:";

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisShardMapStore"/> class.
    /// </summary>
    /// <param name="connectionString">The Redis connection string.</param>
    public RedisShardMapStore(string connectionString)
    {
        var connection = ConnectionMultiplexer.Connect(connectionString);
        _database = connection.GetDatabase();
    }

    /// <inheritdoc/>
    public bool TryGetShardIdForKey(ShardKey<TKey> shardKey, out ShardId shardId)
    {
        var redisKey = ShardMapKeyPrefix + shardKey.Value;
        var shardIdValue = _database.StringGet(redisKey);

        if (shardIdValue.HasValue)
        {
            shardId = new ShardId(shardIdValue!);
            return true;
        }

        shardId = default;
        return false;
    }

    /// <inheritdoc/>
    public ShardMap<TKey> AssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId)
    {
        var redisKey = ShardMapKeyPrefix + shardKey.Value;
        _database.StringSet(redisKey, shardId.Value);
        return new ShardMap<TKey>(shardKey, shardId);
    }

    /// <inheritdoc />
    public bool TryAssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId, out ShardMap<TKey> shardMap)
    {
        var redisKey = ShardMapKeyPrefix + shardKey.Value;
        // SET key value NX for compare-and-set semantics
        var created = _database.StringSet(redisKey, shardId.Value, when: When.NotExists);
        if (!created)
        {
            // Read existing
            var existing = _database.StringGet(redisKey);
            shardMap = new ShardMap<TKey>(shardKey, new ShardId(existing!));
            return false;
        }
        shardMap = new ShardMap<TKey>(shardKey, shardId);
        return true;
    }

    /// <inheritdoc />
    public bool TryGetOrAdd(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, out ShardMap<TKey> shardMap)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        if (TryGetShardIdForKey(shardKey, out var existing))
        {
            shardMap = new ShardMap<TKey>(shardKey, existing);
            return false;
        }
        var id = valueFactory();
        // attempt NX set; if lost race, fetch existing
        var redisKey = ShardMapKeyPrefix + shardKey.Value;
        var created = _database.StringSet(redisKey, id.Value, when: When.NotExists);
        if (!created)
        {
            var current = _database.StringGet(redisKey);
            shardMap = new ShardMap<TKey>(shardKey, new ShardId(current!));
            return false;
        }
        shardMap = new ShardMap<TKey>(shardKey, id);
        return true;
    }
}