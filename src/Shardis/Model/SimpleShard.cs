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

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleShard"/> class.
    /// </summary>
    /// <param name="shardId">The unique identifier of the shard.</param>
    /// <param name="connectionString">The connection string for the shard.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="shardId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionString"/> is null.</exception>
    /// <param name="queryExecutor">Optional query executor enabling LINQ operations.</param>
    public SimpleShard(ShardId shardId, string connectionString, IShardQueryExecutor<string>? queryExecutor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shardId.Value, nameof(shardId));
        ArgumentNullException.ThrowIfNull(connectionString, nameof(connectionString));
        QueryExecutor = queryExecutor ?? NoOpQueryExecutor<string>.Instance;
        ShardId = shardId;
        ConnectionString = connectionString;

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

    /// <summary>Gets the configured query executor (or a no-op executor if none supplied).</summary>
    public IShardQueryExecutor<string> QueryExecutor { get; }
}