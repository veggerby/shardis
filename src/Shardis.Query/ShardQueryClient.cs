using System.Linq.Expressions;

using Shardis.Query.Execution;

namespace Shardis.Query;

/// <summary>
/// Default implementation of <see cref="IShardQueryClient"/> delegating to an underlying <see cref="IShardQueryExecutor"/>.
/// </summary>
/// <remarks>
/// This type is registered via <see cref="QueryClientServiceCollectionExtensions.AddShardisQueryClient"/> and is intentionally
/// lightweight: it does not introduce additional buffering, caching, or state. All query semantics are owned by the executor
/// returned from provider packages (e.g. EF Core, Marten, in-memory).
/// </remarks>
public sealed class ShardQueryClient(IShardQueryExecutor executor) : IShardQueryClient
{
    private readonly IShardQueryExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <inheritdoc />
    public IShardQueryable<T> Query<T>() => ShardQuery.For<T>(_executor);

    /// <inheritdoc />
    public IShardQueryable<TResult> Query<T, TResult>(Expression<Func<T, bool>>? where = null,
                                                      Expression<Func<T, TResult>>? select = null)
    {
        var root = ShardQuery.For<T>(_executor);

        if (where is not null)
        {
            root = ShardQueryableExtensions.Where(root, where);
        }

        if (select is not null)
        {
            return ShardQueryableSelectExtensions.Select((IShardQueryable<T>)root, select);
        }

        if (typeof(TResult) == typeof(T))
        {
            return (IShardQueryable<TResult>)root;
        }

        throw new InvalidOperationException("Projection (select) must be supplied when the result type differs.");
    }
}
