using System.Threading.Channels;

namespace Shardis.Query.Internals;

/// <summary>
/// Lightweight unordered interleaving merge for <see cref="IAsyncEnumerable{Object}"/> sources.
/// Mirrors semantics of broadcaster unordered merge (arrival order, early yield, cancellation aware).
/// </summary>
/// <summary>Public unordered interleaving merge helper for query executors (arrival order, non-deterministic across shards).</summary>
public static class UnorderedMerge
{
    /// <summary>Create an unordered merged async stream from shard item streams (non-deterministic interleaving).</summary>
    /// <param name="sources">Shard item streams.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="channelCapacity">Optional bounded channel capacity; null for unbounded.</param>
    public static IAsyncEnumerable<object> Merge(IEnumerable<IAsyncEnumerable<object>> sources, CancellationToken ct = default, int? channelCapacity = null)
    {
        return Execute();

        async IAsyncEnumerable<object> Execute()
        {
            var srcArray = sources.ToArray();
            if (srcArray.Length == 0) yield break;

            var channel = channelCapacity.HasValue
                ? Channel.CreateBounded<object>(new BoundedChannelOptions(channelCapacity.Value)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                })
                : Channel.CreateUnbounded<object>();

            var tasks = srcArray.Select(src => Task.Run(async () =>
            {
                try
                {
                    await foreach (var item in src.WithCancellation(ct).ConfigureAwait(false))
                    {
                        await channel.Writer.WriteAsync(item, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* swallow */ }
            }, ct)).ToArray();

            _ = Task.WhenAll(tasks).ContinueWith(t =>
            {
                channel.Writer.TryComplete(t.Exception);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            await foreach (var item in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return item;
            }
        }
    }
}