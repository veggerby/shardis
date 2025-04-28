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

    /// <summary>
    /// Initializes a new instance of the <see cref="Shard{TSession}"/> class.
    /// </summary>
    /// <param name="shardId">The unique identifier of the shard.</param>
    /// <param name="sessionProvider">The session provider for managing shard sessions.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="shardId"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sessionProvider"/> is null.</exception>
    public Shard(ShardId shardId, IShardSessionProvider<TSession> sessionProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shardId.Value, nameof(shardId));
        ArgumentNullException.ThrowIfNull(sessionProvider, nameof(sessionProvider));

        ShardId = shardId;
        _sessionProvider = sessionProvider;
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
}
