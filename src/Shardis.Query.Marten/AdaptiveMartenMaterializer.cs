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
    private readonly Diagnostics.IAdaptivePagingObserver _observer;
    // Telemetry tracking: decision history (per shard) for oscillation detection & final stats
    private readonly Dictionary<int, Queue<(DateTime ts, int size)>> _history = new();
    private readonly TimeSpan _oscWindow = TimeSpan.FromSeconds(5);
    private readonly int _oscThreshold = 6; // decisions within window to signal oscillation
    private readonly Dictionary<int, (int lastSize, int decisions)> _final = new();

    /// <summary>
    /// Create a new adaptive materializer.
    /// </summary>
    /// <param name="minPageSize">Lower bound for page size.</param>
    /// <param name="maxPageSize">Upper bound for page size.</param>
    /// <param name="targetBatchMilliseconds">Desired approximate duration per batch; drives growth/shrink decisions.</param>
    /// <param name="growFactor">Multiplier applied when batches are faster than target.</param>
    /// <param name="shrinkFactor">Multiplier applied when batches exceed target.</param>
    /// <param name="observer">Optional observer receiving page size decision events.</param>
    public AdaptiveMartenMaterializer(
        int minPageSize = 64,
        int maxPageSize = 8192,
        double targetBatchMilliseconds = 75,
        double growFactor = 1.5,
        double shrinkFactor = 0.5,
        Diagnostics.IAdaptivePagingObserver? observer = null)
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
        _observer = observer ?? Shardis.Query.Diagnostics.NoopAdaptivePagingObserver.Instance;
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
        try
        {
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
                int prev = pageSize;
                int nextCandidate = pageSize;
                if (elapsed < _targetBatchTime && pageSize < _maxPageSize)
                {
                    nextCandidate = (int)Math.Min(_maxPageSize, Math.Round(pageSize * _growFactor));
                }
                else if (elapsed > _targetBatchTime && pageSize > _minPageSize)
                {
                    nextCandidate = (int)Math.Max(_minPageSize, Math.Round(pageSize * _shrinkFactor));
                }
                if (nextCandidate != pageSize)
                {
                    _observer.OnPageDecision(0, prev, nextCandidate, elapsed);
                    RecordDecision(0, nextCandidate);
                    pageSize = nextCandidate;
                }
                page++;
            }
        }
        finally
        {
            // Emit final page size summary
            foreach (var kv in _final)
            {
                _observer.OnFinalPageSize(kv.Key, kv.Value.lastSize, kv.Value.decisions);
            }
        }
    }

    private void RecordDecision(int shardId, int newSize)
    {
        var now = DateTime.UtcNow;
        if (!_history.TryGetValue(shardId, out var q))
        {
            q = new Queue<(DateTime ts, int size)>();
            _history[shardId] = q;
        }
        q.Enqueue((now, newSize));
        while (q.Count > 0 && now - q.Peek().ts > _oscWindow)
        {
            q.Dequeue();
        }
        if (q.Count >= _oscThreshold)
        {
            _observer.OnOscillationDetected(shardId, q.Count, _oscWindow);
        }
        if (_final.TryGetValue(shardId, out var tuple))
        {
            _final[shardId] = (newSize, tuple.decisions + 1);
        }
        else
        {
            _final[shardId] = (newSize, 1);
        }
    }
}