using Shardis.Migration.Abstractions;
using Shardis.Migration.InMemory;
using Shardis.Migration.Model;

namespace Shardis.Migration.Tests;

internal sealed class CountingCheckpointStore<TKey> : IShardMigrationCheckpointStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly InMemoryCheckpointStore<TKey> _inner = new();
    public int PersistCount { get; private set; }
    public Task<MigrationCheckpoint<TKey>?> LoadAsync(Guid planId, CancellationToken ct) => _inner.LoadAsync(planId, ct);
    public Task PersistAsync(MigrationCheckpoint<TKey> checkpoint, CancellationToken ct)
    {
        PersistCount++;
        return _inner.PersistAsync(checkpoint, ct);
    }
}