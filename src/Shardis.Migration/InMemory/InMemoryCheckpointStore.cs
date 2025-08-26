namespace Shardis.Migration.InMemory;

using System.Collections.Concurrent;
using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

/// <summary>
/// Thread-safe in-memory checkpoint store. Suitable for tests only.
/// </summary>
internal sealed class InMemoryCheckpointStore<TKey> : IShardMigrationCheckpointStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly ConcurrentDictionary<Guid, MigrationCheckpoint<TKey>> _store = new();

    public Task<MigrationCheckpoint<TKey>?> LoadAsync(Guid planId, CancellationToken ct)
    {
        _store.TryGetValue(planId, out var cp);
        return Task.FromResult(cp);
    }

    public Task PersistAsync(MigrationCheckpoint<TKey> checkpoint, CancellationToken ct)
    {
        // Shallow copy to avoid external mutation (record already protects but dictionary inside is defensive copied at construction).
        _store[checkpoint.PlanId] = checkpoint;
        return Task.CompletedTask;
    }
}