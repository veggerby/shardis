using System.Diagnostics;
using System.Runtime.CompilerServices;

using Marten;
using Marten.Linq;

namespace Shardis.Query.Marten;

/// <summary>
/// Adaptive Marten materializer that adjusts page size based on observed per-item latency aiming to keep
/// batches within a target duration window. Deterministic (no randomness) and monotonic within bounds.
/// </summary>
public sealed class AdaptiveMartenMaterializer : IQueryableShardMaterializer
{
    private readonly int _minPageSize;
    private readonly int _maxPageSize;
    private readonly TimeSpan _targetBatchTime;
    private readonly double _growFactor;
    private readonly double _shrinkFactor;

    /// <summary>
    /// Create a new adaptive materializer.
    /// </summary>
    /// <param name="minPageSize">Lower bound for page size.</param>
    /// <param name="maxPageSize">Upper bound for page size.</param>
    /// <param name="targetBatchMilliseconds">Desired approximate duration per batch; drives growth/shrink decisions.</param>
    /// <param name="growFactor">Multiplier applied when batches are faster than target.</param>
    /// <param name="shrinkFactor">Multiplier applied when batches exceed target.</param>
    public AdaptiveMartenMaterializer(
        int minPageSize = 64,
        int maxPageSize = 8192,
        double targetBatchMilliseconds = 75,
        double growFactor = 1.5,
        double shrinkFactor = 0.5)
    {
        if (minPageSize <= 0) throw new ArgumentOutOfRangeException(nameof(minPageSize));
        if (maxPageSize < minPageSize) throw new ArgumentOutOfRangeException(nameof(maxPageSize));
        if (targetBatchMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(targetBatchMilliseconds));
        if (growFactor <= 1.0) throw new ArgumentOutOfRangeException(nameof(growFactor));
        if (shrinkFactor <= 0 || shrinkFactor >= 1.0) throw new ArgumentOutOfRangeException(nameof(shrinkFactor));
        _minPageSize = minPageSize;
        _maxPageSize = maxPageSize;
        _targetBatchTime = TimeSpan.FromMilliseconds(targetBatchMilliseconds);
        _growFactor = growFactor;
        _shrinkFactor = shrinkFactor;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IQueryable<T> query, [EnumeratorCancellation] CancellationToken ct) where T : notnull
    {
        if (query is not IMartenQueryable<T> marten)
        {
            // Fallback: simple static page strategy.
            var staticMaterializer = new MartenMaterializer(_minPageSize);
            await foreach (var item in staticMaterializer.ToAsyncEnumerable(query, ct).ConfigureAwait(false))
            {
                yield return item;
            }
            yield break;
        }

        var pageSize = _minPageSize;
        var page = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            var batch = await marten.Skip(page * pageSize).Take(pageSize).ToListAsync(ct).ConfigureAwait(false);
            sw.Stop();
            if (batch.Count == 0)
            {
                yield break;
            }

            foreach (var item in batch)
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }

            // Adjust page size deterministically based on elapsed vs target.
            var elapsed = sw.Elapsed;
            if (elapsed < _targetBatchTime && pageSize < _maxPageSize)
            {
                var next = (int)Math.Min(_maxPageSize, Math.Round(pageSize * _growFactor));
                pageSize = next;
            }
            else if (elapsed > _targetBatchTime && pageSize > _minPageSize)
            {
                var next = (int)Math.Max(_minPageSize, Math.Round(pageSize * _shrinkFactor));
                pageSize = next;
            }

            page++;
        }
    }
}