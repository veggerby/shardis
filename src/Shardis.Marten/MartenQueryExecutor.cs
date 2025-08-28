using System.Linq.Expressions;

using Marten;

using Shardis.Query.Diagnostics;
using Shardis.Query.Marten;
using Shardis.Querying.Linq;

namespace Shardis.Marten;

/// <summary>
/// Marten implementation of <see cref="IShardQueryExecutor{TSession}"/> translating expression trees to Marten LINQ queries.
/// </summary>
public sealed class MartenQueryExecutor : IShardQueryExecutor<IDocumentSession>
{
    /// <summary>
    /// Singleton instance to avoid repeated allocations.
    /// </summary>
    public static readonly MartenQueryExecutor Instance = new();
    private readonly IQueryMetricsObserver _metrics;
    private readonly IQueryableShardMaterializer _materializer;

    private MartenQueryExecutor(IQueryMetricsObserver? metrics = null, IQueryableShardMaterializer? materializer = null)
    {
        _metrics = metrics ?? NoopQueryMetricsObserver.Instance;
        _materializer = materializer ?? new MartenMaterializer();
    }
    /// <summary>Create a new executor instance with a metrics observer.</summary>
    public MartenQueryExecutor WithMetrics(IQueryMetricsObserver metrics) => new(metrics, _materializer);
    /// <summary>Create a new executor instance with a custom materializer.</summary>
    public MartenQueryExecutor WithMaterializer(IQueryableShardMaterializer materializer) => new(_metrics, materializer);
    /// <summary>Create a new executor instance with a custom page size (used by default materializer paging).</summary>
    public MartenQueryExecutor WithPageSize(int pageSize) => new(_metrics, new MartenMaterializer(pageSize));
    /// <summary>Create a new executor instance with adaptive paging materializer.</summary>
    public MartenQueryExecutor WithAdaptivePaging(
        int minPageSize = 64,
        int maxPageSize = 8192,
        double targetBatchMilliseconds = 75,
        double growFactor = 1.5,
    double shrinkFactor = 0.5,
    IAdaptivePagingObserver? observer = null)
    => new(_metrics, new AdaptiveMartenMaterializer(minPageSize, maxPageSize, targetBatchMilliseconds, growFactor, shrinkFactor, observer));

    /// <summary>
    /// Executes an unordered Marten LINQ query.
    /// </summary>
    public IAsyncEnumerable<T> Execute<T>(IDocumentSession session, Expression<Func<IQueryable<T>, IQueryable<T>>> expr) where T : notnull
    {
        _metrics.OnShardStart(0);
        var queryable = session.Query<T>();
        var transformed = expr.Compile().Invoke(queryable);
        return Wrap(ct => _materializer.ToAsyncEnumerable(transformed, ct));
    }

    /// <summary>
    /// Executes an ordered Marten LINQ query preserving backend ordering.
    /// </summary>
    public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(
        IDocumentSession session,
        Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr,
        Func<T, TKey> keySelector) where T : notnull
    {
        _metrics.OnShardStart(0);
        var queryable = session.Query<T>();
        var transformed = orderedExpr.Compile().Invoke(queryable);
        return Wrap(ct => _materializer.ToAsyncEnumerable(transformed, ct));
    }

    /// <summary>
    /// Enumerates an <see cref="IEnumerable{T}"/> as an <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    private IAsyncEnumerable<TItem> Wrap<TItem>(Func<CancellationToken, IAsyncEnumerable<TItem>> factory)
    {
        return Impl();

        async IAsyncEnumerable<TItem> Impl([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            var source = factory(ct);
            await using var enumerator = source.GetAsyncEnumerator(ct);
            var canceled = false;
            try
            {
                while (true)
                {
                    TItem current;
                    try
                    {
                        if (!await enumerator.MoveNextAsync().ConfigureAwait(false)) break;
                        current = enumerator.Current;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        canceled = true;
                        _metrics.OnCanceled();
                        break;
                    }
                    _metrics.OnItemsProduced(0, 1);
                    yield return current;
                }
            }
            finally
            {
                _metrics.OnShardStop(0);
                if (!canceled) _metrics.OnCompleted();
            }
        }
    }
}