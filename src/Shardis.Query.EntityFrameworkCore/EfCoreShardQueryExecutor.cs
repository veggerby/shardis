using System.Diagnostics;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Model;
using Shardis.Query.EntityFrameworkCore.Execution;
using Shardis.Query.Execution;

namespace Shardis.Query.EntityFrameworkCore;

/// <summary>
/// Factory helpers for creating EF Core shard query executors with reduced ceremony.
/// </summary>
public static class EfCoreShardQueryExecutor
{
    /// <summary>
    /// Create an unordered EF Core shard query executor (generic context factory variant).
    /// </summary>
    public static IShardQueryExecutor CreateUnordered<TContext>(int shardCount,
                                                                IShardFactory<TContext> contextFactory,
                                                                EfCoreExecutionOptions? options = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        var adapter = new DbContextFactoryAdapter<TContext>(contextFactory);
        return CreateInternal(shardCount, adapter, options);
    }

    /// <summary>
    /// Create an unordered EF Core shard query executor (DbContext factory variant).
    /// </summary>
    public static IShardQueryExecutor CreateUnordered(int shardCount,
                                                       IShardFactory<DbContext> contextFactory,
                                                       EfCoreExecutionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        return CreateInternal(shardCount, contextFactory, options);
    }

    /// <summary>
    /// Create an ordered (buffered) EF Core shard query executor. This variant materializes per-shard results then performs a stable merge.
    /// Suitable when a global ordering (e.g. by primary key) is required and result cardinality is bounded.
    /// </summary>
    /// <remarks>
    /// Implementation note: Current approach buffers each shard's full result set before k-way merging. Future versions may adopt
    /// a streaming k-way cursor when providers can surface ordered subsets efficiently. Use with care for large datasets.
    /// </remarks>
    public static IShardQueryExecutor CreateOrdered<TContext, TOrder>(int shardCount,
                                                                      IShardFactory<TContext> contextFactory,
                                                                      Expression<Func<TOrder, object>> orderKey,
                                                                      bool descending = false,
                                                                      EfCoreExecutionOptions? options = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(orderKey);

        var adapter = new DbContextFactoryAdapter<TContext>(contextFactory);
        var unordered = CreateInternal(shardCount, adapter, options);
        return new OrderedWrapperExecutor(unordered, orderKey, descending);
    }

    internal static IShardQueryExecutor CreateOrderedFromExisting(EntityFrameworkCoreShardQueryExecutor existingUnordered,
                                                                  Expression<Func<object, object>> orderKey,
                                                                  bool descending)
    {
        return new OrderedWrapperExecutor(existingUnordered, orderKey, descending);
    }

    /// <summary>
    /// Internal factory abstraction to avoid reflection for ordered wrapper creation.
    /// </summary>
    internal interface IOrderedEfCoreExecutorFactory
    {
        IShardQueryExecutor CreateOrdered(IShardQueryExecutor unordered, LambdaExpression orderKey, bool descending);
    }

    /// <summary>
    /// Default implementation of <see cref="IOrderedEfCoreExecutorFactory"/> creating the buffered ordered wrapper.
    /// Exposed publicly to allow test harnesses and advanced callers to construct ordered executors without reflection.
    /// </summary>
    public sealed class DefaultOrderedEfCoreExecutorFactory : IOrderedEfCoreExecutorFactory
    {
        /// <summary>
        /// Create an ordered (buffered) executor wrapping the provided unordered EF Core executor.
        /// </summary>
        /// <param name="unordered">Underlying unordered EF executor; must be <see cref="EntityFrameworkCoreShardQueryExecutor"/>.</param>
        /// <param name="orderKey">Lambda producing the global ordering key from the result element.</param>
        /// <param name="descending">Whether ordering is descending.</param>
        public IShardQueryExecutor CreateOrdered(IShardQueryExecutor unordered, LambdaExpression orderKey, bool descending)
        {
            if (unordered is not EntityFrameworkCoreShardQueryExecutor efExec)
            {
                throw new ArgumentException("Executor must be EntityFrameworkCoreShardQueryExecutor", nameof(unordered));
            }
            // Convert generic key (LambdaExpression) into object->object lambda as existing helper expects.
            var objParam = Expression.Parameter(typeof(object), "o");
            var originalParam = orderKey.Parameters[0];
            var castParam = Expression.Convert(objParam, originalParam.Type);
            var replacedBody = new OrderedWrapperExecutor.ParameterReplaceVisitor(originalParam, castParam).Visit(orderKey.Body)!;
            var bodyAsObject = Expression.Convert(replacedBody, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(bodyAsObject, objParam);
            return new OrderedWrapperExecutor(efExec, lambda, descending);
        }
    }

    private static IShardQueryExecutor CreateInternal(int shardCount,
                                                       IShardFactory<DbContext> contextFactory,
                                                       EfCoreExecutionOptions? options)
    {
        // For now defer all tuning to merge delegate hints; channel capacity only.
        var channelCapacity = options?.ChannelCapacity;
        return new EntityFrameworkCoreShardQueryExecutor(
            shardCount: shardCount,
            contextFactory: contextFactory,
            merge: (streams, ct) => UnorderedMergeHelper.Merge(streams, ct, channelCapacity: channelCapacity),
            commandTimeoutSeconds: (int?)options?.PerShardCommandTimeout?.TotalSeconds,
            maxConcurrency: options?.Concurrency,
            disposeContextPerQuery: options?.DisposeContextPerQuery ?? true,
            channelCapacity: channelCapacity);
    }

