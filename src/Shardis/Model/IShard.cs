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
    /// Asynchronously creates a new session for the shard.
    /// Default implementation wraps <see cref="CreateSession"/> in a completed <see cref="ValueTask{T}"/>.
    /// Implementations backed by async factories should override this for better performance.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A value task that resolves to a new session.</returns>
    ValueTask<TSession> CreateSessionAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(CreateSession());

    /// <summary>Optional query executor for provider-specific LINQ operations (legacy; pending consolidation).</summary>
    IShardLinqExecutor<TSession> QueryExecutor { get; }
}