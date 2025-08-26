namespace Shardis.Migration.Abstractions;

using Shardis.Migration.Model;

/// <summary>
/// Persists and retrieves migration checkpoints for recovery and resumption.
/// </summary>
/// <typeparam name="TKey">Underlying key value type.</typeparam>
public interface IShardMigrationCheckpointStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Loads the latest checkpoint for a plan or null if none exists.</summary>
    /// <param name="planId">The plan identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The latest checkpoint or null.</returns>
    Task<MigrationCheckpoint<TKey>?> LoadAsync(Guid planId, CancellationToken ct);

    /// <summary>Persists the provided checkpoint (replace or upsert semantics).</summary>
    /// <param name="checkpoint">The checkpoint to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PersistAsync(MigrationCheckpoint<TKey> checkpoint, CancellationToken ct);
}