    private sealed class DbContextFactoryAdapter<TContext>(IShardFactory<TContext> inner) : IShardFactory<DbContext> where TContext : DbContext
    {
        private readonly IShardFactory<TContext> _inner = inner;

        public async ValueTask<DbContext> CreateAsync(ShardId shardId, CancellationToken ct = default)
            => await _inner.CreateAsync(shardId, ct).ConfigureAwait(false);
    }

    private sealed class OrderedWrapperExecutor : IShardQueryExecutor
    {
        private readonly IShardQueryExecutor _inner;
        private readonly Func<object, object?> _keySelectorCompiled;
        private readonly bool _descending;
        private EntityFrameworkCoreShardQueryExecutor? _efInner; // for unified latency emission

        public OrderedWrapperExecutor(IShardQueryExecutor inner, LambdaExpression keySelector, bool descending)
        {
            _inner = inner;
            _descending = descending;
            // Rewrite the provided key selector lambda (TIn -> TKey) into a compiled delegate object -> object
            // by replacing its parameter with an object parameter cast to the original type.
            var objParam = Expression.Parameter(typeof(object), "o");
            var originalParam = keySelector.Parameters[0];
            var castParam = Expression.Convert(objParam, originalParam.Type);
            var replacedBody = new ParameterReplaceVisitor(originalParam, castParam).Visit(keySelector.Body)!;
            var bodyAsObject = Expression.Convert(replacedBody, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(bodyAsObject, objParam);
            _keySelectorCompiled = lambda.Compile();
        }

        public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, CancellationToken ct = default)
        {
            return ExecuteOrderedAsync<TResult>(model, ct);
        }

        public IShardQueryCapabilities Capabilities => new BasicQueryCapabilities(ordering: true, pagination: false);

        private async IAsyncEnumerable<TResult> ExecuteOrderedAsync<TResult>(QueryModel model, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            var orderingActivity = ShardisQueryActivitySource.Instance.StartActivity("shardis.query.ordering", ActivityKind.Internal);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // If inner is EF executor, suppress its upcoming unordered emission for this query.
            _efInner = _inner as EntityFrameworkCoreShardQueryExecutor;
            _efInner?.SuppressNextLatencyEmission();
            try
            {
                // Materialize underlying unordered results (already merged) then apply ordering.
                var all = new List<TResult>();
                await foreach (var item in _inner!.ExecuteAsync<TResult>(model, ct).WithCancellation(ct).ConfigureAwait(false))
                {
                    all.Add(item);
                }
                orderingActivity?.AddTag("merge.strategy", "ordered");
                orderingActivity?.AddTag("ordering.buffered", true);
                orderingActivity?.AddTag("ordering.materialized.count", all.Count);

                // Apply ordering using dynamic selector.
                Func<TResult, object?> key = r => _keySelectorCompiled(r!);
                IEnumerable<TResult> ordered = _descending ? all.OrderByDescending(key) : all.OrderBy(key);
                foreach (var it in ordered)
                {
                    ct.ThrowIfCancellationRequested();
                    yield return it;
                }
            }
            finally
            {
                sw.Stop();
                orderingActivity?.AddTag("ordering.duration.ms", sw.Elapsed.TotalMilliseconds);
                orderingActivity?.SetStatus(ActivityStatusCode.Ok);

                // Unified latency emission (single histogram) using inner executor's helper.
                if (_efInner is not null)
                {
                    var ctx = _efInner.ConsumePendingLatencyContext();
                    if (ctx is not null)
                    {
                        var baseCtx = ctx.Value;
                        var status = ct.IsCancellationRequested ? "canceled" : baseCtx.resultStatus; // preserve failure if occurred earlier
                        var failureMode = FailureHandlingAmbientAccessor.TryGet() ?? baseCtx.failureMode;
                        _efInner.MetricsSink.RecordQueryMergeLatency(sw.Elapsed.TotalMilliseconds, new Shardis.Query.Diagnostics.QueryMetricTags(
                            dbSystem: baseCtx.dbSystem,
                            provider: "efcore",
                            shardCount: baseCtx.shardCount,
                            targetShardCount: baseCtx.targetShardCount,
                            mergeStrategy: "ordered",
                            orderingBuffered: "true",
                            fanoutConcurrency: baseCtx.effectiveFanout,
                            channelCapacity: baseCtx.channelCapacity,
                            failureMode: failureMode,
                            resultStatus: status,
                            rootType: baseCtx.rootType,
                            invalidShardCount: baseCtx.invalidShardCount));
                    }
                }

                orderingActivity?.Dispose();
            }
        }

        internal sealed class ParameterReplaceVisitor(ParameterExpression source, Expression target) : ExpressionVisitor
        {
            private readonly ParameterExpression _source = source;
            private readonly Expression _target = target;

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _source)
                {
                    return _target;
                }
                return base.VisitParameter(node);
            }
        }
    }
}