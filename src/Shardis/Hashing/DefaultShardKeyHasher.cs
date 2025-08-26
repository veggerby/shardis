namespace Shardis.Hashing;

/// <summary>
/// Provides a default selection of <see cref="IShardKeyHasher{TKey}"/> implementations for common primitive key types.
/// </summary>
/// <remarks>
/// This factory inspects <typeparamref name="TKey"/> at first access and returns a singleton hasher optimized
/// for that key type. Unsupported key types produce a <see cref="ShardisException"/>. Consumers are expected
/// to cache the returned <see cref="IShardKeyHasher{TKey}"/> (the <see cref="Instance"/> property already does this).
/// </remarks>
/// <typeparam name="TKey">The shard key value type.</typeparam>
public static class DefaultShardKeyHasher<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Gets the singleton <see cref="IShardKeyHasher{TKey}"/> for the target key type.
    /// </summary>
    /// <exception cref="ShardisException">Thrown when no built‑in hasher exists for <typeparamref name="TKey"/>.</exception>
    public static IShardKeyHasher<TKey> Instance => typeof(TKey) switch
    {
        Type t when t == typeof(string) => (IShardKeyHasher<TKey>)StringShardKeyHasher.Instance,
        Type t when t == typeof(int) => (IShardKeyHasher<TKey>)Int32ShardKeyHasher.Instance,
        Type t when t == typeof(uint) => (IShardKeyHasher<TKey>)UInt32ShardKeyHasher.Instance,
        Type t when t == typeof(long) => (IShardKeyHasher<TKey>)Int64ShardKeyHasher.Instance,
        Type t when t == typeof(Guid) => (IShardKeyHasher<TKey>)GuidShardKeyHasher.Instance,
        _ => throw new ShardisException($"No shard hasher is registered for type {typeof(TKey)}."),
    };
}