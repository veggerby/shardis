namespace Shardis.Hashing;

public static class DefaultShardKeyHasher<TKey>
    where TKey : notnull, IEquatable<TKey>
{
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
