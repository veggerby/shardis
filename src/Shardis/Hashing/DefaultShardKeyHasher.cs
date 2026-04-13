namespace Shardis.Hashing;

/// <summary>
/// Provides a default selection of <see cref="IShardKeyHasher{TKey}"/> implementations for common primitive key types.
/// </summary>
/// <remarks>
/// This factory inspects <typeparamref name="TKey"/> at first access. For supported types the resolved hasher is cached
/// in a static field via <see cref="System.Threading.Interlocked.CompareExchange{T}"/>; subsequent calls pay only the
/// cost of a null check. For unsupported types a <see cref="ShardisException"/> is thrown on every call.
/// </remarks>
/// <typeparam name="TKey">The shard key value type.</typeparam>
public static class DefaultShardKeyHasher<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private static IShardKeyHasher<TKey>? _cached;

    /// <summary>
    /// Gets the singleton <see cref="IShardKeyHasher{TKey}"/> for the target key type.
    /// </summary>
    /// <exception cref="ShardisException">Thrown when no built‑in hasher exists for <typeparamref name="TKey"/>.</exception>
    public static IShardKeyHasher<TKey> Instance
    {
        get
        {
            if (_cached is not null)
            {
                return _cached;
            }

            // Evaluate the type switch once; only one value wins the CAS for concurrent first-access.
            var resolved = Resolve();
            return Interlocked.CompareExchange(ref _cached, resolved, null) ?? resolved;
        }
    }

    private static IShardKeyHasher<TKey> Resolve() => typeof(TKey) switch
    {
        Type t when t == typeof(string) => (IShardKeyHasher<TKey>)StringShardKeyHasher.Instance,
        Type t when t == typeof(int) => (IShardKeyHasher<TKey>)Int32ShardKeyHasher.Instance,
        Type t when t == typeof(uint) => (IShardKeyHasher<TKey>)UInt32ShardKeyHasher.Instance,
        Type t when t == typeof(long) => (IShardKeyHasher<TKey>)Int64ShardKeyHasher.Instance,
        Type t when t == typeof(Guid) => (IShardKeyHasher<TKey>)GuidShardKeyHasher.Instance,
        _ => throw new ShardisException($"No shard hasher is registered for type {typeof(TKey)}."),
    };
}