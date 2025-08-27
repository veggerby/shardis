using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

namespace Shardis.Migration.Tests;

internal sealed class InspectableCheckpointStore<TKey> : IShardMigrationCheckpointStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly List<MigrationCheckpoint<TKey>> _persisted = [];
    public IReadOnlyList<MigrationCheckpoint<TKey>> Persisted => _persisted;
    public IEnumerable<int> PersistedIndexes => _persisted.Select(p => p.LastProcessedIndex);
    public MigrationCheckpoint<TKey>? LastPersisted => _persisted.LastOrDefault();
    public Task<MigrationCheckpoint<TKey>?> LoadAsync(Guid planId, CancellationToken ct) => Task.FromResult<MigrationCheckpoint<TKey>?>(null);
    public Task PersistAsync(MigrationCheckpoint<TKey> checkpoint, CancellationToken ct)
    {
        _persisted.Add(checkpoint);
        return Task.CompletedTask;
    }
}