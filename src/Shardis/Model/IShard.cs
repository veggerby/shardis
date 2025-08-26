using Shardis.Querying.Linq;

namespace Shardis.Model;

/// <summary>
/// Represents a shard that provides session management.
/// </summary>
/// <typeparam name="TSession">The type of session managed by the shard.</typeparam>
public interface IShard<TSession>
{
    /// <summary>
    /// Gets the unique identifier of the shard.
    /// </summary>
    ShardId ShardId { get; }

    /// <summary>
    /// Creates a new session for the shard.
    /// </summary>
    /// <returns>A new session of type <typeparamref name="TSession"/>.</returns>
    TSession CreateSession();

    /// <summary>
    /// Gets the query executor capable of running LINQ expressions or provider-specific queries
    /// against this shard's backing store using a <typeparamref name="TSession"/> instance.
    /// </summary>
    IShardQueryExecutor<TSession> QueryExecutor { get; }
}