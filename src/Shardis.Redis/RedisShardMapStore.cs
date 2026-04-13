using Shardis.Model;
using Shardis.Persistence;

using StackExchange.Redis;

namespace Shardis.Redis;

/// <summary>
/// Provides a Redis-backed implementation of <see cref="IShardMapStoreAsync{TKey}"/> and <see cref="IShardMapStore{TKey}"/>.
/// </summary>
/// <remarks>
/// Prefer the <see cref="RedisShardMapStore{TKey}(IConnectionMultiplexer)"/> constructor and inject a shared
/// <see cref="IConnectionMultiplexer"/> singleton. Creating a multiplexer per store instance is wasteful.
/// The synchronous <c>IShardMapStore&lt;TKey&gt;</c> methods perform blocking network I/O on the calling thread;
/// always use the async variants (<c>IShardMapStoreAsync&lt;TKey&gt;</c>) in production code.
/// </remarks>
public class RedisShardMapStore<TKey> : IShardMapStoreAsync<TKey>, IShardMapStore<TKey>, IDisposable
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IDatabase _database;
    private readonly ConnectionMultiplexer? _ownedMultiplexer;
    private bool _disposed;
    private const string ShardMapKeyPrefix = "shardmap:";

    /// <summary>
    /// Initializes a new instance accepting an externally managed <see cref="IConnectionMultiplexer"/>.
    /// The multiplexer is NOT disposed when this store is disposed. Prefer this overload in production.
    /// </summary>
    /// <param name="multiplexer">Shared Redis connection multiplexer.</param>
    public RedisShardMapStore(IConnectionMultiplexer multiplexer)
    {
        ArgumentNullException.ThrowIfNull(multiplexer, nameof(multiplexer));
        _database = multiplexer.GetDatabase();
    }

    /// <summary>
    /// Initializes a new instance creating an owned <see cref="ConnectionMultiplexer"/> from the provided
    /// connection string. The multiplexer is disposed together with this store.
    /// </summary>
    /// <param name="connectionString">The Redis connection string.</param>
    public RedisShardMapStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));
        _ownedMultiplexer = ConnectionMultiplexer.Connect(connectionString);
        _database = _ownedMultiplexer.GetDatabase();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Note: StackExchange.Redis does not support CancellationToken in its async operations.
    /// The cancellationToken parameter is accepted for interface compatibility but not used.
    /// </remarks>
    public async ValueTask<ShardId?> TryGetShardIdForKeyAsync(ShardKey<TKey> shardKey, CancellationToken cancellationToken = default)
    {
        var redisKey = ShardMapKeyPrefix + shardKey.Value;
        var shardIdValue = await _database.StringGetAsync(redisKey);

        if (shardIdValue.HasValue)
        {
            return new ShardId(shardIdValue!);
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<ShardMap<TKey>> AssignShardToKeyAsync(ShardKey<TKey> shardKey, ShardId shardId, CancellationToken cancellationToken = default)
    {
        var redisKey = ShardMapKeyPrefix + shardKey.Value;
        await _database.StringSetAsync(redisKey, shardId.Value);
        return new ShardMap<TKey>(shardKey, shardId);
    }

    /// <inheritdoc />
    public async ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryAssignShardToKeyAsync(ShardKey<TKey> shardKey, ShardId shardId, CancellationToken cancellationToken = default)
    {
        var redisKey = ShardMapKeyPrefix + shardKey.Value;
        // SET key value NX for compare-and-set semantics
        var created = await _database.StringSetAsync(redisKey, shardId.Value, when: When.NotExists);

        if (!created)
        {
            // Read existing
            var existing = await _database.StringGetAsync(redisKey);
            return (false, new ShardMap<TKey>(shardKey, new ShardId(existing!)));
        }

        return (true, new ShardMap<TKey>(shardKey, shardId));
    }

    /// <inheritdoc />
    public async ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryGetOrAddAsync(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);

        var existing = await TryGetShardIdForKeyAsync(shardKey, cancellationToken);
        if (existing is not null)
        {
            return (false, new ShardMap<TKey>(shardKey, existing.Value));
        }

        var id = valueFactory();

        // attempt NX set; if lost race, fetch existing
        var redisKey = ShardMapKeyPrefix + shardKey.Value;
        var created = await _database.StringSetAsync(redisKey, id.Value, when: When.NotExists);

        if (!created)
        {
            var current = await _database.StringGetAsync(redisKey);
            return (false, new ShardMap<TKey>(shardKey, new ShardId(current!)));
        }

        return (true, new ShardMap<TKey>(shardKey, id));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// WARNING: This synchronous method blocks the calling thread waiting for a Redis network round-trip.
    /// Use <see cref="TryGetShardIdForKeyAsync"/> instead.
    /// </remarks>
    [Obsolete("Blocking synchronous Redis I/O starves the thread pool. Use TryGetShardIdForKeyAsync instead.")]
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
    /// <remarks>
    /// WARNING: This synchronous method blocks the calling thread waiting for a Redis network round-trip.
    /// Use <see cref="AssignShardToKeyAsync"/> instead.
    /// </remarks>
    [Obsolete("Blocking synchronous Redis I/O starves the thread pool. Use AssignShardToKeyAsync instead.")]
    public ShardMap<TKey> AssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId)
    {
        var redisKey = ShardMapKeyPrefix + shardKey.Value;
        _database.StringSet(redisKey, shardId.Value);
        return new ShardMap<TKey>(shardKey, shardId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// WARNING: This synchronous method blocks the calling thread waiting for a Redis network round-trip.
    /// Use <see cref="TryAssignShardToKeyAsync"/> instead.
    /// </remarks>
    [Obsolete("Blocking synchronous Redis I/O starves the thread pool. Use TryAssignShardToKeyAsync instead.")]
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
    /// <remarks>
    /// WARNING: This synchronous method blocks the calling thread waiting for Redis network round-trips.
    /// Use <see cref="TryGetOrAddAsync(ShardKey{TKey}, Func{ShardId}, CancellationToken)"/> instead.
    /// </remarks>
    [Obsolete("Blocking synchronous Redis I/O starves the thread pool. Use TryGetOrAddAsync instead.")]
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

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ownedMultiplexer?.Dispose();
    }
}
