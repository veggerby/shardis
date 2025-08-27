using System.Runtime.CompilerServices;

using Marten;
using Marten.Linq;

namespace Shardis.Query.Marten;

/// <summary>
/// Abstraction for turning a provider-specific <see cref="IQueryable{T}"/> into an asynchronous streaming sequence
/// without materializing the full result set in memory.
/// </summary>
public interface IQueryableShardMaterializer
{
    /// <summary>
    /// Convert the given query into an asynchronous stream honoring the cancellation token.
    /// Implementations should prefer provider-native async streaming if available, otherwise fall back to safe pagination.
    /// </summary>
    IAsyncEnumerable<T> ToAsyncEnumerable<T>(IQueryable<T> query, CancellationToken ct) where T : notnull;
}

/// <summary>Default Marten materializer: prefers native async streaming; otherwise pages.</summary>
/// <summary>
/// Default Marten implementation using native query execution (list paging) to provide backpressure-friendly streaming.
/// </summary>
public sealed class MartenMaterializer : IQueryableShardMaterializer
{
    private readonly int _pageSize;

    /// <summary>Create a materializer with a given page size (used for paged streaming when native streaming not available).</summary>
    public MartenMaterializer(int pageSize = 512)
    {
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        _pageSize = pageSize;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IQueryable<T> query, [EnumeratorCancellation] CancellationToken ct) where T : notnull
    {
        // If we have a Marten queryable, page over results yielding each item eagerly per page to simulate streaming.
        if (query is IMartenQueryable<T> mq)
        {
            var page = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var batch = await mq.Skip(page * _pageSize).Take(_pageSize).ToListAsync(ct).ConfigureAwait(false);
                if (batch.Count == 0) yield break;
                foreach (var item in batch)
                {
                    ct.ThrowIfCancellationRequested();
                    yield return item;
                    // Yield control to preserve responsiveness under mixed shard workloads.
                    await Task.Yield();
                }
                page++;
            }
        }

        // Fallback for non-Marten IQueryable (unlikely in production path but keeps contract robust) using same paging strategy.
        {
            var page = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var batch = await query.Skip(page * _pageSize).Take(_pageSize).ToListAsync(ct).ConfigureAwait(false);
                if (batch.Count == 0) yield break;
                foreach (var item in batch)
                {
                    ct.ThrowIfCancellationRequested();
                    yield return item;
                    await Task.Yield();
                }
                page++;
            }
        }
    }
}