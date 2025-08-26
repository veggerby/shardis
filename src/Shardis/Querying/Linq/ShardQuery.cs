using System.Linq.Expressions;

namespace Shardis.Querying.Linq;

/// <summary>
/// Entry points for constructing shard-composed queries.
/// </summary>
public static class ShardQuery
{
    /// <summary>
    /// Begins a query for the element type <typeparamref name="T"/>.
    /// </summary>
    public static IShardQueryable<T> For<T>() => new InitialShardQueryable<T>();
}

/// <summary>
/// Internal builder that wraps the composed shard query and executes it via the broadcaster.
/// </summary>
internal sealed class ShardQuery<TSession, T>(IShardStreamBroadcaster<TSession> broadcaster, Func<TSession, IQueryable<T>> query) : IShardQueryable<T>
{
    private readonly IShardStreamBroadcaster<TSession> _broadcaster = broadcaster;
    private readonly Func<TSession, IQueryable<T>> _query = query;
    private readonly ShardQueryOptions _options = new();

    private Expression<Func<T, bool>>? _where;
    private LambdaExpression? _selector;
    private LambdaExpression? _orderBy;

    public IShardQueryable<T> Where(Expression<Func<T, bool>> predicate)
    {
        _where = predicate;
        return this;
    }

    public IShardQueryable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector)
    {
        _selector = selector;
        return new ShardQuery<TSession, TResult>(
            _broadcaster,
            session => _query(session).Select(selector));
    }

    public IShardQueryable<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        where TKey : IComparable<TKey>
    {
        _orderBy = keySelector;
        return this;
    }

    public IShardQueryable<T> WithOptions(Action<ShardQueryOptions> configure)
    {
        configure(_options);
        return this;
    }

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var effectiveToken = MergeToken(cancellationToken);

        Func<TSession, IAsyncEnumerable<T>> asyncQuery = session =>
        {
            var queryable = _query(session);
            if (_where != null)
            {
                queryable = queryable.Where(_where);
            }
            // Ordering not currently applied without full provider infrastructure
            return Enumerate(queryable);
        };

        var results = new List<T>();
        await foreach (var shardItem in _broadcaster.QueryAllShardsAsync(asyncQuery, effectiveToken).ConfigureAwait(false))
        {
            results.Add(shardItem.Item);
        }
        // Global ordering skipped if _orderBy specified; future implementation can reintroduce
        return results;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var token = MergeToken(cancellationToken);

        var query = _query;
        if (_where != null)
        {
            query = session => _query(session).Where(_where);
        }

        int count = 0;
        await foreach (var _ in _broadcaster.QueryAllShardsAsync(session => Enumerate(query(session).Select(_ => 1)), token))
        {
            count++;
        }
        return count;
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        var linked = MergeToken(cancellationToken);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(linked);
        var query = _query;
        if (_where != null)
        {
            query = session => _query(session).Where(_where);
        }
        await foreach (var _ in _broadcaster.QueryAllShardsAsync(session => Enumerate(query(session)), cts.Token))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    public async Task<T> FirstAsync(CancellationToken cancellationToken = default)
    {
        var linked = MergeToken(cancellationToken);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(linked);
        var query = _query;
        if (_where != null)
        {
            query = session => _query(session).Where(_where);
        }
        await foreach (var item in _broadcaster.QueryAllShardsAsync(session => Enumerate(query(session)), cts.Token))
        {
            cts.Cancel();
            return item.Item;
        }
        throw new InvalidOperationException("Sequence contains no elements.");
    }

    private CancellationToken MergeToken(CancellationToken external)
    {
        if (_options.CancellationToken != default && external != default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(external, _options.CancellationToken);
            return cts.Token;
        }

        return external != default ? external : _options.CancellationToken;
    }

    private static async IAsyncEnumerable<TItem> Enumerate<TItem>(IEnumerable<TItem> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.CompletedTask; // keep method async without per-item yielding cost
        }
    }
}