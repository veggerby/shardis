
using Shardis.Factories;
using Shardis.Model;

namespace Shardis.DependencyInjection;

/// <summary>
/// Convenience helpers for creating and disposing shard-scoped resources with async using semantics.
/// </summary>
public static class ShardFactoryUseExtensions
{
    /// <summary>
    /// Creates a shard resource, executes the asynchronous function and disposes the resource when complete returning the function result.
    /// </summary>
    /// <typeparam name="T">Resource type.</typeparam>
    /// <typeparam name="TResult">Result type.</typeparam>
    /// <param name="factory">Shard factory.</param>
    /// <param name="shard">Shard identifier.</param>
    /// <param name="action">Callback executed with the created resource returning a result.</param>
    public static async ValueTask<TResult> UseAsync<T, TResult>(this IShardFactory<T> factory, ShardId shard, Func<T, ValueTask<TResult>> action)
        where T : IAsyncDisposable
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(action);
        var resource = await factory.CreateAsync(shard, CancellationToken.None).ConfigureAwait(false);
        await using (resource.ConfigureAwait(false))
        {
            return await action(resource).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a shard resource and invokes the provided asynchronous callback disposing the resource afterwards.
    /// </summary>
    /// <typeparam name="T">Resource type.</typeparam>
    /// <param name="factory">Shard factory.</param>
    /// <param name="shard">Shard identifier.</param>
    /// <param name="action">Callback executed with the created resource.</param>
    public static async ValueTask UseAsync<T>(this IShardFactory<T> factory, ShardId shard, Func<T, ValueTask> action)
        where T : IAsyncDisposable
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(action);
        var resource = await factory.CreateAsync(shard, CancellationToken.None).ConfigureAwait(false);
        await using (resource.ConfigureAwait(false))
        {
            await action(resource).ConfigureAwait(false);
        }
    }
}