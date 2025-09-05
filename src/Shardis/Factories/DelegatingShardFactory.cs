using Shardis.Model;

namespace Shardis.Factories;

/// <summary>
/// Lightweight adapter turning a delegate into an <see cref="IShardFactory{T}"/>.
/// </summary>
/// <typeparam name="T">Resource type.</typeparam>
public sealed class DelegatingShardFactory<T>(Func<ShardId, CancellationToken, ValueTask<T>> asyncFactory) : IShardFactory<T>
{
    private readonly Func<ShardId, CancellationToken, ValueTask<T>> _asyncFactory = asyncFactory;

    /// <inheritdoc />
    public ValueTask<T> CreateAsync(ShardId shard, CancellationToken ct = default) => _asyncFactory(shard, ct);
}