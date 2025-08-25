using System.Linq.Expressions;

using Shardis.Querying.Linq;

namespace Shardis.Model;

/// <summary>
/// Represents a shard that provides session management for a specific shard ID.
/// </summary>
/// <typeparam name="TSession">The type of session managed by the shard.</typeparam>
public class Shard<TSession> : IShard<TSession>
{
    /// <summary>
    /// Gets the unique identifier of the shard.
    /// </summary>
    public ShardId ShardId { get; }

    private readonly IShardSessionProvider<TSession> _sessionProvider;
    private readonly IShardQueryExecutor<TSession> _queryExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="Shard{TSession}"/> class.
    /// </summary>
    /// <param name="shardId">The unique identifier of the shard.</param>
    /// <param name="sessionProvider">The session provider for managing shard sessions.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="shardId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sessionProvider"/> is null.</exception>
    public Shard(ShardId shardId, IShardSessionProvider<TSession> sessionProvider, IShardQueryExecutor<TSession>? queryExecutor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shardId.Value, nameof(shardId));
        ArgumentNullException.ThrowIfNull(sessionProvider, nameof(sessionProvider));

        ShardId = shardId;
        _sessionProvider = sessionProvider;
        _queryExecutor = queryExecutor ?? NoOpQueryExecutor.Instance;
    }

    /// <summary>
    /// Returns the string representation of the shard.
    /// </summary>
    /// <returns>The shard ID as a string.</returns>
    public override string ToString() => ShardId.ToString();

    /// <summary>
    /// Creates a new session for the shard.
    /// </summary>
    /// <returns>A new session of type <typeparamref name="TSession"/>.</returns>
    public TSession CreateSession()
    {
        return _sessionProvider.GetSession(ShardId);
    }

    public IShardQueryExecutor<TSession> QueryExecutor => _queryExecutor;

    private sealed class NoOpQueryExecutor : IShardQueryExecutor<TSession>
    {
        public static readonly NoOpQueryExecutor Instance = new();
        public IAsyncEnumerable<T> Execute<T>(TSession session, Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr)
        {
            throw new NotSupportedException("No query executor configured for this shard.");
        }

        public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(TSession session, Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr, Func<T, TKey> keySelector)
        {
            throw new NotSupportedException("No query executor configured for this shard.");
        }
    }
}