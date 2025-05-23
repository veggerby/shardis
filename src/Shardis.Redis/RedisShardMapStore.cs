﻿using StackExchange.Redis;
using Shardis.Model;
using Shardis.Persistence;

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
}
