namespace Shardis.Migration.Marten;

using global::Marten;

using Shardis.Model;

/// <summary>
/// Creates Marten per-shard query and document sessions for migration operations.
/// Implementations must ensure isolation across shards (e.g. separate schemas / databases)
/// and must not return pooled sessions that might leak state between shards.
/// </summary>
public interface IMartenSessionFactory
{
    /// <summary>Create a read-only query session for the given shard.</summary>
    ValueTask<IQuerySession> CreateQuerySessionAsync(ShardId shardId, CancellationToken cancellationToken = default);

    /// <summary>Create a document session for write operations for the given shard.</summary>
    ValueTask<IDocumentSession> CreateDocumentSessionAsync(ShardId shardId, CancellationToken cancellationToken = default);
}