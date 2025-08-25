using System.Linq.Expressions;

using Shardis.Querying.Linq;

namespace Shardis.Model;

/// <summary>
/// Represents a simple shard with a connection string for session management.
/// </summary>
public sealed class SimpleShard : ISimpleShard
{
    /// <summary>
    /// Gets the unique identifier of the shard.
    /// </summary>
    public ShardId ShardId { get; }

    /// <summary>
    /// Gets the connection string for the shard.
    /// </summary>
    public string ConnectionString { get; } = string.Empty;
    private readonly IShardQueryExecutor<string> _queryExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleShard"/> class.
    /// </summary>
    /// <param name="shardId">The unique identifier of the shard.</param>
    /// <param name="connectionString">The connection string for the shard.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="shardId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionString"/> is null.</exception>
    public SimpleShard(ShardId shardId, string connectionString, IShardQueryExecutor<string>? queryExecutor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shardId.Value, nameof(shardId));
        ArgumentNullException.ThrowIfNull(connectionString, nameof(connectionString));

        ShardId = shardId;
        ConnectionString = connectionString;
        _queryExecutor = queryExecutor ?? NoOpQueryExecutor.Instance;
    }

    /// <summary>
    /// Returns the string representation of the shard.
    /// </summary>
    /// <returns>The shard ID as a string.</returns>
    public override string ToString() => ShardId.ToString();

    /// <summary>
    /// Creates a new session for the shard using the connection string.
    /// </summary>
    /// <returns>The connection string as the session.</returns>
    public string CreateSession() => ConnectionString;

    public IShardQueryExecutor<string> QueryExecutor => _queryExecutor;

    private sealed class NoOpQueryExecutor : IShardQueryExecutor<string>
    {
        public static readonly NoOpQueryExecutor Instance = new();
        public IAsyncEnumerable<T> Execute<T>(string session, Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr) where T : notnull
            => throw new NotSupportedException("No query executor configured for this shard.");
        public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(string session, Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr, Func<T, TKey> keySelector) where T : notnull
            => throw new NotSupportedException("No query executor configured for this shard.");
    }
}