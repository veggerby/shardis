namespace Shardis.Model;

/// <summary>
/// Defines the contract for providing sessions for a specific shard.
/// </summary>
/// <typeparam name="TSession">The type of session provided by the implementation.</typeparam>
public interface IShardSessionProvider<TSession>
{
    /// <summary>
    /// Retrieves a session for the specified shard.
    /// </summary>
    /// <param name="shardId">The unique identifier of the shard.</param>
    /// <returns>A session of type <typeparamref name="TSession"/> for the specified shard.</returns>
    TSession GetSession(ShardId shardId);
}
