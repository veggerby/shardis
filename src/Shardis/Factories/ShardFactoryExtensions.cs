using Shardis.Model;

namespace Shardis.Factories;

/// <summary>
/// Extension helpers for <see cref="IShardFactory{T}"/>.
/// </summary>
public static class ShardFactoryExtensions
{
    /// <summary>
    /// Creates a shard-scoped resource and executes the provided action ensuring proper disposal (sync or async) afterwards.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="factory">Factory used to create the resource.</param>
    /// <param name="shard">Shard identifier.</param>
    /// <param name="action">User action operating on the resource.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async ValueTask UseAsync<T>(this IShardFactory<T> factory, ShardId shard, Func<T, CancellationToken, ValueTask> action, CancellationToken ct = default)
    {
        var resource = await factory.CreateAsync(shard, ct).ConfigureAwait(false);

        try
        {
            await action(resource, ct).ConfigureAwait(false);
        }
        finally
        {
            switch (resource)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }
}